using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using LightDraw.Core.Electromagnetics;
using LightDraw.Core.Geometry;

namespace LightDraw.Rendering.Skia.Magnetostatics;

public enum MagnetostaticTool
{
    Pan,
    Move,
    Delete,
    PlanarIdealConstantCurrentConductor,
    VerticalInfiniteCurrentConductor,
    PlanarCircularCurrentLoop,
    VerticalCircularCurrentLoop
}
public enum MagnetostaticSelectionKind
{
    PlanarIdealConstantCurrentConductor,
    VerticalInfiniteCurrentConductor,
    PlanarCircularCurrentLoop,
    VerticalCircularCurrentLoop
}
public sealed record MagnetostaticSelection(
    MagnetostaticSelectionKind Kind,
    int Index,
    double X,
    double Y,
    double CurrentAmperes,
    double? Length = null,
    double? AngleDegrees = null,
    double? Radius = null,
    double? SecondOriginX = null,
    double? SecondOriginY = null);

public sealed class MagnetostaticCanvas : Control
{
    private const double RotationHandleOffset = 100;
    public static readonly DirectProperty<MagnetostaticCanvas, MagnetostaticScene> SceneProperty =
        AvaloniaProperty.RegisterDirect<MagnetostaticCanvas, MagnetostaticScene>(nameof(Scene), c => c.Scene,
            (c, v) => c.SetScene(v), defaultBindingMode: BindingMode.TwoWay);
    public static readonly DirectProperty<MagnetostaticCanvas, MagnetostaticTool> ActiveToolProperty =
        AvaloniaProperty.RegisterDirect<MagnetostaticCanvas, MagnetostaticTool>(nameof(ActiveTool), c => c.ActiveTool,
            (c, v) => c.SelectTool(v), defaultBindingMode: BindingMode.TwoWay);
    public static readonly DirectProperty<MagnetostaticCanvas, int> MarkerDensityProperty =
        AvaloniaProperty.RegisterDirect<MagnetostaticCanvas, int>(nameof(MarkerDensity), c => c.MarkerDensity,
            (c, v) => c.SetMarkerDensity(v));

    private static readonly IBrush BackgroundBrush = Brush("#08111F");
    private static readonly IBrush TextBrush = Brush("#B8C9E2");
    private static readonly Pen MinorGridPen = Pen("#15243A", 1);
    private static readonly Pen MajorGridPen = Pen("#233955", 1);
    private static readonly Pen AxisPen = Pen("#3C5878", 1.2);
    private static readonly Pen TickPen = Pen("#96AAC9", 1);
    private static readonly IBrush FieldBrush = Brush("#67E8C7");
    private static readonly Pen FieldPen = Pen("#67E8C7", 1.4);
    private static readonly Pen PositiveCurrentPen = Pen("#FF8A65", 6);
    private static readonly Pen NegativeCurrentPen = Pen("#7AA7FF", 6);
    private static readonly Pen ZeroCurrentPen = Pen("#8493A8", 6);
    private static readonly IBrush PositiveCurrentBrush = Brush("#FF8A65");
    private static readonly IBrush NegativeCurrentBrush = Brush("#7AA7FF");
    private static readonly IBrush ZeroCurrentBrush = Brush("#8493A8");
    private static readonly Pen SelectionPen = Pen("#FFFFFF", 2);
    private static readonly Pen PreviewPen = new(Brush("#A7F3D0"), 2, DashStyle.Dash);

    private readonly MagnetostaticSimulator _simulator = new();
    private MagnetostaticScene _scene = MagnetostaticScene.CreateEmpty();
    private MagnetostaticSimulationResult _result = new([], [], TimeSpan.Zero);
    private MagnetostaticTool _activeTool = MagnetostaticTool.Pan;
    private int _markerDensity = 16;
    private Vector2D _pan = new(560, 360);
    private double _zoom = 1;
    private bool _isPanning, _isMoving, _moveChanged, _moveSimulationDirty;
    private long _lastMoveSimulationTimestamp;
    private Point _lastPointer;
    private MagnetostaticSelectionKind? _selectedKind;
    private int _selectedIndex = -1;
    private Vector2D _dragStart, _moveOffset;
    private PlanarIdealConstantCurrentConductor? _dragOriginal;
    private VerticalInfiniteCurrentConductor? _dragOriginalVertical;
    private PlanarCircularCurrentLoop? _dragOriginalPlanarLoop;
    private VerticalCircularCurrentLoop? _dragOriginalVerticalLoop;
    private Vector2D? _conductorStart;
    private Vector2D _conductorPreviewEnd;
    private Vector2D? _loopCenter;
    private double _loopPreviewRadius;
    private DragMode _dragMode;

    public MagnetostaticCanvas() { ClipToBounds = true; Focusable = true; Recalculate(); }
    public MagnetostaticScene Scene { get => _scene; set => SetScene(value); }
    public MagnetostaticTool ActiveTool { get => _activeTool; set => SelectTool(value); }
    public int MarkerDensity { get => _markerDensity; set => SetMarkerDensity(value); }
    public MagnetostaticSimulationResult SimulationResult => _result;
    public MagnetostaticSelection? Selection
    {
        get
        {
            if (_selectedKind == MagnetostaticSelectionKind.PlanarIdealConstantCurrentConductor &&
                IsIndex(_selectedIndex, _scene.Conductors))
            {
                var conductor = _scene.Conductors[_selectedIndex];
                var center = (conductor.Start + conductor.End) / 2;
                return new(_selectedKind.Value, _selectedIndex, center.X, center.Y, conductor.CurrentAmperes,
                    (conductor.End - conductor.Start).Length,
                    NormalizeDegrees(Math.Atan2(conductor.End.Y - conductor.Start.Y,
                        conductor.End.X - conductor.Start.X) * 180 / Math.PI));
            }
            if (_selectedKind == MagnetostaticSelectionKind.VerticalInfiniteCurrentConductor &&
                IsIndex(_selectedIndex, _scene.VerticalConductorElements))
            {
                var conductor = _scene.VerticalConductorElements[_selectedIndex];
                return new(_selectedKind.Value, _selectedIndex, conductor.Position.X, conductor.Position.Y,
                    conductor.CurrentAmperes);
            }
            if (_selectedKind == MagnetostaticSelectionKind.PlanarCircularCurrentLoop &&
                IsIndex(_selectedIndex, _scene.PlanarLoopElements))
            {
                var loop = _scene.PlanarLoopElements[_selectedIndex];
                return new(_selectedKind.Value, _selectedIndex, loop.Center.X, loop.Center.Y,
                    loop.CurrentAmperes, Radius: loop.Radius);
            }
            if (_selectedKind == MagnetostaticSelectionKind.VerticalCircularCurrentLoop &&
                IsIndex(_selectedIndex, _scene.VerticalLoopElements))
            {
                var loop = _scene.VerticalLoopElements[_selectedIndex];
                var secondOrigin = VerticalLoopRotationHandle(loop);
                return new(_selectedKind.Value, _selectedIndex, loop.Center.X, loop.Center.Y,
                    loop.CurrentAmperes, AngleDegrees: NormalizeDegrees(loop.AngleDegrees), Radius: loop.Radius,
                    SecondOriginX: secondOrigin.X, SecondOriginY: secondOrigin.Y);
            }
            return null;
        }
    }

    public event EventHandler? SceneChanged;
    public event EventHandler? SimulationCompleted;
    public event EventHandler? ToolStateChanged;
    public event EventHandler? SelectionChanged;

    public void SetScene(MagnetostaticScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (ReferenceEquals(scene, _scene)) return;
        SetAndRaise(SceneProperty, ref _scene, scene); SetSelection(null, -1); Recalculate();
    }
    public void SelectTool(MagnetostaticTool tool)
    {
        SetAndRaise(ActiveToolProperty, ref _activeTool, tool);
        if (tool != MagnetostaticTool.Move) SetSelection(null, -1);
        if (tool != MagnetostaticTool.PlanarIdealConstantCurrentConductor) _conductorStart = null;
        if (tool is not (MagnetostaticTool.PlanarCircularCurrentLoop or MagnetostaticTool.VerticalCircularCurrentLoop))
            _loopCenter = null;
        ToolStateChanged?.Invoke(this, EventArgs.Empty); InvalidateVisual();
    }
    public void SetMarkerDensity(int value)
    {
        value = Math.Clamp(value, 4, 48); if (value == _markerDensity) return;
        SetAndRaise(MarkerDensityProperty, ref _markerDensity, value); Recalculate();
    }
    public void ResetView()
    {
        _zoom = 1; _pan = new(Math.Max(420, Bounds.Width * .5), Math.Max(300, Bounds.Height * .52)); InvalidateVisual();
    }
    public void SetSelectedCurrent(double value)
    {
        if (Selection is not { } selection || !double.IsFinite(value)) return;
        var current = Math.Clamp(value, -1e6, 1e6);
        if (selection.Kind == MagnetostaticSelectionKind.PlanarIdealConstantCurrentConductor)
        {
            var items = _scene.Conductors.ToArray();
            items[selection.Index] = items[selection.Index] with { CurrentAmperes = current };
            CommitScene(_scene with { Conductors = items });
        }
        else if (selection.Kind == MagnetostaticSelectionKind.VerticalInfiniteCurrentConductor)
        {
            var items = _scene.VerticalConductorElements.ToArray();
            items[selection.Index] = items[selection.Index] with { CurrentAmperes = current };
            CommitScene(_scene with { VerticalConductors = items });
        }
        else if (selection.Kind == MagnetostaticSelectionKind.PlanarCircularCurrentLoop)
        {
            var items = _scene.PlanarLoopElements.ToArray();
            items[selection.Index] = items[selection.Index] with { CurrentAmperes = current };
            CommitScene(_scene with { PlanarLoops = items });
        }
        else
        {
            var items = _scene.VerticalLoopElements.ToArray();
            items[selection.Index] = items[selection.Index] with { CurrentAmperes = current };
            CommitScene(_scene with { VerticalLoops = items });
        }
    }
    public void SetSelectedLength(double value)
    {
        if (Selection is not { Kind: MagnetostaticSelectionKind.PlanarIdealConstantCurrentConductor } selection ||
            !double.IsFinite(value)) return;
        var items = _scene.Conductors.ToArray(); var conductor = items[selection.Index];
        var direction = (conductor.End - conductor.Start).Normalized();
        if (direction.LengthSquared < 1e-12) direction = new Vector2D(1, 0);
        var center = (conductor.Start + conductor.End) / 2;
        var half = direction * (Math.Clamp(value, 10, 100_000) / 2);
        items[selection.Index] = conductor with { Start = center - half, End = center + half };
        CommitScene(_scene with { Conductors = items });
    }
    public void SetSelectedAngle(double degrees)
    {
        if (Selection is not { } selection || !double.IsFinite(degrees)) return;
        if (selection.Kind == MagnetostaticSelectionKind.PlanarIdealConstantCurrentConductor)
        {
            var items = _scene.Conductors.ToArray(); var conductor = items[selection.Index];
            var center = (conductor.Start + conductor.End) / 2;
            var half = Vector2D.FromAngle(degrees * Math.PI / 180) * ((conductor.End - conductor.Start).Length / 2);
            items[selection.Index] = conductor with { Start = center - half, End = center + half };
            CommitScene(_scene with { Conductors = items });
        }
        else if (selection.Kind == MagnetostaticSelectionKind.VerticalCircularCurrentLoop)
        {
            var items = _scene.VerticalLoopElements.ToArray();
            items[selection.Index] = items[selection.Index] with { AngleDegrees = degrees };
            CommitScene(_scene with { VerticalLoops = items });
        }
    }
    public void SetSelectedRadius(double value)
    {
        if (Selection is not { } selection || !double.IsFinite(value)) return;
        var radius = Math.Clamp(value, 10, 100_000);
        if (selection.Kind == MagnetostaticSelectionKind.PlanarCircularCurrentLoop)
        {
            var items = _scene.PlanarLoopElements.ToArray();
            items[selection.Index] = items[selection.Index] with { Radius = radius };
            CommitScene(_scene with { PlanarLoops = items });
        }
        else if (selection.Kind == MagnetostaticSelectionKind.VerticalCircularCurrentLoop)
        {
            var items = _scene.VerticalLoopElements.ToArray();
            items[selection.Index] = items[selection.Index] with { Radius = radius };
            CommitScene(_scene with { VerticalLoops = items });
        }
    }
    public void SetSelectedSecondOrigin(double x, double y)
    {
        if (Selection is not { Kind: MagnetostaticSelectionKind.VerticalCircularCurrentLoop } selection ||
            !double.IsFinite(x) || !double.IsFinite(y)) return;
        var items = _scene.VerticalLoopElements.ToArray();
        var loop = items[selection.Index];
        var direction = new Vector2D(x, y) - loop.Center;
        if (direction.LengthSquared < 1e-12) return;
        var handleAngle = Math.Atan2(direction.Y, direction.X) * 180 / Math.PI;
        items[selection.Index] = loop with { AngleDegrees = handleAngle - 90 };
        CommitScene(_scene with { VerticalLoops = items });
    }
    public void SetSelectedOrigin(double x, double y)
    {
        if (Selection is not { } selection || !double.IsFinite(x) || !double.IsFinite(y)) return;
        var target = new Vector2D(x, y);
        if (selection.Kind == MagnetostaticSelectionKind.PlanarIdealConstantCurrentConductor)
        {
            var items = _scene.Conductors.ToArray(); var conductor = items[selection.Index];
            var offset = target - (conductor.Start + conductor.End) / 2;
            items[selection.Index] = conductor with { Start = conductor.Start + offset, End = conductor.End + offset };
            CommitScene(_scene with { Conductors = items });
        }
        else if (selection.Kind == MagnetostaticSelectionKind.VerticalInfiniteCurrentConductor)
        {
            var items = _scene.VerticalConductorElements.ToArray();
            items[selection.Index] = items[selection.Index] with { Position = target };
            CommitScene(_scene with { VerticalConductors = items });
        }
        else if (selection.Kind == MagnetostaticSelectionKind.PlanarCircularCurrentLoop)
        {
            var items = _scene.PlanarLoopElements.ToArray();
            items[selection.Index] = items[selection.Index] with { Center = target };
            CommitScene(_scene with { PlanarLoops = items });
        }
        else
        {
            var items = _scene.VerticalLoopElements.ToArray();
            items[selection.Index] = items[selection.Index] with { Center = target };
            CommitScene(_scene with { VerticalLoops = items });
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context); context.FillRectangle(BackgroundBrush, new Rect(Bounds.Size)); DrawGrid(context);
        foreach (var line in _result.FieldLines) DrawFieldLine(context, line);
        var markerReferenceField = _result.Samples.Count == 0 ? 0 : _result.Samples
            .Select(sample => Math.Abs(sample.NormalTesla)).OrderBy(value => value)
            .ElementAt((int)Math.Floor((_result.Samples.Count - 1) * .9));
        foreach (var sample in _result.Samples) DrawFieldSample(context, sample, markerReferenceField);
        for (var index = 0; index < _scene.Conductors.Length; index++) DrawConductor(context, _scene.Conductors[index], index);
        for (var index = 0; index < _scene.VerticalConductorElements.Length; index++)
            DrawVerticalConductor(context, _scene.VerticalConductorElements[index], index);
        for (var index = 0; index < _scene.PlanarLoopElements.Length; index++)
            DrawPlanarLoop(context, _scene.PlanarLoopElements[index], index);
        for (var index = 0; index < _scene.VerticalLoopElements.Length; index++)
            DrawVerticalLoop(context, _scene.VerticalLoopElements[index], index);
        if (_conductorStart is { } start)
        {
            context.DrawLine(PreviewPen, ToScreen(start), ToScreen(_conductorPreviewEnd));
            DrawHandle(context, start); DrawHandle(context, _conductorPreviewEnd);
        }
        if (_loopCenter is { } center && _loopPreviewRadius >= 1)
        {
            if (_activeTool == MagnetostaticTool.PlanarCircularCurrentLoop)
                context.DrawEllipse(null, PreviewPen, ToScreen(center),
                    _loopPreviewRadius * _zoom, _loopPreviewRadius * _zoom);
            else
                DrawEllipsePolyline(context, PreviewPen, center, _loopPreviewRadius, 0);
            DrawHandle(context, center);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus(); var screen = e.GetPosition(this); var props = e.GetCurrentPoint(this).Properties;
        if (props.IsRightButtonPressed || props.IsMiddleButtonPressed ||
            (_activeTool == MagnetostaticTool.Pan && props.IsLeftButtonPressed))
        { _isPanning = true; _lastPointer = screen; e.Pointer.Capture(this); }
        else if (props.IsLeftButtonPressed)
        {
            var world = ToWorld(screen); var hit = HitTest(world);
            switch (_activeTool)
            {
                case MagnetostaticTool.PlanarIdealConstantCurrentConductor when _conductorStart is null:
                    _conductorStart = world; _conductorPreviewEnd = world; break;
                case MagnetostaticTool.PlanarIdealConstantCurrentConductor when (world - _conductorStart.Value).Length >= 10:
                    CommitScene(_scene with { Conductors = [.. _scene.Conductors, new(_conductorStart.Value, world)] });
                    _conductorStart = null; SelectTool(MagnetostaticTool.Move);
                    SetSelection(MagnetostaticSelectionKind.PlanarIdealConstantCurrentConductor, _scene.Conductors.Length - 1); break;
                case MagnetostaticTool.VerticalInfiniteCurrentConductor:
                    CommitScene(_scene with
                    {
                        VerticalConductors = [.. _scene.VerticalConductorElements, new VerticalInfiniteCurrentConductor(world)]
                    });
                    SelectTool(MagnetostaticTool.Move);
                    SetSelection(MagnetostaticSelectionKind.VerticalInfiniteCurrentConductor,
                        _scene.VerticalConductorElements.Length - 1); break;
                case MagnetostaticTool.PlanarCircularCurrentLoop when _loopCenter is null:
                case MagnetostaticTool.VerticalCircularCurrentLoop when _loopCenter is null:
                    _loopCenter = world; _loopPreviewRadius = 0; break;
                case MagnetostaticTool.PlanarCircularCurrentLoop when (world - _loopCenter.Value).Length >= 10:
                    CommitScene(_scene with
                    {
                        PlanarLoops = [.. _scene.PlanarLoopElements,
                            new PlanarCircularCurrentLoop(_loopCenter.Value, (world - _loopCenter.Value).Length)]
                    });
                    _loopCenter = null; SelectTool(MagnetostaticTool.Move);
                    SetSelection(MagnetostaticSelectionKind.PlanarCircularCurrentLoop,
                        _scene.PlanarLoopElements.Length - 1); break;
                case MagnetostaticTool.VerticalCircularCurrentLoop when (world - _loopCenter.Value).Length >= 10:
                    CommitScene(_scene with
                    {
                        VerticalLoops = [.. _scene.VerticalLoopElements,
                            new VerticalCircularCurrentLoop(_loopCenter.Value, (world - _loopCenter.Value).Length)]
                    });
                    _loopCenter = null; SelectTool(MagnetostaticTool.Move);
                    SetSelection(MagnetostaticSelectionKind.VerticalCircularCurrentLoop,
                        _scene.VerticalLoopElements.Length - 1); break;
                case MagnetostaticTool.Move: BeginMove(hit, world, e); break;
                case MagnetostaticTool.Delete when hit is { } element:
                    DeleteElement(element); SelectTool(MagnetostaticTool.Pan); break;
            }
        }
        InvalidateVisual(); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); var screen = e.GetPosition(this); var world = ToWorld(screen);
        if (_isPanning) { _pan += new Vector2D(screen.X - _lastPointer.X, screen.Y - _lastPointer.Y); _lastPointer = screen; InvalidateVisual(); }
        else if (_isMoving && Selection is { } selection)
        { MoveSelection(selection, world); _moveChanged = _moveSimulationDirty = true; SelectionChanged?.Invoke(this, EventArgs.Empty); RecalculateDuringMoveIfDue(); InvalidateVisual(); }
        else if (_conductorStart is not null) { _conductorPreviewEnd = world; InvalidateVisual(); }
        else if (_loopCenter is { } center) { _loopPreviewRadius = (world - center).Length; InvalidateVisual(); }
        e.Handled = true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); _isPanning = false;
        if (_isMoving)
        {
            _isMoving = false;
            if (_moveChanged) { if (_moveSimulationDirty) Recalculate(); SceneChanged?.Invoke(this, EventArgs.Empty); }
            _moveChanged = _moveSimulationDirty = false;
        }
        e.Pointer.Capture(null); e.Handled = true;
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e); var point = e.GetPosition(this); var before = ToWorld(point);
        _zoom = Math.Clamp(_zoom * Math.Pow(1.12, e.Delta.Y), .15, 8);
        _pan = new(point.X - before.X * _zoom, point.Y + before.Y * _zoom);
        InvalidateVisual(); e.Handled = true;
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e); if (e.Key != Key.Escape) return;
        _conductorStart = null; _loopCenter = null; SelectTool(MagnetostaticTool.Pan); e.Handled = true;
    }

    private void BeginMove(ElementHit? hit, Vector2D world, PointerPressedEventArgs e)
    {
        if (hit is not { } element) { SetSelection(null, -1); return; }
        SetSelection(element.Kind, element.Index);
        if (element.Mode == DragMode.Body) return;
        _isMoving = true; _moveChanged = _moveSimulationDirty = false; _lastMoveSimulationTimestamp = 0;
        _dragStart = world; _dragMode = element.Mode;
        if (element.Kind == MagnetostaticSelectionKind.PlanarIdealConstantCurrentConductor)
            _dragOriginal = _scene.Conductors[element.Index];
        else if (element.Kind == MagnetostaticSelectionKind.VerticalInfiniteCurrentConductor)
        {
            _dragOriginalVertical = _scene.VerticalConductorElements[element.Index];
            _moveOffset = _dragOriginalVertical.Position - world;
        }
        else if (element.Kind == MagnetostaticSelectionKind.PlanarCircularCurrentLoop)
        {
            _dragOriginalPlanarLoop = _scene.PlanarLoopElements[element.Index];
            _moveOffset = _dragOriginalPlanarLoop.Center - world;
        }
        else
        {
            _dragOriginalVerticalLoop = _scene.VerticalLoopElements[element.Index];
            _moveOffset = _dragOriginalVerticalLoop.Center - world;
        }
        e.Pointer.Capture(this);
    }
    private void MoveSelection(MagnetostaticSelection selection, Vector2D world)
    {
        if (_dragMode == DragMode.RotationHandle)
        {
            if (selection.Kind != MagnetostaticSelectionKind.VerticalCircularCurrentLoop) return;
            var items = _scene.VerticalLoopElements.ToArray();
            var loop = items[selection.Index];
            var direction = world - loop.Center;
            if (direction.LengthSquared < 1e-12) return;
            items[selection.Index] = loop with
            {
                AngleDegrees = Math.Atan2(direction.Y, direction.X) * 180 / Math.PI - 90
            };
            SetAndRaise(SceneProperty, ref _scene, _scene with { VerticalLoops = items });
            return;
        }
        if (selection.Kind == MagnetostaticSelectionKind.PlanarIdealConstantCurrentConductor)
        {
            if (_dragOriginal is not { } original) return;
            var items = _scene.Conductors.ToArray(); var offset = world - _dragStart;
            items[selection.Index] = original with { Start = original.Start + offset, End = original.End + offset };
            SetAndRaise(SceneProperty, ref _scene, _scene with { Conductors = items });
        }
        else if (selection.Kind == MagnetostaticSelectionKind.VerticalInfiniteCurrentConductor)
        {
            if (_dragOriginalVertical is null) return;
            var items = _scene.VerticalConductorElements.ToArray();
            items[selection.Index] = items[selection.Index] with { Position = world + _moveOffset };
            SetAndRaise(SceneProperty, ref _scene, _scene with { VerticalConductors = items });
        }
        else if (selection.Kind == MagnetostaticSelectionKind.PlanarCircularCurrentLoop)
        {
            if (_dragOriginalPlanarLoop is null) return;
            var items = _scene.PlanarLoopElements.ToArray();
            items[selection.Index] = items[selection.Index] with { Center = world + _moveOffset };
            SetAndRaise(SceneProperty, ref _scene, _scene with { PlanarLoops = items });
        }
        else
        {
            if (_dragOriginalVerticalLoop is null) return;
            var items = _scene.VerticalLoopElements.ToArray();
            items[selection.Index] = items[selection.Index] with { Center = world + _moveOffset };
            SetAndRaise(SceneProperty, ref _scene, _scene with { VerticalLoops = items });
        }
    }

    private void DrawGrid(DrawingContext context)
    {
        const double spacing = 50; var left = ToWorld(new(0, 0)).X; var right = ToWorld(new(Bounds.Width, 0)).X;
        var top = ToWorld(new(0, 0)).Y; var bottom = ToWorld(new(0, Bounds.Height)).Y;
        for (var x = Math.Floor(left / spacing) * spacing; x <= right; x += spacing)
            context.DrawLine(Math.Abs(x % 250) < .01 ? MajorGridPen : MinorGridPen, ToScreen(new(x, top)), ToScreen(new(x, bottom)));
        for (var y = Math.Floor(bottom / spacing) * spacing; y <= top; y += spacing)
            context.DrawLine(Math.Abs(y % 250) < .01 ? MajorGridPen : MinorGridPen, ToScreen(new(left, y)), ToScreen(new(right, y)));
        context.DrawLine(AxisPen, ToScreen(new(0, top)), ToScreen(new(0, bottom)));
        context.DrawLine(AxisPen, ToScreen(new(left, 0)), ToScreen(new(right, 0))); DrawTicks(context, left, right, bottom, top);
    }
    private void DrawTicks(DrawingContext context, double left, double right, double bottom, double top)
    {
        var step = 250d; while (step * _zoom < 72) step *= 2;
        if (_pan.Y >= 0 && _pan.Y <= Bounds.Height)
            for (var x = Math.Ceiling(left / step) * step; x <= right; x += step)
            { var p = ToScreen(new(x, 0)); context.DrawLine(TickPen, new(p.X, _pan.Y - 4), new(p.X, _pan.Y + 4)); if (Math.Abs(x) > .01) DrawCoordinate(context, x, new(p.X + 4, _pan.Y + 6)); }
        if (_pan.X >= 0 && _pan.X <= Bounds.Width)
            for (var y = Math.Ceiling(bottom / step) * step; y <= top; y += step)
            { var p = ToScreen(new(0, y)); context.DrawLine(TickPen, new(_pan.X - 4, p.Y), new(_pan.X + 4, p.Y)); if (Math.Abs(y) > .01) DrawCoordinate(context, y, new(_pan.X + 7, p.Y + 2)); }
    }
    private static void DrawCoordinate(DrawingContext context, double value, Point point) =>
        context.DrawText(Text(Math.Round(value).ToString(CultureInfo.InvariantCulture), 11), point);

    private void DrawFieldLine(DrawingContext context, MagneticFieldLine line)
    {
        for (var index = 1; index < line.Points.Count; index++)
            context.DrawLine(FieldPen, ToScreen(line.Points[index - 1]), ToScreen(line.Points[index]));
        if (line.Points.Count < 4) return;
        var arrowIndex = line.Points.Count / 4;
        var direction = (line.Points[arrowIndex + 1] - line.Points[arrowIndex - 1]).Normalized();
        DrawArrowHead(context, FieldPen, line.Points[arrowIndex], direction, 8, 4);
    }

    private void DrawFieldSample(DrawingContext context, MagneticFieldSample sample, double maximumField)
    {
        var center = ToScreen(sample.Position);
        if (center.X < -10 || center.X > Bounds.Width + 10 || center.Y < -10 || center.Y > Bounds.Height + 10) return;
        var relativeMagnitude = maximumField <= 0 ? 0 : Math.Sqrt(Math.Abs(sample.NormalTesla) / maximumField);
        var symbolSize = 2.5 + 4 * Math.Clamp(relativeMagnitude, 0, 1);
        if (sample.NormalTesla > 0)
        {
            // Dot: vector points out of the drawing plane.
            context.DrawEllipse(FieldBrush, null, center, symbolSize * .48, symbolSize * .48);
        }
        else
        {
            // Cross: vector points into the drawing plane.
            var arm = symbolSize * .72;
            context.DrawLine(FieldPen, new(center.X - arm, center.Y - arm), new(center.X + arm, center.Y + arm));
            context.DrawLine(FieldPen, new(center.X - arm, center.Y + arm), new(center.X + arm, center.Y - arm));
        }
    }
    private void DrawConductor(DrawingContext context, PlanarIdealConstantCurrentConductor conductor, int index)
    {
        var pen = conductor.CurrentAmperes > 1e-12 ? PositiveCurrentPen :
            conductor.CurrentAmperes < -1e-12 ? NegativeCurrentPen : ZeroCurrentPen;
        context.DrawLine(pen, ToScreen(conductor.Start), ToScreen(conductor.End));
        var center = (conductor.Start + conductor.End) / 2;
        var positiveDirection = (conductor.End - conductor.Start).Normalized();
        var currentDirection = conductor.CurrentAmperes < 0 ? -positiveDirection : positiveDirection;
        if (Math.Abs(conductor.CurrentAmperes) > 1e-12)
            DrawArrowHead(context, pen, center, currentDirection, 11, 5);
        var centerScreen = ToScreen(center);
        context.DrawText(Text($"I = {conductor.CurrentAmperes:G5} A", 11), new(centerScreen.X + 8, centerScreen.Y - 22));
        if (_activeTool == MagnetostaticTool.Move) DrawHandle(context, center);
        if (_selectedKind == MagnetostaticSelectionKind.PlanarIdealConstantCurrentConductor && _selectedIndex == index)
            context.DrawLine(SelectionPen, ToScreen(conductor.Start), ToScreen(conductor.End));
    }
    private void DrawVerticalConductor(DrawingContext context, VerticalInfiniteCurrentConductor conductor, int index)
    {
        var brush = conductor.CurrentAmperes > 1e-12 ? PositiveCurrentBrush :
            conductor.CurrentAmperes < -1e-12 ? NegativeCurrentBrush : ZeroCurrentBrush;
        var center = ToScreen(conductor.Position);
        context.DrawEllipse(brush, TickPen, center, 12, 12);
        if (conductor.CurrentAmperes > 1e-12)
            context.DrawEllipse(Brushes.White, null, center, 2.2, 2.2);
        else if (conductor.CurrentAmperes < -1e-12)
        {
            context.DrawLine(SelectionPen, new(center.X - 5, center.Y - 5), new(center.X + 5, center.Y + 5));
            context.DrawLine(SelectionPen, new(center.X - 5, center.Y + 5), new(center.X + 5, center.Y - 5));
        }
        else
            context.DrawLine(SelectionPen, new(center.X - 5, center.Y), new(center.X + 5, center.Y));
        context.DrawText(Text($"I = {conductor.CurrentAmperes:G5} A", 11), new(center.X + 16, center.Y - 20));
        if (_selectedKind == MagnetostaticSelectionKind.VerticalInfiniteCurrentConductor && _selectedIndex == index)
            context.DrawEllipse(null, SelectionPen, center, 17, 17);
    }
    private void DrawPlanarLoop(DrawingContext context, PlanarCircularCurrentLoop loop, int index)
    {
        var pen = CurrentPen(loop.CurrentAmperes);
        var center = ToScreen(loop.Center);
        context.DrawEllipse(null, pen, center, loop.Radius * _zoom, loop.Radius * _zoom);
        if (Math.Abs(loop.CurrentAmperes) > 1e-12)
        {
            var radial = Vector2D.FromAngle(Math.PI / 4);
            var direction = radial.Perpendicular() * Math.Sign(loop.CurrentAmperes);
            DrawArrowHead(context, pen, loop.Center + radial * loop.Radius, direction, 11, 5);
        }
        context.DrawText(Text($"I = {loop.CurrentAmperes:G5} A", 11), new(center.X + 10, center.Y - 20));
        if (_activeTool == MagnetostaticTool.Move) DrawHandle(context, loop.Center);
        if (_selectedKind == MagnetostaticSelectionKind.PlanarCircularCurrentLoop && _selectedIndex == index)
            context.DrawEllipse(null, SelectionPen, center, loop.Radius * _zoom, loop.Radius * _zoom);
    }
    private void DrawVerticalLoop(DrawingContext context, VerticalCircularCurrentLoop loop, int index)
    {
        var pen = CurrentPen(loop.CurrentAmperes);
        DrawEllipsePolyline(context, pen, loop.Center, loop.Radius, loop.AngleDegrees, dashedBackHalf: true);
        var axis = Vector2D.FromAngle(loop.AngleDegrees * Math.PI / 180);
        var normal = axis.Perpendicular();
        if (Math.Abs(loop.CurrentAmperes) > 1e-12)
        {
            const double parameter = Math.PI / 4;
            var point = loop.Center + axis * (loop.Radius * Math.Cos(parameter)) +
                        normal * (loop.Radius * .38 * Math.Sin(parameter));
            var tangent = axis * (-loop.Radius * Math.Sin(parameter)) +
                          normal * (loop.Radius * .38 * Math.Cos(parameter));
            DrawArrowHead(context, pen, point, tangent.Normalized() * Math.Sign(loop.CurrentAmperes), 11, 5);
        }
        var center = ToScreen(loop.Center);
        context.DrawText(Text($"I = {loop.CurrentAmperes:G5} A", 11), new(center.X + 10, center.Y - 20));
        if (_activeTool == MagnetostaticTool.Move)
        {
            DrawHandle(context, loop.Center);
            DrawHandle(context, VerticalLoopRotationHandle(loop));
        }
        if (_selectedKind == MagnetostaticSelectionKind.VerticalCircularCurrentLoop && _selectedIndex == index)
            DrawEllipsePolyline(context, SelectionPen,
                loop.Center, loop.Radius, loop.AngleDegrees, dashedBackHalf: true);
    }
    private void DrawEllipsePolyline(
        DrawingContext context,
        Pen pen,
        Vector2D center,
        double radius,
        double angleDegrees,
        bool dashedBackHalf = false)
    {
        const int segments = 72;
        var axis = Vector2D.FromAngle(angleDegrees * Math.PI / 180);
        var normal = axis.Perpendicular();
        var previous = center + axis * radius;
        for (var index = 1; index <= segments; index++)
        {
            var parameter = 2 * Math.PI * index / segments;
            var current = center + axis * (radius * Math.Cos(parameter)) +
                          normal * (radius * .38 * Math.Sin(parameter));
            // The ellipse's minor-axis coordinate represents physical z. sin(t) < 0 is the
            // half-loop below the drawing plane and is therefore drawn dashed.
            var midpointParameter = 2 * Math.PI * (index - 0.5) / segments;
            var isBackHalf = Math.Sin(midpointParameter) < 0;
            if (!isBackHalf || !dashedBackHalf || index % 4 < 2)
                context.DrawLine(pen, ToScreen(previous), ToScreen(current));
            previous = current;
        }
    }
    private static Pen CurrentPen(double current) => current > 1e-12 ? PositiveCurrentPen :
        current < -1e-12 ? NegativeCurrentPen : ZeroCurrentPen;
    private static Vector2D VerticalLoopRotationHandle(VerticalCircularCurrentLoop loop) =>
        loop.Center + Vector2D.FromAngle(loop.AngleDegrees * Math.PI / 180).Perpendicular() * RotationHandleOffset;
    private void DrawArrowHead(DrawingContext context, Pen pen, Vector2D point, Vector2D direction, double length, double width)
    {
        var back = direction * (-length / _zoom); var wing = direction.Perpendicular() * (width / _zoom);
        context.DrawLine(pen, ToScreen(point), ToScreen(point + back + wing));
        context.DrawLine(pen, ToScreen(point), ToScreen(point + back - wing));
    }
    private void DrawHandle(DrawingContext context, Vector2D point) =>
        context.DrawEllipse(Brushes.White, TickPen, ToScreen(point), 5, 5);
    private ElementHit? HitTest(Vector2D world)
    {
        for (var index = _scene.VerticalLoopElements.Length - 1; index >= 0; index--)
        {
            var loop = _scene.VerticalLoopElements[index];
            if (_activeTool == MagnetostaticTool.Move &&
                (world - VerticalLoopRotationHandle(loop)).Length <= 12 / _zoom)
                return new(MagnetostaticSelectionKind.VerticalCircularCurrentLoop, index, DragMode.RotationHandle);
            var axis = Vector2D.FromAngle(loop.AngleDegrees * Math.PI / 180);
            var normal = axis.Perpendicular();
            var relative = world - loop.Center;
            var ellipticalRadius = Math.Sqrt(Math.Pow(relative.Dot(axis), 2) +
                                             Math.Pow(relative.Dot(normal) / .38, 2));
            if (relative.Length <= 12 / _zoom)
                return new(MagnetostaticSelectionKind.VerticalCircularCurrentLoop, index, DragMode.Origin);
            if (Math.Abs(ellipticalRadius - loop.Radius) <= 10 / _zoom)
                return new(MagnetostaticSelectionKind.VerticalCircularCurrentLoop, index, DragMode.Body);
        }
        for (var index = _scene.PlanarLoopElements.Length - 1; index >= 0; index--)
        {
            var loop = _scene.PlanarLoopElements[index];
            if ((world - loop.Center).Length <= 12 / _zoom)
                return new(MagnetostaticSelectionKind.PlanarCircularCurrentLoop, index, DragMode.Origin);
            if (Math.Abs((world - loop.Center).Length - loop.Radius) <= 10 / _zoom)
                return new(MagnetostaticSelectionKind.PlanarCircularCurrentLoop, index, DragMode.Body);
        }
        for (var index = _scene.VerticalConductorElements.Length - 1; index >= 0; index--)
            if ((world - _scene.VerticalConductorElements[index].Position).Length <= 18 / _zoom)
                return new(MagnetostaticSelectionKind.VerticalInfiniteCurrentConductor, index, DragMode.Origin);
        for (var index = _scene.Conductors.Length - 1; index >= 0; index--)
        {
            var conductor = _scene.Conductors[index];
            if ((world - (conductor.Start + conductor.End) / 2).Length <= 12 / _zoom ||
                DistanceToSegment(world, conductor.Start, conductor.End) <= 10 / _zoom)
                return new(MagnetostaticSelectionKind.PlanarIdealConstantCurrentConductor, index,
                    (world - (conductor.Start + conductor.End) / 2).Length <= 12 / _zoom
                        ? DragMode.Origin : DragMode.Body);
        }
        return null;
    }
    private void DeleteElement(ElementHit hit)
    {
        SetSelection(null, -1);
        if (hit.Kind == MagnetostaticSelectionKind.PlanarIdealConstantCurrentConductor)
            CommitScene(_scene with { Conductors = _scene.Conductors.Where((_, index) => index != hit.Index).ToArray() });
        else if (hit.Kind == MagnetostaticSelectionKind.VerticalInfiniteCurrentConductor)
            CommitScene(_scene with
            {
                VerticalConductors = _scene.VerticalConductorElements.Where((_, index) => index != hit.Index).ToArray()
            });
        else if (hit.Kind == MagnetostaticSelectionKind.PlanarCircularCurrentLoop)
            CommitScene(_scene with
            {
                PlanarLoops = _scene.PlanarLoopElements.Where((_, index) => index != hit.Index).ToArray()
            });
        else
            CommitScene(_scene with
            {
                VerticalLoops = _scene.VerticalLoopElements.Where((_, index) => index != hit.Index).ToArray()
            });
    }
    private void SetSelection(MagnetostaticSelectionKind? kind, int index)
    {
        if (_selectedKind == kind && _selectedIndex == index) return;
        _selectedKind = kind; _selectedIndex = index;
        SelectionChanged?.Invoke(this, EventArgs.Empty); InvalidateVisual();
    }
    private void CommitScene(MagnetostaticScene scene)
    {
        SetAndRaise(SceneProperty, ref _scene, scene); Recalculate();
        SceneChanged?.Invoke(this, EventArgs.Empty); SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private void Recalculate()
    {
        _result = _simulator.Simulate(_scene, new(_markerDensity));
        SimulationCompleted?.Invoke(this, EventArgs.Empty); InvalidateVisual();
    }
    private void RecalculateDuringMoveIfDue()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = _lastMoveSimulationTimestamp == 0 ? double.PositiveInfinity :
            (now - _lastMoveSimulationTimestamp) * 1000d / Stopwatch.Frequency;
        if (elapsed < 33) return;
        _lastMoveSimulationTimestamp = now; _moveSimulationDirty = false; Recalculate();
    }

    private static double DistanceToSegment(Vector2D point, Vector2D start, Vector2D end)
    { var delta = end - start; if (delta.LengthSquared < 1e-12) return (point - start).Length; return (point - (start + delta * Math.Clamp((point - start).Dot(delta) / delta.LengthSquared, 0, 1))).Length; }
    private static bool IsIndex<T>(int index, IReadOnlyList<T> items) => index >= 0 && index < items.Count;
    private static double NormalizeDegrees(double degrees) => (degrees % 360 + 360) % 360;
    private Point ToScreen(Vector2D point) => new(point.X * _zoom + _pan.X, _pan.Y - point.Y * _zoom);
    private Vector2D ToWorld(Point point) => new((point.X - _pan.X) / _zoom, (_pan.Y - point.Y) / _zoom);
    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
    private static Pen Pen(string color, double width) => new(Brush(color), width);
    private static FormattedText Text(string value, double size) =>
        new(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new("Inter"), size, TextBrush);
    private readonly record struct ElementHit(MagnetostaticSelectionKind Kind, int Index, DragMode Mode);
    private enum DragMode { Body, Origin, RotationHandle }
}
