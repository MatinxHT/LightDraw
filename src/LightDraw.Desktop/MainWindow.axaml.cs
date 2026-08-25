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
            _viewModel.AboutRequested -= OnAboutRequested;
            _viewModel.RotateSelectedRequested -= OnRotateSelectedRequested;
            _viewModel.SetSelectedAngleRequested -= OnSetSelectedAngleRequested;
            _viewModel.SetSelectedFocalLengthRequested -= OnSetSelectedFocalLengthRequested;
            _viewModel.SetSelectedSphericalMirrorRadiusRequested -= OnSetSelectedSphericalMirrorRadiusRequested;
            _viewModel.SetSelectedSphericalMirrorArcAngleRequested -= OnSetSelectedSphericalMirrorArcAngleRequested;
            _viewModel.SetSelectedApertureOpeningRequested -= OnSetSelectedApertureOpeningRequested;
            _viewModel.SetSelectedGrooveDensityRequested -= OnSetSelectedGrooveDensityRequested;
            _viewModel.SetSelectedWavelengthRequested -= OnSetSelectedWavelengthRequested;
            _viewModel.SetSelectedLengthRequested -= OnSetSelectedLengthRequested;
            _viewModel.SetSelectedOriginRequested -= OnSetSelectedOriginRequested;
            _viewModel.SetSelectedSecondOriginRequested -= OnSetSelectedSecondOriginRequested;
        }

        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.ResetViewRequested += OnResetViewRequested;
        _viewModel.AboutRequested += OnAboutRequested;
        _viewModel.RotateSelectedRequested += OnRotateSelectedRequested;
        _viewModel.SetSelectedAngleRequested += OnSetSelectedAngleRequested;
        _viewModel.SetSelectedFocalLengthRequested += OnSetSelectedFocalLengthRequested;
        _viewModel.SetSelectedSphericalMirrorRadiusRequested += OnSetSelectedSphericalMirrorRadiusRequested;
        _viewModel.SetSelectedSphericalMirrorArcAngleRequested += OnSetSelectedSphericalMirrorArcAngleRequested;
        _viewModel.SetSelectedApertureOpeningRequested += OnSetSelectedApertureOpeningRequested;
        _viewModel.SetSelectedGrooveDensityRequested += OnSetSelectedGrooveDensityRequested;
        _viewModel.SetSelectedWavelengthRequested += OnSetSelectedWavelengthRequested;
        _viewModel.SetSelectedLengthRequested += OnSetSelectedLengthRequested;
        _viewModel.SetSelectedOriginRequested += OnSetSelectedOriginRequested;
        _viewModel.SetSelectedSecondOriginRequested += OnSetSelectedSecondOriginRequested;
        Canvas.SimulationCompleted += OnSimulationCompleted;
        Canvas.ToolStateChanged += OnToolStateChanged;
        Canvas.SceneChanged += OnSceneChanged;
        Canvas.SelectionChanged += OnSelectionChanged;
        viewModel.UpdateSimulation(Canvas.SimulationResult);
        viewModel.UpdateSelection(Canvas.Selection);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ResetViewRequested -= OnResetViewRequested;
            _viewModel.AboutRequested -= OnAboutRequested;
            _viewModel.RotateSelectedRequested -= OnRotateSelectedRequested;
            _viewModel.SetSelectedAngleRequested -= OnSetSelectedAngleRequested;
            _viewModel.SetSelectedFocalLengthRequested -= OnSetSelectedFocalLengthRequested;
            _viewModel.SetSelectedSphericalMirrorRadiusRequested -= OnSetSelectedSphericalMirrorRadiusRequested;
            _viewModel.SetSelectedSphericalMirrorArcAngleRequested -= OnSetSelectedSphericalMirrorArcAngleRequested;
            _viewModel.SetSelectedApertureOpeningRequested -= OnSetSelectedApertureOpeningRequested;
            _viewModel.SetSelectedGrooveDensityRequested -= OnSetSelectedGrooveDensityRequested;
            _viewModel.SetSelectedWavelengthRequested -= OnSetSelectedWavelengthRequested;
            _viewModel.SetSelectedLengthRequested -= OnSetSelectedLengthRequested;
            _viewModel.SetSelectedOriginRequested -= OnSetSelectedOriginRequested;
            _viewModel.SetSelectedSecondOriginRequested -= OnSetSelectedSecondOriginRequested;
        }

        Canvas.SimulationCompleted -= OnSimulationCompleted;
        Canvas.ToolStateChanged -= OnToolStateChanged;
        Canvas.SceneChanged -= OnSceneChanged;
        Canvas.SelectionChanged -= OnSelectionChanged;
        base.OnClosed(e);
    }

    private void OnResetViewRequested(object? sender, EventArgs e) => Canvas.ResetView();

    private void OnAboutRequested(object? sender, EventArgs e) =>
        _ = new AboutWindow().ShowDialog(this);

    private void OnSimulationCompleted(object? sender, EventArgs e) =>
        _viewModel?.UpdateSimulation(Canvas.SimulationResult);

    private void OnToolStateChanged(object? sender, EventArgs e) =>
        _viewModel?.UpdateToolState(Canvas.ActiveTool, Canvas.IsPlacing);

    private void OnSelectionChanged(object? sender, EventArgs e) =>
        _viewModel?.UpdateSelection(Canvas.Selection);

    private void OnRotateSelectedRequested(double degrees) =>
        Canvas.RotateSelectedBy(degrees);

    private void OnSetSelectedAngleRequested(double degrees) =>
        Canvas.SetSelectedAngle(degrees);

    private void OnSetSelectedFocalLengthRequested(double focalLength) =>
        Canvas.SetSelectedFocalLength(focalLength);

    private void OnSetSelectedSphericalMirrorRadiusRequested(double radius) =>
        Canvas.SetSelectedSphericalMirrorRadius(radius);

    private void OnSetSelectedSphericalMirrorArcAngleRequested(double angleDegrees) =>
        Canvas.SetSelectedSphericalMirrorArcAngle(angleDegrees);

    private void OnSetSelectedApertureOpeningRequested(double openingSize) =>
        Canvas.SetSelectedApertureOpening(openingSize);

    private void OnSetSelectedGrooveDensityRequested(double grooveDensity) =>
        Canvas.SetSelectedGrooveDensity(grooveDensity);

    private void OnSetSelectedWavelengthRequested(double wavelengthNanometers) =>
        Canvas.SetSelectedWavelength(wavelengthNanometers);

    private void OnSetSelectedLengthRequested(double length) =>
        Canvas.SetSelectedLength(length);

    private void OnSetSelectedOriginRequested(double x, double y) =>
        Canvas.SetSelectedOrigin(x, y);

    private void OnSetSelectedSecondOriginRequested(double x, double y) =>
        Canvas.SetSelectedSecondOrigin(x, y);

    private void OnSceneChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CurrentScene = Canvas.Scene;
        }
    }
}
