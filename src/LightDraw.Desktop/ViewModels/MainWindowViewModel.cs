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

    [ObservableProperty]
    private OpticalScene _currentScene = OpticalScene.CreateDemo();

    [ObservableProperty]
    private int _rayDensity = 160;

    [ObservableProperty]
    private int _appliedRaysPerSource = 160;

    [ObservableProperty]
    private CanvasTool _activeTool = CanvasTool.Pan;

    [ObservableProperty]
    private string _statusText = "就绪";

    public event EventHandler? ResetViewRequested;

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
    private void ResetScene()
    {
        CancelRayDensityUpdate();
        RayDensity = 160;
        AppliedRaysPerSource = 160;
        CurrentScene = OpticalScene.CreateDemo();
        ActiveTool = CanvasTool.Pan;
        ResetViewRequested?.Invoke(this, EventArgs.Empty);
        StatusText = "已恢复内置演示场景";
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
            CanvasTool.Delete => "删除元件 · 单击光源、镜面或透镜即可删除，随后自动返回平移工具",
            _ => PlacementStatus(tool, isPlacing)
        };
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
            CanvasTool.ConvexLens => "凸透镜",
            CanvasTool.ConcaveLens => "凹透镜",
            _ => "物件"
        };
        return isPlacing ? $"正在绘制{name} · 单击确定终点" : $"{name} · 单击确定起点";
    }
}
