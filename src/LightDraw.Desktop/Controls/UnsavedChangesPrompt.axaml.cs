using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LightDraw.Desktop.ViewModels;

namespace LightDraw.Desktop.Controls;

public sealed partial class UnsavedChangesPrompt : UserControl
{
    private TaskCompletionSource<UnsavedChangesChoice>? _completion;
    public UnsavedChangesPrompt() => InitializeComponent();

    public async Task<UnsavedChangesChoice> ShowAsync()
    {
        if (_completion is not null) return UnsavedChangesChoice.Cancel;
        _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        IsVisible = true;
        CancelButton.Focus();
        try { return await _completion.Task; }
        finally { IsVisible = false; _completion = null; }
    }

    public void Cancel() => _completion?.TrySetResult(UnsavedChangesChoice.Cancel);
    private void OnSave(object? sender, RoutedEventArgs e) => _completion?.TrySetResult(UnsavedChangesChoice.Save);
    private void OnDiscard(object? sender, RoutedEventArgs e) => _completion?.TrySetResult(UnsavedChangesChoice.Discard);
    private void OnCancel(object? sender, RoutedEventArgs e) => Cancel();
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Cancel(); e.Handled = true; }
        base.OnKeyDown(e);
    }
}
