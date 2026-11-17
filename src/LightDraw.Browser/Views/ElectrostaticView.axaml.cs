using LightDraw.Core.Electromagnetics;
using LightDraw.Core.Persistence;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LightDraw.Browser.Services;
using LightDraw.Desktop.Services;
using LightDraw.Desktop.ViewModels;

namespace LightDraw.Browser.Views;

public sealed partial class ElectrostaticView : UserControl
{
    private readonly ElectrostaticWindowViewModel _viewModel;
    private readonly DocumentViewBinding _documentBinding;

    public ElectrostaticView()
    {
        InitializeComponent();
        _viewModel = new(new BrowserSceneStorageService<ElectrostaticScene>(ElectrostaticSceneCodec.Instance));
        DataContext = _viewModel;
        _documentBinding = new(this, Canvas, _viewModel.Document, UnsavedPrompt, Canvas.FinishInteraction);
        _viewModel.ResetViewRequested += OnResetViewRequested;
        _viewModel.SetSelectedChargeRequested += OnSetSelectedChargeRequested;
        _viewModel.SetSelectedPotentialRequested += OnSetSelectedPotentialRequested;
        _viewModel.SetSelectedPlateLengthRequested += OnSetSelectedPlateLengthRequested;
        _viewModel.SetSelectedPlateAngleRequested += OnSetSelectedPlateAngleRequested;
        _viewModel.SetSelectedOriginRequested += OnSetSelectedOriginRequested;
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
            BrowserFileInterop.Download("lightdraw-electrostatic-canvas.png", "image/png", stream.ToArray());
        }
        catch (Exception exception) { _viewModel.StatusText = exception.Message; }
    }

    private void OnSetSelectedChargeRequested(double value) => Canvas.SetSelectedCharge(value);
    private void OnSetSelectedPotentialRequested(double value) => Canvas.SetSelectedPotential(value);
    private void OnSetSelectedPlateLengthRequested(double value) => Canvas.SetSelectedPlateLength(value);
    private void OnSetSelectedPlateAngleRequested(double value) => Canvas.SetSelectedPlateAngle(value);
    private void OnSetSelectedOriginRequested(double x, double y) => Canvas.SetSelectedOrigin(x, y);
    private void OnSetSelectedNameRequested(string name) => Canvas.SetSelectedName(name);
    private void OnSceneChanged(object? sender, EventArgs e) => _viewModel.Document.Commit(Canvas.Scene);
    private void OnSimulationCompleted(object? sender, EventArgs e) => _viewModel.UpdateSimulation(Canvas.SimulationResult);
    private void OnToolStateChanged(object? sender, EventArgs e) => _viewModel.UpdateToolState(Canvas.ActiveTool);
    private void OnSelectionChanged(object? sender, EventArgs e) => _viewModel.UpdateSelection(Canvas.Selection);
    private void OnLanguageChanged(object? sender, EventArgs e) => _viewModel.RefreshLanguage();
}
