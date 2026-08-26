using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using LightDraw.Core.Geometry;
using LightDraw.Core.Scene;
using LightDraw.Core.Simulation;

namespace LightDraw.Rendering.Skia.Optics;

public enum CanvasTool
{
    Pan, Move, Delete, PointLight, ParallelLight, Mirror,
    ConcaveSphericalMirror, ConvexSphericalMirror, BeamSplitter,
    Screen, Aperture, ReflectionGrating, ConvexLens, ConcaveLens
}

public enum CanvasSelectionKind
{
    PointLight, ParallelLight, Mirror, ConcaveSphericalMirror,
    ConvexSphericalMirror, BeamSplitter, Screen, Aperture,
    ReflectionGrating, ConvexLens, ConcaveLens
}

public sealed record CanvasSelection(
    CanvasSelectionKind Kind, string DisplayName, bool CanRotate,
    double OriginX, double OriginY, double AngleDegrees,
    double? FocalLength, double? Length,
    double? ApertureOpening = null, double? GrooveDensity = null,
    double? WavelengthNanometers = null, double? Radius = null,
    double? ArcAngleDegrees = null, double? SecondOriginX = null,
    double? SecondOriginY = null, double? EmissionAngleDegrees = null);

internal enum SceneItemKind
{
    None, LightSource, Mirror, ConcaveSphericalMirror, ConvexSphericalMirror,
    BeamSplitter, Screen, Aperture, ReflectionGrating, Lens
}

internal enum MoveDragMode
{
    None, Translate, DirectionHandle, RotationHandle
}

public sealed class OpticalCanvas : Control
{
    public static readonly DirectProperty<OpticalCanvas, OpticalScene> SceneProperty =
        AvaloniaProperty.RegisterDirect<OpticalCanvas, OpticalScene>(
            nameof(Scene), canvas => canvas.Scene, (canvas, value) => canvas.SetScene(value),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<OpticalCanvas, int> RaysPerSourceProperty =
        AvaloniaProperty.RegisterDirect<OpticalCanvas, int>(
            nameof(RaysPerSource), canvas => canvas.RaysPerSource,
            (canvas, value) => canvas.SetRaysPerSource(value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<OpticalCanvas, CanvasTool> ActiveToolProperty =
        AvaloniaProperty.RegisterDirect<OpticalCanvas, CanvasTool>(
            nameof(ActiveTool), canvas => canvas.ActiveTool,
            (canvas, value) => canvas.SelectTool(value), defaultBindingMode: BindingMode.TwoWay);

    private readonly RayTracer _rayTracer = new();
    private readonly SceneEditor _editor = new();
    private OpticalScene _scene = OpticalScene.CreateEmpty();
    private SimulationResult _result = new([], 0, 0, 0, 0, TimeSpan.Zero);
    private Vector2D _pan = new(520, 360);
    private double _zoom = 1;
    private bool _isPanning;
    private Point _lastPointer;
    private int _raysPerSource = 160;
    private CanvasTool _tool = CanvasTool.Pan;
    private Vector2D? _placementStart;
    private Vector2D? _placementPreview;

    public OpticalCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        _editor.SceneUpdated += OnEditorSceneUpdated;
        _editor.SceneCommitted += OnEditorSceneCommitted;
        _editor.PreviewRequested += OnEditorPreviewRequested;
        _editor.SelectionChanged += OnEditorSelectionChanged;
        _editor.InteractionStateChanged += OnEditorInteractionStateChanged;
        _editor.SetScene(_scene);
        Recalculate();
    }

    public OpticalScene Scene { get => _scene; set => SetScene(value); }
    public SimulationResult SimulationResult => _result;
    public int RaysPerSource { get => _raysPerSource; set => SetRaysPerSource(value); }
    public CanvasTool ActiveTool { get => _tool; set => SelectTool(value); }
    public bool IsPlacing => _placementStart is not null;
    public CanvasSelection? Selection => _editor.Selection;

    public event EventHandler? SceneChanged;
    public event EventHandler? SimulationCompleted;
    public event EventHandler? ToolStateChanged;
    public event EventHandler? SelectionChanged;

    public void SetScene(OpticalScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (ReferenceEquals(_scene, scene)) return;
        _editor.SetScene(scene);
        SetAndRaise(SceneProperty, ref _scene, _editor.Scene);
        SetAndRaise(ActiveToolProperty, ref _tool, CanvasTool.Pan);
        CancelPlacement(false);
        Recalculate();
        SceneChanged?.Invoke(this, EventArgs.Empty);
        ToolStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetRaysPerSource(int value)
    {
        var clamped = Math.Clamp(value, 1, 2000);
        if (clamped == _raysPerSource) return;
        SetAndRaise(RaysPerSourceProperty, ref _raysPerSource, clamped);
        Recalculate();
    }

    public void SelectTool(CanvasTool tool)
    {
        SetAndRaise(ActiveToolProperty, ref _tool, tool);
        if (tool != CanvasTool.Move) _editor.ClearSelection();
        CancelPlacement(false);
        ToolStateChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    public void CancelPlacement(bool notify = true)
    {
        _placementStart = null;
        _placementPreview = null;
        if (notify) ToolStateChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    public void ResetView()
    {
        _zoom = 1;
        _editor.SetZoom(_zoom);
        _pan = new Vector2D(Math.Max(420, Bounds.Width * 0.48), Math.Max(300, Bounds.Height * 0.52));
        InvalidateVisual();
    }

    public void RotateSelectedBy(double degrees) => _editor.RotateSelectedBy(degrees);
    public void SetSelectedAngle(double degrees) => _editor.SetSelectedAngle(degrees);
    public void SetSelectedFocalLength(double value) => _editor.SetSelectedFocalLength(value);
    public void SetSelectedSphericalMirrorRadius(double value) => _editor.SetSelectedSphericalMirrorRadius(value);
    public void SetSelectedSphericalMirrorArcAngle(double value) => _editor.SetSelectedSphericalMirrorArcAngle(value);
    public void SetSelectedPointLightEmissionAngle(double value) => _editor.SetSelectedPointLightEmissionAngle(value);
    public void SetSelectedCentralAngle(double value) => _editor.SetSelectedCentralAngle(value);
    public void SetSelectedApertureOpening(double value) => _editor.SetSelectedApertureOpening(value);
    public void SetSelectedGrooveDensity(double value) => _editor.SetSelectedGrooveDensity(value);
    public void SetSelectedWavelength(double value) => _editor.SetSelectedWavelength(value);
    public void SetSelectedLength(double value) => _editor.SetSelectedLength(value);
    public void SetSelectedOrigin(double x, double y) => _editor.SetSelectedOrigin(x, y);
    public void SetSelectedSecondOrigin(double x, double y) => _editor.SetSelectedSecondOrigin(x, y);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.Custom(new SkiaSceneDrawOperation(new Rect(Bounds.Size), _scene, _result, _pan, _zoom,
            _raysPerSource, _tool, _placementStart, _placementPreview,
            _editor.SelectedKind, _editor.SelectedIndex));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var pointerPosition = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsRightButtonPressed || properties.IsMiddleButtonPressed ||
            (_tool == CanvasTool.Pan && properties.IsLeftButtonPressed))
        {
            _isPanning = true;
            _lastPointer = pointerPosition;
            e.Pointer.Capture(this);
        }
        else if (_tool == CanvasTool.Move && properties.IsLeftButtonPressed)
        {
            _editor.SetZoom(_zoom);
            if (_editor.TryBeginMove(ScreenToWorld(pointerPosition))) e.Pointer.Capture(this);
        }
        else if (_tool == CanvasTool.Delete && properties.IsLeftButtonPressed)
        {
            var previousScene = _scene;
            _editor.SetZoom(_zoom);
            _editor.DeleteItemAt(ScreenToWorld(pointerPosition));
            if (!ReferenceEquals(previousScene, _scene))
            {
                SetAndRaise(ActiveToolProperty, ref _tool, CanvasTool.Pan);
                ToolStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        else if (properties.IsLeftButtonPressed)
        {
            var world = ScreenToWorld(pointerPosition);
            if (_tool == CanvasTool.PointLight)
            {
                _editor.AddPointLight(world);
                FinishOneShotPlacement();
            }
            else if (_placementStart is null)
            {
                _placementStart = world;
                _placementPreview = world;
                ToolStateChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _editor.SetZoom(_zoom);
                if (_editor.AddElement(_tool, _placementStart.Value, world)) FinishOneShotPlacement();
            }
        }
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var current = e.GetPosition(this);
        if (_isPanning)
        {
            _pan += new Vector2D(current.X - _lastPointer.X, current.Y - _lastPointer.Y);
            _lastPointer = current;
        }
        else if (_editor.IsMoving) _editor.MoveSelectedItem(ScreenToWorld(current));
        else if (_placementStart is not null) _placementPreview = ScreenToWorld(current);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
        }
        else if (_editor.IsMoving)
        {
            _editor.EndMove();
            e.Pointer.Capture(null);
        }
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var pointer = e.GetPosition(this);
        var before = ScreenToWorld(pointer);
        _zoom = Math.Clamp(_zoom * Math.Pow(1.12, e.Delta.Y), 0.15, 8);
        _editor.SetZoom(_zoom);
        _pan = new Vector2D(pointer.X - before.X * _zoom, pointer.Y - before.Y * _zoom);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && _tool != CanvasTool.Pan)
        {
            SelectTool(CanvasTool.Pan);
            e.Handled = true;
        }
    }

    private void FinishOneShotPlacement()
    {
        _placementStart = null;
        _placementPreview = null;
        SetAndRaise(ActiveToolProperty, ref _tool, CanvasTool.Pan);
        ToolStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private Vector2D ScreenToWorld(Point point) =>
        new((point.X - _pan.X) / _zoom, (point.Y - _pan.Y) / _zoom);

    private void Recalculate()
    {
        _result = _rayTracer.Trace(_scene, new SimulationOptions(_raysPerSource));
        SimulationCompleted?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    private void OnEditorSceneUpdated(object? sender, EventArgs e)
    {
        SetAndRaise(SceneProperty, ref _scene, _editor.Scene);
        InvalidateVisual();
    }

    private void OnEditorSceneCommitted(object? sender, EventArgs e) => SceneChanged?.Invoke(this, EventArgs.Empty);
    private void OnEditorPreviewRequested(object? sender, EventArgs e) => Recalculate();
    private void OnEditorSelectionChanged(object? sender, EventArgs e)
    {
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }
    private void OnEditorInteractionStateChanged(object? sender, EventArgs e) =>
        ToolStateChanged?.Invoke(this, EventArgs.Empty);
}
