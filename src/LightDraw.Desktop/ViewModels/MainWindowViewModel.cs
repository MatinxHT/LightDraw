using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightDraw.Core.Scene;
using LightDraw.Core.Simulation;
using LightDraw.Desktop.Services;
using LightDraw.Rendering.Skia;

namespace LightDraw.Desktop.ViewModels;

public sealed partial class MainWindowViewModel(ISceneStorageService sceneStorage) : ObservableObject
{
    private CancellationTokenSource? _rayDensityUpdate;
    private bool _updatingSelection;

    [ObservableProperty]
    private OpticalScene _currentScene = OpticalScene.CreateEmpty();

    [ObservableProperty]
    private int _rayDensity = 160;

    [ObservableProperty]
    private int _appliedRaysPerSource = 160;

    [ObservableProperty]
    private CanvasTool _activeTool = CanvasTool.Pan;

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private bool _hasSelectedElement;

    [ObservableProperty]
    private bool _canRotateSelectedElement;

    [ObservableProperty]
    private bool _hasSelectedLens;

    [ObservableProperty]
    private bool _hasSelectedLength;

    [ObservableProperty]
    private string _selectedElementName = "未选择元件";

    [ObservableProperty]
    private string _selectedAngleText = "当前角度 --";

    [ObservableProperty]
    private decimal _selectedFocalLength = 100;

    [ObservableProperty]
    private decimal _selectedLength = 100;

    [ObservableProperty]
    private decimal _rotationStep = 15;

    [ObservableProperty]
    private decimal _selectedOriginX;

    [ObservableProperty]
    private decimal _selectedOriginY;

    [ObservableProperty]
    private int _selectedStandardAngleIndex;

    public event EventHandler? ResetViewRequested;
    public event EventHandler? AboutRequested;
    public event Action<double>? RotateSelectedRequested;
    public event Action<double>? SetSelectedAngleRequested;
    public event Action<double>? SetSelectedFocalLengthRequested;
    public event Action<double>? SetSelectedLengthRequested;
    public event Action<double, double>? SetSelectedOriginRequested;

    partial void OnRayDensityChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 1000);
        if (clamped != value)
        {
            RayDensity = clamped;
            return;
        }

        _rayDensityUpdate?.Cancel();
        _rayDensityUpdate?.Dispose();
        _rayDensityUpdate = new CancellationTokenSource();
        _ = ApplyRayDensityAsync(clamped, _rayDensityUpdate.Token);
    }

    partial void OnSelectedFocalLengthChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 1, 10000);
        if (clamped != value)
        {
            SelectedFocalLength = clamped;
            return;
        }

        if (!_updatingSelection && HasSelectedLens)
        {
            SetSelectedFocalLengthRequested?.Invoke((double)clamped);
        }
    }

    partial void OnSelectedLengthChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 1, 10000);
        if (clamped != value)
        {
            SelectedLength = clamped;
            return;
        }

        if (!_updatingSelection && HasSelectedLength)
        {
            SetSelectedLengthRequested?.Invoke((double)clamped);
        }
    }

    partial void OnRotationStepChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0.1m, 360);
        if (clamped != value)
        {
            RotationStep = clamped;
        }
    }

    partial void OnSelectedOriginXChanged(decimal value) => ApplySelectedOrigin(value, SelectedOriginY);

    partial void OnSelectedOriginYChanged(decimal value) => ApplySelectedOrigin(SelectedOriginX, value);

    partial void OnSelectedStandardAngleIndexChanged(int value)
    {
        if (_updatingSelection || value <= 0 || !CanRotateSelectedElement)
        {
            return;
        }

        var standardAngles = new[] { 0d, 90d, 180d, 270d };
        var angleIndex = value - 1;
        if (angleIndex < standardAngles.Length)
        {
            SetSelectedAngleRequested?.Invoke(standardAngles[angleIndex]);
        }
    }

    [RelayCommand]
    private void SelectTool(string? value)
    {
        if (Enum.TryParse<CanvasTool>(value, out var tool))
        {
            ActiveTool = tool;
            UpdateToolState(tool, false);
        }
    }

    [RelayCommand]
    private void RotateSelected(string? direction)
    {
        if (!CanRotateSelectedElement)
        {
            return;
        }

        var amount = (double)RotationStep;
        RotateSelectedRequested?.Invoke(direction == "Counterclockwise" ? -amount : amount);
    }

    [RelayCommand]
    private void ShowAbout() => AboutRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ResetScene()
    {
        CancelRayDensityUpdate();
        RayDensity = 160;
        AppliedRaysPerSource = 160;
        CurrentScene = OpticalScene.CreateEmpty();
        ActiveTool = CanvasTool.Pan;
        ResetViewRequested?.Invoke(this, EventArgs.Empty);
        StatusText = "已重置为空白场景";
    }

    [RelayCommand]
    private void ResetView()
    {
        ResetViewRequested?.Invoke(this, EventArgs.Empty);
        StatusText = "视图已复位";
    }

    [RelayCommand]
    private async Task OpenSceneAsync(CancellationToken cancellationToken)
    {
        try
        {
            var opened = await sceneStorage.OpenAsync(cancellationToken);
            if (opened is null)
            {
                return;
            }

            CurrentScene = opened.Scene;
            ActiveTool = CanvasTool.Pan;
            StatusText = $"已打开：{opened.FileName}";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            StatusText = $"打开失败：{exception.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveSceneAsync(CancellationToken cancellationToken)
    {
        try
        {
            var fileName = await sceneStorage.SaveAsync(CurrentScene, cancellationToken);
            if (fileName is not null)
            {
                StatusText = $"已保存：{fileName}";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            StatusText = $"保存失败：{exception.Message}";
        }
    }

    public void UpdateSimulation(SimulationResult result) =>
        StatusText = $"{CurrentScene.Name} · 每光源 {AppliedRaysPerSource} 条 / 共 {result.InitialRayCount} 条 · " +
                     $"{result.Segments.Count} 个线段 · 计算 {result.Elapsed.TotalMilliseconds:F2} ms";

    public void UpdateToolState(CanvasTool tool, bool isPlacing)
    {
        ActiveTool = tool;
        StatusText = tool switch
        {
            CanvasTool.Pan => "平移工具 · 按住左键拖动画布",
            CanvasTool.Move => "移动或调整元件 · 拖动主体平移；拖动端点可拉伸和旋转；光路实时刷新",
            CanvasTool.Delete => "删除元件 · 单击光源、镜面、光屏或透镜即可删除，随后自动返回平移工具",
            _ => PlacementStatus(tool, isPlacing)
        };
    }

    public void UpdateSelection(CanvasSelection? selection)
    {
        _updatingSelection = true;
        try
        {
            HasSelectedElement = selection is not null;
            CanRotateSelectedElement = selection?.CanRotate == true;
            HasSelectedLens = selection?.FocalLength is not null;
            HasSelectedLength = selection?.Length is not null;
            SelectedElementName = selection?.DisplayName ?? "未选择元件";
            SelectedAngleText = selection?.CanRotate == true
                ? $"当前角度 {selection.AngleDegrees:F1}°"
                : "当前角度 --";
            SelectedStandardAngleIndex = selection?.CanRotate == true
                ? GetStandardAngleIndex(selection.AngleDegrees)
                : 0;
            if (selection is not null)
            {
                SelectedOriginX = (decimal)Math.Round(selection.OriginX, 2);
                SelectedOriginY = (decimal)Math.Round(selection.OriginY, 2);
            }
            if (selection?.FocalLength is { } focalLength)
            {
                SelectedFocalLength = (decimal)focalLength;
            }
            if (selection?.Length is { } length)
            {
                SelectedLength = (decimal)Math.Round(length, 2);
            }
        }
        finally
        {
            _updatingSelection = false;
        }
    }

    private void ApplySelectedOrigin(decimal x, decimal y)
    {
        if (!_updatingSelection && HasSelectedElement)
        {
            SetSelectedOriginRequested?.Invoke((double)x, (double)y);
        }
    }

    private static int GetStandardAngleIndex(double angleDegrees)
    {
        var normalized = angleDegrees % 360;
        if (normalized < 0)
        {
            normalized += 360;
        }

        var standardAngles = new[] { 0d, 90d, 180d, 270d };
        for (var index = 0; index < standardAngles.Length; index++)
        {
            if (Math.Abs(normalized - standardAngles[index]) <= 1e-6)
            {
                return index + 1;
            }
        }

        return 0;
    }

    private async Task ApplyRayDensityAsync(int count, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(120, cancellationToken);
            AppliedRaysPerSource = count;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelRayDensityUpdate()
    {
        _rayDensityUpdate?.Cancel();
        _rayDensityUpdate?.Dispose();
        _rayDensityUpdate = null;
    }

    private static string PlacementStatus(CanvasTool tool, bool isPlacing)
    {
        var name = tool switch
        {
            CanvasTool.PointLight => "点光源（360° 发光，单击放置）",
            CanvasTool.ParallelLight => "线平行光源（垂直于绘制线发射）",
            CanvasTool.Mirror => "平面反光镜",
            CanvasTool.Screen => "光屏",
            CanvasTool.ConvexLens => "凸透镜",
            CanvasTool.ConcaveLens => "凹透镜",
            _ => "物件"
        };
        return isPlacing ? $"正在绘制{name} · 单击确定终点" : $"{name} · 单击确定起点";
    }
}
