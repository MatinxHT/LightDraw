using LightDraw.Core.Electromagnetics;
using LightDraw.Core.Persistence;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LightDraw.Browser.Services;
using LightDraw.Desktop.Services;
using LightDraw.Desktop.ViewModels;

namespace LightDraw.Browser.Views;

public sealed partial class MagnetostaticView : UserControl
{
    private readonly MagnetostaticWindowViewModel _viewModel;
    private readonly DocumentViewBinding _documentBinding;

    public MagnetostaticView()
    {
        InitializeComponent();
        _viewModel = new(new BrowserSceneStorageService<MagnetostaticScene>(MagnetostaticSceneCodec.Instance));
        DataContext = _viewModel;
        _documentBinding = new(this, Canvas, _viewModel.Document, UnsavedPrompt, Canvas.FinishInteraction);
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

    public SceneDocumentViewModel Document => _viewModel.Document;

    private void OnResetViewRequested(object? sender, EventArgs e) => Canvas.ResetView();

    private void OnExportCanvasClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            using var stream = new MemoryStream();
            Canvas.ExportPng(stream);
            BrowserFileInterop.Download("lightdraw-magnetostatic-canvas.png", "image/png", stream.ToArray());
        }
        catch (Exception exception) { _viewModel.StatusText = exception.Message; }
    }

    private void OnSetSelectedCurrentRequested(double value) => Canvas.SetSelectedCurrent(value);
    private void OnSetSelectedLengthRequested(double value) => Canvas.SetSelectedLength(value);
    private void OnSetSelectedRadiusRequested(double value) => Canvas.SetSelectedRadius(value);
    private void OnSetSelectedAngleRequested(double value) => Canvas.SetSelectedAngle(value);
    private void OnSetSelectedOriginRequested(double x, double y) => Canvas.SetSelectedOrigin(x, y);
    private void OnSetSelectedSecondOriginRequested(double x, double y) => Canvas.SetSelectedSecondOrigin(x, y);
    private void OnSetSelectedNameRequested(string name) => Canvas.SetSelectedName(name);
    private void OnSceneChanged(object? sender, EventArgs e) => _viewModel.Document.Commit(Canvas.Scene);
    private void OnSimulationCompleted(object? sender, EventArgs e) => _viewModel.UpdateSimulation(Canvas.SimulationResult);
    private void OnToolStateChanged(object? sender, EventArgs e) => _viewModel.UpdateToolState(Canvas.ActiveTool);
    private void OnSelectionChanged(object? sender, EventArgs e) => _viewModel.UpdateSelection(Canvas.Selection);
    private void OnLanguageChanged(object? sender, EventArgs e) => _viewModel.RefreshLanguage();
}
