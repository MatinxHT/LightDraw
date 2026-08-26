using Avalonia.Controls;
using LightDraw.Desktop.ViewModels;

namespace LightDraw.Desktop;

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
        Canvas.SceneChanged += OnSceneChanged;
        Canvas.SimulationCompleted += OnSimulationCompleted;
        Canvas.ToolStateChanged += OnToolStateChanged;
        Canvas.SelectionChanged += OnSelectionChanged;
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
        Canvas.SceneChanged -= OnSceneChanged;
        Canvas.SimulationCompleted -= OnSimulationCompleted;
        Canvas.ToolStateChanged -= OnToolStateChanged;
        Canvas.SelectionChanged -= OnSelectionChanged;
        base.OnClosed(e);
    }

    private void OnResetViewRequested(object? sender, EventArgs e) => Canvas.ResetView();
    private void OnSetSelectedCurrentRequested(double value) => Canvas.SetSelectedCurrent(value);
    private void OnSetSelectedLengthRequested(double value) => Canvas.SetSelectedLength(value);
    private void OnSetSelectedRadiusRequested(double value) => Canvas.SetSelectedRadius(value);
    private void OnSetSelectedAngleRequested(double value) => Canvas.SetSelectedAngle(value);
    private void OnSetSelectedOriginRequested(double x, double y) => Canvas.SetSelectedOrigin(x, y);
    private void OnSetSelectedSecondOriginRequested(double x, double y) => Canvas.SetSelectedSecondOrigin(x, y);
    private void OnSceneChanged(object? sender, EventArgs e) => _viewModel.CurrentScene = Canvas.Scene;
    private void OnSimulationCompleted(object? sender, EventArgs e) => _viewModel.UpdateSimulation(Canvas.SimulationResult);
    private void OnToolStateChanged(object? sender, EventArgs e) => _viewModel.UpdateToolState(Canvas.ActiveTool);
    private void OnSelectionChanged(object? sender, EventArgs e) => _viewModel.UpdateSelection(Canvas.Selection);
}
