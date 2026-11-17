using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LightDraw.Browser.Services;
using LightDraw.Core.Persistence;
using LightDraw.Core.Scene;
using LightDraw.Desktop.ViewModels;

namespace LightDraw.Browser.Views;

public sealed partial class WorkspaceView : UserControl
{
    private readonly SceneDocumentViewModel[] _documents;
    private int _current;

    public WorkspaceView()
    {
        InitializeComponent();
        var optical = new MainWindowViewModel(new BrowserSceneStorageService<OpticalScene>(OpticalSceneCodec.Instance));
        DataContext = optical;
        Optics.AttachViewModel(optical);
        _documents = [optical.Document, Electro.Document, Magnetic.Document];
        foreach (var document in _documents) document.PropertyChanged += OnDocumentChanged;
        Navigation.AddHandler(InputElement.KeyDownEvent, OnNavigationKeyDown, RoutingStrategies.Tunnel);
        UpdateDocumentState();
    }

    private void OnNavigationKeyDown(object? sender, KeyEventArgs e)
    {
        var commandModifiers = Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers
            ?? (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control);
        if ((e.KeyModifiers & commandModifiers) != commandModifiers || (e.KeyModifiers & KeyModifiers.Alt) != 0) return;
        var document = _documents[_current];
        ICommand? command = e.Key switch
        {
            Key.S => document.SaveSceneCommand,
            Key.O => document.OpenSceneCommand,
            Key.Z => (e.KeyModifiers & KeyModifiers.Shift) != 0 ? document.RedoCommand : document.UndoCommand,
            Key.Y => document.RedoCommand,
            _ => null
        };
        if (command is null) return;
        e.Handled = true;
        if (document.IsEditingEnabled && command.CanExecute(null)) command.Execute(null);
    }

    private void OnDocumentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SceneDocumentViewModel.HasUnsavedChanges) or nameof(SceneDocumentViewModel.IsEditingEnabled))
            UpdateDocumentState();
    }

    private void UpdateDocumentState()
    {
        BrowserFileInterop.SetUnsavedChanges(_documents.Any(document => document.HasUnsavedChanges));
        Navigation.IsEnabled = _documents.All(document => document.IsEditingEnabled);
    }

    private void Select(int index)
    {
        if (index == _current || _documents.Any(document => !document.IsEditingEnabled)) return;
        _documents[_current].FinishEditing?.Invoke();
        _current = index;
        Optics.IsVisible = index == 0;
        Electro.IsVisible = index == 1;
        Magnetic.IsVisible = index == 2;
        OpticsButton.Classes.Set("selected", index == 0);
        ElectroButton.Classes.Set("selected", index == 1);
        MagneticButton.Classes.Set("selected", index == 2);
        _documents[index].FinishEditing?.Invoke();
    }

    private void OnOpticsClick(object? sender, RoutedEventArgs e) => Select(0);
    private void OnElectroClick(object? sender, RoutedEventArgs e) => Select(1);
    private void OnMagneticClick(object? sender, RoutedEventArgs e) => Select(2);
}
