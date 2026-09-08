using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LightDraw.Desktop.Services;
using LightDraw.Desktop.ViewModels;

namespace LightDraw.Desktop.Views;

public sealed partial class MagnetostaticWindow : Window
{
    private readonly MagnetostaticWindowViewModel _viewModel = new();

    public MagnetostaticWindow()
    {
        InitializeComponent(); DataContext = _viewModel;
        _viewModel.ResetViewRequested += OnResetViewRequested;
        _viewModel.SetSelectedCurrentRequested += OnSetSelectedCurrentRequested;
        _viewModel.SetSelectedLengthRequested += OnSetSelectedLengthRequested;
        _viewModel.SetSelectedRadiusRequested += OnSetSelectedRadiusRequested;
        _viewModel.SetSelectedAngleRequested += OnSetSelectedAngleRequested;
        _viewModel.SetSelectedOriginRequested += OnSetSelectedOriginRequested;
        _viewModel.SetSelectedSecondOriginRequested += OnSetSelectedSecondOriginRequested;
        _viewModel.SetSelectedNameRequested += OnSetSelectedNameRequested;
        Canvas.SceneChanged += OnSceneChanged;
        Canvas.SimulationCompleted += OnSimulationCompleted;
        Canvas.ToolStateChanged += OnToolStateChanged;
        Canvas.SelectionChanged += OnSelectionChanged;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        _viewModel.UpdateSelection(Canvas.Selection);
        _viewModel.UpdateSimulation(Canvas.SimulationResult);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.ResetViewRequested -= OnResetViewRequested;
        _viewModel.SetSelectedCurrentRequested -= OnSetSelectedCurrentRequested;
        _viewModel.SetSelectedLengthRequested -= OnSetSelectedLengthRequested;
        _viewModel.SetSelectedRadiusRequested -= OnSetSelectedRadiusRequested;
        _viewModel.SetSelectedAngleRequested -= OnSetSelectedAngleRequested;
        _viewModel.SetSelectedOriginRequested -= OnSetSelectedOriginRequested;
        _viewModel.SetSelectedSecondOriginRequested -= OnSetSelectedSecondOriginRequested;
        _viewModel.SetSelectedNameRequested -= OnSetSelectedNameRequested;
        Canvas.SceneChanged -= OnSceneChanged;
        Canvas.SimulationCompleted -= OnSimulationCompleted;
        Canvas.ToolStateChanged -= OnToolStateChanged;
        Canvas.SelectionChanged -= OnSelectionChanged;
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        base.OnClosed(e);
    }

    private void OnResetViewRequested(object? sender, EventArgs e) => Canvas.ResetView();

    private async void OnExportCanvasClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出画布 PNG",
            SuggestedFileName = "lightdraw-magnetostatic-canvas",
            DefaultExtension = "png",
            FileTypeChoices = [new FilePickerFileType("PNG 图片") { Patterns = ["*.png"], MimeTypes = ["image/png"] }]
        });
        if (file is null) return;
        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        Canvas.ExportPng(stream);
    }

    private void OnSetSelectedCurrentRequested(double value) => Canvas.SetSelectedCurrent(value);
    private void OnSetSelectedLengthRequested(double value) => Canvas.SetSelectedLength(value);
    private void OnSetSelectedRadiusRequested(double value) => Canvas.SetSelectedRadius(value);
    private void OnSetSelectedAngleRequested(double value) => Canvas.SetSelectedAngle(value);
    private void OnSetSelectedOriginRequested(double x, double y) => Canvas.SetSelectedOrigin(x, y);
    private void OnSetSelectedSecondOriginRequested(double x, double y) => Canvas.SetSelectedSecondOrigin(x, y);
    private void OnSetSelectedNameRequested(string name) => Canvas.SetSelectedName(name);
    private void OnSceneChanged(object? sender, EventArgs e) => _viewModel.CurrentScene = Canvas.Scene;
    private void OnSimulationCompleted(object? sender, EventArgs e) => _viewModel.UpdateSimulation(Canvas.SimulationResult);
    private void OnToolStateChanged(object? sender, EventArgs e) => _viewModel.UpdateToolState(Canvas.ActiveTool);
    private void OnSelectionChanged(object? sender, EventArgs e) => _viewModel.UpdateSelection(Canvas.Selection);
    private void OnLanguageChanged(object? sender, EventArgs e) => _viewModel.RefreshLanguage();
}
