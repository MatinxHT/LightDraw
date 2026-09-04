using Avalonia.Controls;
using LightDraw.Desktop.Services;
using LightDraw.Desktop.ViewModels;

namespace LightDraw.Desktop.Views;

public sealed partial class ElectrostaticWindow : Window
{
    private readonly ElectrostaticWindowViewModel _viewModel = new();

    public ElectrostaticWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
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

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.ResetViewRequested -= OnResetViewRequested;
        _viewModel.SetSelectedChargeRequested -= OnSetSelectedChargeRequested;
        _viewModel.SetSelectedPotentialRequested -= OnSetSelectedPotentialRequested;
        _viewModel.SetSelectedPlateLengthRequested -= OnSetSelectedPlateLengthRequested;
        _viewModel.SetSelectedPlateAngleRequested -= OnSetSelectedPlateAngleRequested;
        _viewModel.SetSelectedOriginRequested -= OnSetSelectedOriginRequested;
        _viewModel.SetSelectedNameRequested -= OnSetSelectedNameRequested;
        Canvas.SceneChanged -= OnSceneChanged;
        Canvas.SimulationCompleted -= OnSimulationCompleted;
        Canvas.ToolStateChanged -= OnToolStateChanged;
        Canvas.SelectionChanged -= OnSelectionChanged;
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        base.OnClosed(e);
    }

    private void OnResetViewRequested(object? sender, EventArgs e) => Canvas.ResetView();
    private void OnSetSelectedChargeRequested(double value) => Canvas.SetSelectedCharge(value);
    private void OnSetSelectedPotentialRequested(double value) => Canvas.SetSelectedPotential(value);
    private void OnSetSelectedPlateLengthRequested(double value) => Canvas.SetSelectedPlateLength(value);
    private void OnSetSelectedPlateAngleRequested(double value) => Canvas.SetSelectedPlateAngle(value);
    private void OnSetSelectedOriginRequested(double x, double y) => Canvas.SetSelectedOrigin(x, y);
    private void OnSetSelectedNameRequested(string name) => Canvas.SetSelectedName(name);
    private void OnSceneChanged(object? sender, EventArgs e) => _viewModel.CurrentScene = Canvas.Scene;
    private void OnSimulationCompleted(object? sender, EventArgs e) => _viewModel.UpdateSimulation(Canvas.SimulationResult);
    private void OnToolStateChanged(object? sender, EventArgs e) => _viewModel.UpdateToolState(Canvas.ActiveTool);
    private void OnSelectionChanged(object? sender, EventArgs e) => _viewModel.UpdateSelection(Canvas.Selection);
    private void OnLanguageChanged(object? sender, EventArgs e) => _viewModel.RefreshLanguage();
}
