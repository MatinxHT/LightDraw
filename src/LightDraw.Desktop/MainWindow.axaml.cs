using Avalonia.Controls;
using LightDraw.Desktop.ViewModels;

namespace LightDraw.Desktop;

public sealed partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void AttachViewModel(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (_viewModel is not null)
        {
            _viewModel.ResetViewRequested -= OnResetViewRequested;
        }

        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.ResetViewRequested += OnResetViewRequested;
        Canvas.SimulationCompleted += OnSimulationCompleted;
        Canvas.ToolStateChanged += OnToolStateChanged;
        Canvas.SceneChanged += OnSceneChanged;
        viewModel.UpdateSimulation(Canvas.SimulationResult);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ResetViewRequested -= OnResetViewRequested;
        }

        Canvas.SimulationCompleted -= OnSimulationCompleted;
        Canvas.ToolStateChanged -= OnToolStateChanged;
        Canvas.SceneChanged -= OnSceneChanged;
        base.OnClosed(e);
    }

    private void OnResetViewRequested(object? sender, EventArgs e) => Canvas.ResetView();

    private void OnSimulationCompleted(object? sender, EventArgs e) =>
        _viewModel?.UpdateSimulation(Canvas.SimulationResult);

    private void OnToolStateChanged(object? sender, EventArgs e) =>
        _viewModel?.UpdateToolState(Canvas.ActiveTool, Canvas.IsPlacing);

    private void OnSceneChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CurrentScene = Canvas.Scene;
        }
    }
}
