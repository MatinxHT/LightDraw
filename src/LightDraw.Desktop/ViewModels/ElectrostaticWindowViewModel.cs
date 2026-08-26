using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightDraw.Core.Electromagnetics;
using LightDraw.Rendering.Skia.Electrostatics;

namespace LightDraw.Desktop.ViewModels;

public sealed partial class ElectrostaticWindowViewModel : ObservableObject
{
    private bool _updatingSelection;

    [ObservableProperty] private ElectrostaticScene _currentScene = ElectrostaticScene.CreateEmpty();
    [ObservableProperty] private ElectrostaticTool _activeTool = ElectrostaticTool.Pan;
    [ObservableProperty] private int _linesPerCharge = 24;
    [ObservableProperty] private bool _hasSelectedCharge;
    [ObservableProperty] private bool _hasSelectedPlate;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private string _selectedElementName = "未选择元件";
    [ObservableProperty] private decimal _selectedCharge = 1;
    [ObservableProperty] private decimal _selectedPotential;
    [ObservableProperty] private decimal _selectedPlateLength = 100;
    [ObservableProperty] private decimal _selectedPlateAngle;
    [ObservableProperty] private decimal _rotationStep = 5;
    [ObservableProperty] private int _selectedStandardAngleIndex;
    [ObservableProperty] private decimal _selectedOriginX;
    [ObservableProperty] private decimal _selectedOriginY;
    [ObservableProperty] private string _statusText = "真空静电场 · 就绪";

    public event EventHandler? ResetViewRequested;
    public event Action<double>? SetSelectedChargeRequested;
    public event Action<double>? SetSelectedPotentialRequested;
    public event Action<double>? SetSelectedPlateLengthRequested;
    public event Action<double>? SetSelectedPlateAngleRequested;
    public event Action<double, double>? SetSelectedOriginRequested;

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
        StatusText = "已重置为空白静电场";
    }

    [RelayCommand]
    private void ResetView()
    {
        ResetViewRequested?.Invoke(this, EventArgs.Empty);
        StatusText = "视图已复位";
    }

    public void UpdateToolState(ElectrostaticTool tool) => StatusText = tool switch
    {
        ElectrostaticTool.Pan => "平移工具 · 按住左键拖动画布，滚轮缩放",
        ElectrostaticTool.Move => "移动/编辑 · 仅拖动第一原点平移；平板长度和角度通过属性框修改",
        ElectrostaticTool.Delete => "删除工具 · 单击点电荷或平板后自动返回平移工具",
        ElectrostaticTool.PointCharge => "放置点电荷 · 在画布中单击，随后可在顶部设置电量",
        ElectrostaticTool.ChargedPlate => "放置带电平板 · 第一次点击起点，第二次点击终点",
        _ => StatusText
    };

    public void UpdateSelection(ElectrostaticSelection? selection)
    {
        _updatingSelection = true;
        try
        {
            HasSelection = selection is not null;
            HasSelectedCharge = selection?.Kind == ElectrostaticSelectionKind.PointCharge;
            HasSelectedPlate = selection?.Kind == ElectrostaticSelectionKind.ChargedPlate;
            SelectedElementName = selection is null ? "未选择元件" :
                selection.Kind == ElectrostaticSelectionKind.PointCharge
                    ? $"点电荷 #{selection.Index + 1}"
                    : $"带电平板 #{selection.Index + 1}";
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
        StatusText = $"{CurrentScene.Name} · {CurrentScene.Charges.Length} 个点电荷 · " +
                     $"{CurrentScene.PlateElements.Length} 块平板 · " +
                     $"{result.FieldLines.Count} 条电场线 · 计算 {result.Elapsed.TotalMilliseconds:F2} ms";

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
}
