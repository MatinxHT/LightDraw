using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using LightDraw.Desktop.Controls;
using LightDraw.Desktop.ViewModels;

namespace LightDraw.Desktop.Services;

/// <summary>Connects document actions to focus, keyboard input and a shared confirmation overlay.</summary>
public sealed class DocumentViewBinding : IDisposable
{
    private readonly Control _view;
    private readonly Control _canvas;
    private readonly SceneDocumentViewModel _document;
    private readonly UnsavedChangesPrompt _prompt;
    private TextBox? _propertyField;
    private string? _originalText;
    private bool _wasEditingEnabled;

    public DocumentViewBinding(Control view, Control canvas, SceneDocumentViewModel document,
        UnsavedChangesPrompt prompt, Action finishInteraction)
    {
        _view = view; _canvas = canvas; _document = document; _prompt = prompt;
        _wasEditingEnabled = document.IsEditingEnabled;
        document.FinishEditing = () => { canvas.Focus(); finishInteraction(); };
        document.ConfirmUnsavedChangesAsync = prompt.ShowAsync;
        view.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        document.PropertyChanged += OnDocumentPropertyChanged;
        view.AddHandler(InputElement.GotFocusEvent, OnGotFocus);
        view.AddHandler(InputElement.LostFocusEvent, OnLostFocus);
        view.AddHandler(TextBox.TextChangedEvent, OnTextChanged);
    }

    private void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.Source is not TextBox { Tag: "SceneProperty" } field) return;
        _propertyField = field;
        _originalText = field.Text;
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_propertyField is { } field && ReferenceEquals(e.Source, field))
            _document.SetPendingTextEdit(field.Text != _originalText);
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, _propertyField)) return;
        _propertyField = null;
        _document.SetPendingTextEdit(false);
    }

    private void OnDocumentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SceneDocumentViewModel.IsEditingEnabled)) return;
        var restoreFocus = !_wasEditingEnabled && _document.IsEditingEnabled;
        _wasEditingEnabled = _document.IsEditingEnabled;
        if (restoreFocus) _canvas.Focus();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        var isTextInput = e.Source is Visual source &&
            (source is TextBox || source.GetVisualAncestors().OfType<TextBox>().Any());
        if (_document.IsEditingEnabled && isTextInput && e.Key == Key.Enter)
        {
            _document.FinishEditing?.Invoke();
            e.Handled = true;
            return;
        }
        var commandModifiers = Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers
            ?? (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control);
        var modifiers = e.KeyModifiers;
        if ((modifiers & commandModifiers) != commandModifiers || (modifiers & KeyModifiers.Alt) != 0) return;

        ICommand? command = e.Key switch
        {
            Key.S => _document.SaveSceneCommand,
            Key.O => _document.OpenSceneCommand,
            Key.Z when !isTextInput => (modifiers & KeyModifiers.Shift) != 0 ? _document.RedoCommand : _document.UndoCommand,
            Key.Y when !isTextInput => _document.RedoCommand,
            _ => null
        };
        if (command is null) return;
        // Prevent browser Save Page/Open File even when a document command is temporarily disabled.
        e.Handled = true;
        if (_document.IsEditingEnabled && command.CanExecute(null)) command.Execute(null);
    }

    public void Dispose()
    {
        _view.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        _view.RemoveHandler(InputElement.GotFocusEvent, OnGotFocus);
        _view.RemoveHandler(InputElement.LostFocusEvent, OnLostFocus);
        _view.RemoveHandler(TextBox.TextChangedEvent, OnTextChanged);
        _document.PropertyChanged -= OnDocumentPropertyChanged;
        _document.SetPendingTextEdit(false);
        _document.FinishEditing = null;
        _document.ConfirmUnsavedChangesAsync = null;
        _prompt.Cancel();
    }
}
