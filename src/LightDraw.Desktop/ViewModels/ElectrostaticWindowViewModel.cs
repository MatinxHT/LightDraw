using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightDraw.Core.Electromagnetics;
using LightDraw.Desktop.Services;
using LightDraw.Rendering.Skia.Electrostatics;

namespace LightDraw.Desktop.ViewModels;

public sealed partial class ElectrostaticWindowViewModel : ObservableObject
{
    private bool _updatingSelection;
    private ElectrostaticSelection? _selection;

    [ObservableProperty] private ElectrostaticScene _currentScene = ElectrostaticScene.CreateEmpty();
    [ObservableProperty] private ElectrostaticTool _activeTool = ElectrostaticTool.Pan;
    [ObservableProperty] private int _linesPerCharge = 24;
    [ObservableProperty] private bool _hasSelectedCharge;
    [ObservableProperty] private bool _hasSelectedPlate;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private string _selectedElementName = T("Selection.None");
    [ObservableProperty] private string _selectedElementTitle = string.Empty;
    [ObservableProperty] private decimal _selectedCharge = 1;
    [ObservableProperty] private decimal _selectedPotential;
    [ObservableProperty] private decimal _selectedPlateLength = 100;
    [ObservableProperty] private decimal _selectedPlateAngle;
    [ObservableProperty] private decimal _rotationStep = 5;
    [ObservableProperty] private int _selectedStandardAngleIndex;
    [ObservableProperty] private decimal _selectedOriginX;
    [ObservableProperty] private decimal _selectedOriginY;
    [ObservableProperty] private string _statusText = T("Status.Ready");

    public event EventHandler? ResetViewRequested;
    public event Action<double>? SetSelectedChargeRequested;
    public event Action<double>? SetSelectedPotentialRequested;
    public event Action<double>? SetSelectedPlateLengthRequested;
    public event Action<double>? SetSelectedPlateAngleRequested;
    public event Action<double, double>? SetSelectedOriginRequested;
    public event Action<string>? SetSelectedNameRequested;

    partial void OnLinesPerChargeChanged(int value)
    {
        var clamped = Math.Clamp(value, 8, 96);
        if (clamped != value) LinesPerCharge = clamped;
    }

    partial void OnSelectedChargeChanged(decimal value)
    {
        var clamped = Math.Clamp(value, -1_000_000, 1_000_000);
        if (clamped != value)
        {
            SelectedCharge = clamped;
            return;
        }
        if (!_updatingSelection && HasSelectedCharge) SetSelectedChargeRequested?.Invoke((double)clamped);
    }

    partial void OnSelectedPotentialChanged(decimal value)
    {
        var clamped = Math.Clamp(value, -10_000_000, 10_000_000);
        if (clamped != value) { SelectedPotential = clamped; return; }
        if (!_updatingSelection && HasSelectedPlate) SetSelectedPotentialRequested?.Invoke((double)clamped);
    }

    partial void OnSelectedPlateLengthChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 10, 100_000);
        if (clamped != value) { SelectedPlateLength = clamped; return; }
        if (!_updatingSelection && HasSelectedPlate) SetSelectedPlateLengthRequested?.Invoke((double)clamped);
    }

    partial void OnSelectedPlateAngleChanged(decimal value)
    {
        var clamped = Math.Clamp(value, -360_000, 360_000);
        if (clamped != value) { SelectedPlateAngle = clamped; return; }
        if (!_updatingSelection && HasSelectedPlate) SetSelectedPlateAngleRequested?.Invoke((double)clamped);
    }

    partial void OnRotationStepChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0.1m, 360);
        if (clamped != value) RotationStep = clamped;
    }

    partial void OnSelectedStandardAngleIndexChanged(int value)
    {
        if (_updatingSelection || !HasSelectedPlate || value is < 1 or > 4) return;
        SelectedPlateAngle = (value - 1) * 90;
    }

    [RelayCommand]
    private void RotateSelected(string? direction)
    {
        if (!HasSelectedPlate) return;
        SelectedPlateAngle += direction == "Counterclockwise" ? RotationStep : -RotationStep;
    }

    partial void OnSelectedOriginXChanged(decimal value) => ApplyOrigin(value, SelectedOriginY);
    partial void OnSelectedOriginYChanged(decimal value) => ApplyOrigin(SelectedOriginX, value);
    partial void OnSelectedElementTitleChanged(string value)
    {
        if (!_updatingSelection && HasSelection && !string.IsNullOrWhiteSpace(value) && value.Length <= 120)
            SetSelectedNameRequested?.Invoke(value);
    }

    [RelayCommand]
    private void SelectTool(string? value)
    {
        if (!Enum.TryParse<ElectrostaticTool>(value, out var tool)) return;
        ActiveTool = tool;
        UpdateToolState(tool);
    }

    [RelayCommand]
    private void ResetScene()
    {
        CurrentScene = ElectrostaticScene.CreateEmpty();
        ActiveTool = ElectrostaticTool.Pan;
        UpdateSelection(null);
        StatusText = T("Status.ElectroReset");
    }

    [RelayCommand]
    private void ResetView()
    {
        ResetViewRequested?.Invoke(this, EventArgs.Empty);
        StatusText = T("Status.ViewReset");
    }

    public void UpdateToolState(ElectrostaticTool tool) => StatusText = tool switch
    {
        ElectrostaticTool.Pan => T("Status.PanZoom"),
        ElectrostaticTool.Move => T("Status.ElectroMove"),
        ElectrostaticTool.Delete => T("Status.ElectroDelete"),
        ElectrostaticTool.PointCharge => T("Status.PlaceCharge"),
        ElectrostaticTool.ChargedPlate => T("Status.PlacePlate"),
        _ => StatusText
    };

    public void UpdateSelection(ElectrostaticSelection? selection)
    {
        _selection = selection;
        _updatingSelection = true;
        try
        {
            HasSelection = selection is not null;
            HasSelectedCharge = selection?.Kind == ElectrostaticSelectionKind.PointCharge;
            HasSelectedPlate = selection?.Kind == ElectrostaticSelectionKind.ChargedPlate;
            SelectedElementName = selection is null ? T("Selection.None") :
                selection.Kind == ElectrostaticSelectionKind.PointCharge
                    ? F("Selection.PointCharge", selection.Index + 1)
                    : F("Selection.ChargedPlate", selection.Index + 1);
            SelectedElementTitle = selection?.Name ?? string.Empty;
            if (selection is not null)
            {
                if (selection.ChargeNanocoulombs is { } charge) SelectedCharge = (decimal)charge;
                if (selection.PotentialVolts is { } potential) SelectedPotential = (decimal)potential;
                if (selection.Length is { } length) SelectedPlateLength = (decimal)length;
                if (selection.AngleDegrees is { } angle)
                {
                    SelectedPlateAngle = (decimal)Math.Round(angle, 2);
                    SelectedStandardAngleIndex = GetStandardAngleIndex(angle);
                }
                SelectedOriginX = (decimal)selection.X;
                SelectedOriginY = (decimal)selection.Y;
            }
        }
        finally
        {
            _updatingSelection = false;
        }
    }

    public void UpdateSimulation(ElectrostaticSimulationResult result) =>
        StatusText = F("Status.ElectroSimulation", CurrentScene.Name, CurrentScene.Charges.Length,
            CurrentScene.PlateElements.Length, result.FieldLines.Count, result.Elapsed.TotalMilliseconds);

    public void RefreshLanguage()
    {
        UpdateSelection(_selection);
        UpdateToolState(ActiveTool);
    }

    private void ApplyOrigin(decimal x, decimal y)
    {
        if (!_updatingSelection && HasSelection) SetSelectedOriginRequested?.Invoke((double)x, (double)y);
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
