using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightDraw.Core.Editing;
using LightDraw.Core.Persistence;
using LightDraw.Desktop.Services;

namespace LightDraw.Desktop.ViewModels;

public enum UnsavedChangesChoice { Save, Discard, Cancel }

public abstract class SceneDocumentViewModel : ObservableObject
{
    public abstract bool HasUnsavedChanges { get; }
    public abstract bool IsBusy { get; }
    public abstract bool IsEditingEnabled { get; }
    public abstract void SetCloseReview(bool reviewing);
    public abstract void SetPendingTextEdit(bool pending);
    public abstract IRelayCommand UndoCommand { get; }
    public abstract IRelayCommand RedoCommand { get; }
    public abstract IAsyncRelayCommand SaveSceneCommand { get; }
    public abstract IAsyncRelayCommand OpenSceneCommand { get; }
    public abstract IAsyncRelayCommand ResetSceneCommand { get; }
    public Func<Task<UnsavedChangesChoice>>? ConfirmUnsavedChangesAsync { get; set; }
    // Flush focused property fields and finish an active drag before taking a snapshot.
    public Action? FinishEditing { get; set; }
    public abstract Task<bool> RequestCloseAsync();
}

/// <summary>Shared document workflow for optical, electrostatic and magnetostatic scenes.</summary>
public sealed class SceneDocumentViewModel<T> : SceneDocumentViewModel where T : class
{
    private readonly ISceneStorageService<T> _storage;
    private readonly ISceneCodec<T> _codec;
    private readonly SceneHistory<T> _history;
    private T _scene;
    private bool _isBusy;
    private bool _closeReview;
    private bool _pendingTextEdit;
    private bool _applyingScene;
    private readonly RelayCommand _undo;
    private readonly RelayCommand _redo;
    private readonly AsyncRelayCommand _save;
    private readonly AsyncRelayCommand _open;
    private readonly AsyncRelayCommand _reset;

    public SceneDocumentViewModel(ISceneStorageService<T> storage, ISceneCodec<T> codec)
    {
        _storage = storage;
        _codec = codec;
        _scene = codec.Normalize(codec.CreateEmpty());
        _history = new(_scene, codec.Snapshot, codec.ContentEquals);
        _undo = new(() => Restore(undo: true), () => IsEditingEnabled && (_history.CanUndo || _pendingTextEdit));
        _redo = new(() => Restore(undo: false), () => IsEditingEnabled && !_pendingTextEdit && _history.CanRedo);
        _save = new(() => RunAsync(async () => { await SaveCoreAsync(); }), () => IsEditingEnabled);
        _open = new(() => RunAsync(OpenCoreAsync), () => IsEditingEnabled);
        _reset = new(() => RunAsync(ResetCoreAsync), () => IsEditingEnabled);
    }

    public T Scene
    {
        get => _scene;
        set
        {
            if (SetProperty(ref _scene, value)) RefreshState();
        }
    }

    public override bool HasUnsavedChanges => _pendingTextEdit || _history.IsModified(Scene);
    public override bool IsBusy => _isBusy;
    public override bool IsEditingEnabled => !_isBusy && !_closeReview;
    public override void SetCloseReview(bool reviewing)
    {
        _closeReview = reviewing;
        RefreshState();
    }
    public override void SetPendingTextEdit(bool pending)
    {
        if (_pendingTextEdit == pending) return;
        _pendingTextEdit = pending;
        RefreshState();
    }
    public override IRelayCommand UndoCommand => _undo;
    public override IRelayCommand RedoCommand => _redo;
    public override IAsyncRelayCommand SaveSceneCommand => _save;
    public override IAsyncRelayCommand OpenSceneCommand => _open;
    public override IAsyncRelayCommand ResetSceneCommand => _reset;
    public event Action<string>? StatusChanged;
    public event EventHandler? SceneReplaced;
    public event EventHandler? SceneReset;

    public void Commit(T scene)
    {
        Scene = scene;
        if (!_applyingScene) _history.Commit(scene);
        RefreshState();
    }

    private void Restore(bool undo)
    {
        if (IsBusy) return;
        FinishEditing?.Invoke();
        _history.Commit(Scene);
        ApplyScene(undo ? _history.Undo() : _history.Redo());
    }

    private void ApplyScene(T scene)
    {
        _applyingScene = true;
        try { Scene = scene; SceneReplaced?.Invoke(this, EventArgs.Empty); }
        finally { _applyingScene = false; }
        RefreshState();
    }

    private async Task RunAsync(Func<Task> operation)
    {
        if (IsBusy) return;
        FinishEditing?.Invoke();
        SetBusy(true);
        try { await operation(); }
        catch (OperationCanceledException) { }
        catch (Exception exception) { Status("Status.DocumentFailed", exception.Message); }
        finally { SetBusy(false); }
    }

    private async Task OpenCoreAsync()
    {
        // Save before reading, including when the selected file is the one just saved.
        if (!await ConfirmReplacementAsync()) return;
        var opened = await _storage.OpenAsync();
        if (opened is null) return;
        var scene = _codec.Normalize(opened.Scene);
        _history.Reset(scene);
        ApplyScene(scene);
        Status("Status.Opened", opened.FileName);
    }

    private async Task ResetCoreAsync()
    {
        if (!await ConfirmReplacementAsync()) return;
        var empty = _codec.Normalize(_codec.CreateEmpty());
        _history.Commit(empty); // Reset can itself be undone.
        ApplyScene(empty);
        SceneReset?.Invoke(this, EventArgs.Empty);
        Status("Status.SceneReset");
    }

    private async Task<bool> SaveCoreAsync()
    {
        var savedScene = _codec.Snapshot(Scene);
        var name = await _storage.SaveAsync(savedScene);
        if (name is null) return false;
        _history.MarkSaved(savedScene);
        RefreshState();
        Status("Status.Saved", name);
        return !HasUnsavedChanges;
    }

    private async Task<bool> ConfirmReplacementAsync()
    {
        if (!HasUnsavedChanges) return true;
        var choice = ConfirmUnsavedChangesAsync is { } confirm
            ? await confirm() : UnsavedChangesChoice.Cancel;
        return choice switch
        {
            UnsavedChangesChoice.Discard => true,
            UnsavedChangesChoice.Save => await SaveCoreAsync(),
            _ => false
        };
    }

    public override async Task<bool> RequestCloseAsync()
    {
        if (IsBusy) return false;
        var canClose = false;
        await RunAsync(async () => canClose = await ConfirmReplacementAsync());
        return canClose;
    }

    private void SetBusy(bool value)
    {
        _isBusy = value;
        OnPropertyChanged(nameof(IsBusy));
        RefreshState();
    }

    private void RefreshState()
    {
        OnPropertyChanged(nameof(IsEditingEnabled));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        _undo.NotifyCanExecuteChanged(); _redo.NotifyCanExecuteChanged();
        _save.NotifyCanExecuteChanged(); _open.NotifyCanExecuteChanged(); _reset.NotifyCanExecuteChanged();
    }

    private void Status(string key, params object[] values) =>
        StatusChanged?.Invoke(string.Format(LocalizationService.Instance.Get(key), values));
}
