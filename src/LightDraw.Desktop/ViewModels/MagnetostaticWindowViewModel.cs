using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightDraw.Core.Electromagnetics;
using LightDraw.Desktop.Services;
using LightDraw.Rendering.Skia.Magnetostatics;

namespace LightDraw.Desktop.ViewModels;

public sealed partial class MagnetostaticWindowViewModel : ObservableObject
{
    private bool _updatingSelection;
    private MagnetostaticSelection? _selection;

    [ObservableProperty] private MagnetostaticScene _currentScene = MagnetostaticScene.CreateEmpty();
    [ObservableProperty] private MagnetostaticTool _activeTool = MagnetostaticTool.Pan;
    [ObservableProperty] private int _markerDensity = 16;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private bool _hasSelectedLinearConductor;
    [ObservableProperty] private bool _hasSelectedRotatableElement;
    [ObservableProperty] private bool _hasSelectedLoop;
    [ObservableProperty] private bool _hasSelectedSecondOrigin;
    [ObservableProperty] private string _selectedElementName = T("Selection.None");
    [ObservableProperty] private string _selectedElementTitle = string.Empty;
    [ObservableProperty] private decimal _selectedCurrent = 1;
    [ObservableProperty] private decimal _selectedLength = 100;
    [ObservableProperty] private decimal _selectedRadius = 80;
    [ObservableProperty] private decimal _selectedAngle;
    [ObservableProperty] private decimal _rotationStep = 5;
    [ObservableProperty] private int _selectedStandardAngleIndex;
    [ObservableProperty] private decimal _selectedOriginX;
    [ObservableProperty] private decimal _selectedOriginY;
    [ObservableProperty] private decimal _selectedSecondOriginX;
    [ObservableProperty] private decimal _selectedSecondOriginY;
    [ObservableProperty] private string _statusText = T("Status.Ready");

    public event EventHandler? ResetViewRequested;
    public event Action<double>? SetSelectedCurrentRequested;
    public event Action<double>? SetSelectedLengthRequested;
    public event Action<double>? SetSelectedRadiusRequested;
    public event Action<double>? SetSelectedAngleRequested;
    public event Action<double, double>? SetSelectedOriginRequested;
    public event Action<double, double>? SetSelectedSecondOriginRequested;
    public event Action<string>? SetSelectedNameRequested;

    partial void OnMarkerDensityChanged(int value)
    {
        var clamped = Math.Clamp(value, 4, 48);
        if (clamped != value) MarkerDensity = clamped;
    }
    partial void OnSelectedCurrentChanged(decimal value)
    {
        var clamped = Math.Clamp(value, -1_000_000, 1_000_000);
        if (clamped != value) { SelectedCurrent = clamped; return; }
        if (!_updatingSelection && HasSelection) SetSelectedCurrentRequested?.Invoke((double)clamped);
    }
    partial void OnSelectedLengthChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 10, 100_000);
        if (clamped != value) { SelectedLength = clamped; return; }
        if (!_updatingSelection && HasSelectedLinearConductor) SetSelectedLengthRequested?.Invoke((double)clamped);
    }
    partial void OnSelectedRadiusChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 10, 100_000);
        if (clamped != value) { SelectedRadius = clamped; return; }
        if (!_updatingSelection && HasSelectedLoop) SetSelectedRadiusRequested?.Invoke((double)clamped);
    }
    partial void OnSelectedAngleChanged(decimal value)
    {
        var clamped = Math.Clamp(value, -360_000, 360_000);
        if (clamped != value) { SelectedAngle = clamped; return; }
        if (!_updatingSelection && HasSelectedRotatableElement) SetSelectedAngleRequested?.Invoke((double)clamped);
    }
    partial void OnRotationStepChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0.1m, 360);
        if (clamped != value) RotationStep = clamped;
    }
    partial void OnSelectedStandardAngleIndexChanged(int value)
    {
        if (_updatingSelection || !HasSelectedRotatableElement || value is < 1 or > 4) return;
        SelectedAngle = (value - 1) * 90;
    }
    partial void OnSelectedOriginXChanged(decimal value) => ApplyOrigin(value, SelectedOriginY);
    partial void OnSelectedOriginYChanged(decimal value) => ApplyOrigin(SelectedOriginX, value);
    partial void OnSelectedSecondOriginXChanged(decimal value) => ApplySecondOrigin(value, SelectedSecondOriginY);
    partial void OnSelectedSecondOriginYChanged(decimal value) => ApplySecondOrigin(SelectedSecondOriginX, value);
    partial void OnSelectedElementTitleChanged(string value)
    {
        if (!_updatingSelection && HasSelection && !string.IsNullOrWhiteSpace(value) && value.Length <= 120)
            SetSelectedNameRequested?.Invoke(value);
    }

    [RelayCommand]
    private void RotateSelected(string? direction)
    {
        if (!HasSelectedRotatableElement) return;
        SelectedAngle += direction == "Counterclockwise" ? RotationStep : -RotationStep;
    }
    [RelayCommand]
    private void SelectTool(string? value)
    {
        if (!Enum.TryParse<MagnetostaticTool>(value, out var tool)) return;
        ActiveTool = tool; UpdateToolState(tool);
    }
    [RelayCommand]
    private void ResetScene()
    {
        CurrentScene = MagnetostaticScene.CreateEmpty(); ActiveTool = MagnetostaticTool.Pan;
        UpdateSelection(null); StatusText = T("Status.MagneticReset");
    }
    [RelayCommand]
    private void ResetView()
    {
        ResetViewRequested?.Invoke(this, EventArgs.Empty); StatusText = T("Status.ViewReset");
    }

    public void UpdateToolState(MagnetostaticTool tool) => StatusText = tool switch
    {
        MagnetostaticTool.Pan => T("Status.PanZoom"),
        MagnetostaticTool.Move => T("Status.MagneticMove"),
        MagnetostaticTool.Delete => T("Status.MagneticDelete"),
        MagnetostaticTool.PlanarIdealConstantCurrentConductor => T("Status.PlacePlanarConductor"),
        MagnetostaticTool.VerticalInfiniteCurrentConductor => T("Status.PlaceVerticalConductor"),
        MagnetostaticTool.PlanarCircularCurrentLoop => T("Status.PlacePlanarLoop"),
        MagnetostaticTool.VerticalCircularCurrentLoop => T("Status.PlaceVerticalLoop"),
        _ => StatusText
    };

    public void UpdateSelection(MagnetostaticSelection? selection)
    {
        _selection = selection;
        _updatingSelection = true;
        try
        {
            HasSelection = selection is not null;
            HasSelectedLinearConductor = selection?.Kind == MagnetostaticSelectionKind.PlanarIdealConstantCurrentConductor;
            HasSelectedRotatableElement = selection?.Kind is
                MagnetostaticSelectionKind.PlanarIdealConstantCurrentConductor or
                MagnetostaticSelectionKind.VerticalCircularCurrentLoop;
            HasSelectedLoop = selection?.Kind is MagnetostaticSelectionKind.PlanarCircularCurrentLoop or
                MagnetostaticSelectionKind.VerticalCircularCurrentLoop;
            HasSelectedSecondOrigin = selection?.SecondOriginX is not null && selection.SecondOriginY is not null;
            SelectedElementName = selection is null ? T("Selection.None") : selection.Kind switch
            {
                MagnetostaticSelectionKind.PlanarIdealConstantCurrentConductor =>
                    F("Selection.PlanarConductor", selection.Index + 1),
                MagnetostaticSelectionKind.VerticalInfiniteCurrentConductor =>
                    F("Selection.VerticalConductor", selection.Index + 1),
                MagnetostaticSelectionKind.PlanarCircularCurrentLoop =>
                    F("Selection.PlanarLoop", selection.Index + 1),
                _ => F("Selection.VerticalLoop", selection.Index + 1)
            };
            SelectedElementTitle = selection?.Name ?? string.Empty;
            if (selection is null) return;
            SelectedCurrent = (decimal)selection.CurrentAmperes;
            if (selection.Length is { } length) SelectedLength = (decimal)length;
            if (selection.Radius is { } radius) SelectedRadius = (decimal)radius;
            if (selection.AngleDegrees is { } angle)
            {
                SelectedAngle = (decimal)Math.Round(angle, 2);
                SelectedStandardAngleIndex = GetStandardAngleIndex(angle);
            }
            SelectedOriginX = (decimal)selection.X;
            SelectedOriginY = (decimal)selection.Y;
            if (selection.SecondOriginX is { } secondOriginX && selection.SecondOriginY is { } secondOriginY)
            {
                SelectedSecondOriginX = (decimal)secondOriginX;
                SelectedSecondOriginY = (decimal)secondOriginY;
            }
        }
        finally { _updatingSelection = false; }
    }
    public void UpdateSimulation(MagnetostaticSimulationResult result)
    {
        var closedCount = result.FieldLines.Count(line => line.IsClosed);
        var divergingCount = result.FieldLines.Count - closedCount;
        StatusText = F("Status.MagneticSimulation", CurrentScene.Name, CurrentScene.Conductors.Length,
            CurrentScene.VerticalConductorElements.Length, CurrentScene.PlanarLoopElements.Length,
            CurrentScene.VerticalLoopElements.Length, result.Samples.Count, closedCount, divergingCount,
            result.Elapsed.TotalMilliseconds);
    }

    public void RefreshLanguage()
    {
        UpdateSelection(_selection);
        UpdateToolState(ActiveTool);
    }

    private void ApplyOrigin(decimal x, decimal y)
    {
        if (!_updatingSelection && HasSelection) SetSelectedOriginRequested?.Invoke((double)x, (double)y);
    }
    private void ApplySecondOrigin(decimal x, decimal y)
    {
        if (!_updatingSelection && HasSelectedSecondOrigin)
            SetSelectedSecondOriginRequested?.Invoke((double)x, (double)y);
    }
    private static int GetStandardAngleIndex(double degrees)
    {
        var normalized = (degrees % 360 + 360) % 360;
        for (var index = 0; index < 4; index++)
            if (Math.Abs(normalized - index * 90) <= 1e-6) return index + 1;
        return 0;
    }

    private static string T(string key) => LocalizationService.Instance.Get(key);

    private static string F(string key, params object[] values) => string.Format(T(key), values);
}
