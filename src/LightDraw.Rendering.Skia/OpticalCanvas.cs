using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using System.Diagnostics;
using System.Globalization;
using LightDraw.Core.Geometry;
using LightDraw.Core.Scene;
using LightDraw.Core.Simulation;
using SkiaSharp;

namespace LightDraw.Rendering.Skia;

public enum CanvasTool
{
    Pan,
    Move,
    Delete,
    PointLight,
    ParallelLight,
    Mirror,
    ConvexLens,
    ConcaveLens
}

public enum CanvasSelectionKind
{
    PointLight,
    ParallelLight,
    Mirror,
    ConvexLens,
    ConcaveLens
}

public sealed record CanvasSelection(
    CanvasSelectionKind Kind,
    string DisplayName,
    bool CanRotate,
    double OriginX,
    double OriginY,
    double AngleDegrees,
    double? FocalLength,
    double? Length);

internal enum SceneItemKind
{
    None,
    LightSource,
    Mirror,
    Lens
}

internal enum MoveDragMode
{
    None,
    Translate,
    StartEndpoint,
    EndEndpoint
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
    private OpticalScene _scene = OpticalScene.CreateEmpty();
    private SimulationResult _result = new([], 0, 0, 0, TimeSpan.Zero);
    private Vector2D _pan = new(520, 360);
    private double _zoom = 1;
    private bool _isPanning;
    private Point _lastPointer;
    private int _raysPerSource = 160;
    private CanvasTool _tool = CanvasTool.Pan;
    private Vector2D? _placementStart;
    private Vector2D? _placementPreview;
    private SceneItemKind _movingKind;
    private MoveDragMode _moveDragMode;
    private int _movingIndex = -1;
    private Vector2D _lastMoveWorld;
    private bool _moveChanged;
    private bool _moveSimulationDirty;
    private long _lastMoveSimulationTimestamp;
    private SceneItemKind _selectedKind;
    private int _selectedIndex = -1;

    public OpticalCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        Recalculate();
    }

    public OpticalScene Scene
    {
        get => _scene;
        set => SetScene(value);
    }

    public SimulationResult SimulationResult => _result;
    public int RaysPerSource
    {
        get => _raysPerSource;
        set => SetRaysPerSource(value);
    }

    public CanvasTool ActiveTool
    {
        get => _tool;
        set => SelectTool(value);
    }
    public bool IsPlacing => _placementStart is not null;
    public CanvasSelection? Selection => CreateSelection();

    public event EventHandler? SceneChanged;
    public event EventHandler? SimulationCompleted;
    public event EventHandler? ToolStateChanged;
    public event EventHandler? SelectionChanged;

    public void SetScene(OpticalScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (ReferenceEquals(_scene, scene))
        {
            // Guards against re-entrant sets caused by the two-way Scene binding
            // echoing back the exact same instance we just pushed via UpdateScene
            // (e.g. while dragging with the Move tool). Without this guard, the
            // echo would reset the in-progress drag state below and the item
            // would only move by a single pointer-move delta.
            return;
        }

        var normalized = scene with
        {
            LightSources = scene.LightSources ?? [],
            Mirrors = scene.Mirrors ?? [],
            Lenses = scene.LensElements
        };
        SetAndRaise(SceneProperty, ref _scene, normalized);
        SetAndRaise(ActiveToolProperty, ref _tool, CanvasTool.Pan);
        _movingKind = SceneItemKind.None;
        _moveDragMode = MoveDragMode.None;
        _movingIndex = -1;
        _moveChanged = false;
        _moveSimulationDirty = false;
        ClearSelection();
        CancelPlacement();
        Recalculate();
        SceneChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetRaysPerSource(int value)
    {
        var clamped = Math.Clamp(value, 1, 2000);
        if (clamped == _raysPerSource)
        {
            return;
        }

        SetAndRaise(RaysPerSourceProperty, ref _raysPerSource, clamped);
        Recalculate();
    }

    public void SelectTool(CanvasTool tool)
    {
        SetAndRaise(ActiveToolProperty, ref _tool, tool);
        if (tool != CanvasTool.Move)
        {
            ClearSelection();
        }
        CancelPlacement(false);
        ToolStateChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    public void CancelPlacement(bool notify = true)
    {
        _placementStart = null;
        _placementPreview = null;
        if (notify)
        {
            ToolStateChanged?.Invoke(this, EventArgs.Empty);
        }
        InvalidateVisual();
    }

    public void ResetView()
    {
        _zoom = 1;
        _pan = new Vector2D(Math.Max(420, Bounds.Width * 0.48), Math.Max(300, Bounds.Height * 0.52));
        InvalidateVisual();
    }

    public void RotateSelectedBy(double degrees)
    {
        var selection = CreateSelection();
        if (selection is null || !selection.CanRotate)
        {
            return;
        }

        SetSelectedAngle(selection.AngleDegrees + degrees);
    }

    public void SetSelectedAngle(double degrees)
    {
        if (!double.IsFinite(degrees))
        {
            return;
        }

        var radians = NormalizeDegrees(degrees) * Math.PI / 180;
        switch (_selectedKind)
        {
            case SceneItemKind.LightSource when IsValidIndex(_selectedIndex, _scene.LightSources):
                var sources = (LightSource[])_scene.LightSources.Clone();
                var source = sources[_selectedIndex];
                if (source.Kind != LightSourceKind.ParallelLine || source.End is not { } sourceEnd)
                {
                    return;
                }
                var rotatedSource = RotateSegment(source.Position, sourceEnd, radians);
                sources[_selectedIndex] = source with
                {
                    Position = rotatedSource.Start,
                    End = rotatedSource.End,
                    DirectionDegrees = DirectionDegrees((rotatedSource.End - rotatedSource.Start)
                        .Normalized().Perpendicular())
                };
                UpdateScene(_scene with { LightSources = sources });
                break;
            case SceneItemKind.Mirror when IsValidIndex(_selectedIndex, _scene.Mirrors):
                var mirrors = (MirrorSegment[])_scene.Mirrors.Clone();
                var mirror = mirrors[_selectedIndex];
                var rotatedMirror = RotateSegment(mirror.Start, mirror.End, radians);
                mirrors[_selectedIndex] = mirror with
                {
                    Start = rotatedMirror.Start,
                    End = rotatedMirror.End
                };
                UpdateScene(_scene with { Mirrors = mirrors });
                break;
            case SceneItemKind.Lens when IsValidIndex(_selectedIndex, _scene.LensElements):
                var lenses = (LensSegment[])_scene.LensElements.Clone();
                var lens = lenses[_selectedIndex];
                var rotatedLens = RotateSegment(lens.Start, lens.End, radians);
                lenses[_selectedIndex] = lens with
                {
                    Start = rotatedLens.Start,
                    End = rotatedLens.End
                };
                UpdateScene(_scene with { Lenses = lenses });
                break;
            default:
                return;
        }

        CommitSelectedEdit();
    }

    public void SetSelectedFocalLength(double focalLength)
    {
        if (_selectedKind != SceneItemKind.Lens ||
            !IsValidIndex(_selectedIndex, _scene.LensElements) ||
            !double.IsFinite(focalLength))
        {
            return;
        }

        var lenses = (LensSegment[])_scene.LensElements.Clone();
        var lens = lenses[_selectedIndex];
        var clamped = Math.Clamp(Math.Abs(focalLength), 1, 10000);
        if (Math.Abs(lens.FocalLength - clamped) <= 1e-9)
        {
            return;
        }

        lenses[_selectedIndex] = lens with { FocalLength = clamped };
        UpdateScene(_scene with { Lenses = lenses });
        CommitSelectedEdit();
    }

    public void SetSelectedLength(double length)
    {
        if (!double.IsFinite(length))
        {
            return;
        }

        var clamped = Math.Clamp(Math.Abs(length), 1, 10000);
        switch (_selectedKind)
        {
            case SceneItemKind.LightSource when IsValidIndex(_selectedIndex, _scene.LightSources):
                var sources = (LightSource[])_scene.LightSources.Clone();
                var source = sources[_selectedIndex];
                if (source.Kind != LightSourceKind.ParallelLine || source.End is not { } sourceEnd)
                {
                    return;
                }
                var resizedSource = ResizeSegment(source.Position, sourceEnd, clamped);
                sources[_selectedIndex] = source with
                {
                    Position = resizedSource.Start,
                    End = resizedSource.End
                };
                UpdateScene(_scene with { LightSources = sources });
                break;
            case SceneItemKind.Mirror when IsValidIndex(_selectedIndex, _scene.Mirrors):
                var mirrors = (MirrorSegment[])_scene.Mirrors.Clone();
                var mirror = mirrors[_selectedIndex];
                var resizedMirror = ResizeSegment(mirror.Start, mirror.End, clamped);
                mirrors[_selectedIndex] = mirror with
                {
                    Start = resizedMirror.Start,
                    End = resizedMirror.End
                };
                UpdateScene(_scene with { Mirrors = mirrors });
                break;
            case SceneItemKind.Lens when IsValidIndex(_selectedIndex, _scene.LensElements):
                var lenses = (LensSegment[])_scene.LensElements.Clone();
                var lens = lenses[_selectedIndex];
                var resizedLens = ResizeSegment(lens.Start, lens.End, clamped);
                lenses[_selectedIndex] = lens with
                {
                    Start = resizedLens.Start,
                    End = resizedLens.End
                };
                UpdateScene(_scene with { Lenses = lenses });
                break;
            default:
                return;
        }

        CommitSelectedEdit();
    }

    public void SetSelectedOrigin(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || CreateSelection() is not { } selection)
        {
            return;
        }

        var delta = new Vector2D(x - selection.OriginX, y - selection.OriginY);
        if (delta.LengthSquared <= 1e-12)
        {
            return;
        }

        switch (_selectedKind)
        {
            case SceneItemKind.LightSource when IsValidIndex(_selectedIndex, _scene.LightSources):
                var sources = (LightSource[])_scene.LightSources.Clone();
                var source = sources[_selectedIndex];
                sources[_selectedIndex] = source with
                {
                    Position = source.Position + delta,
                    End = source.End is { } sourceEnd ? sourceEnd + delta : null
                };
                UpdateScene(_scene with { LightSources = sources });
                break;
            case SceneItemKind.Mirror when IsValidIndex(_selectedIndex, _scene.Mirrors):
                var mirrors = (MirrorSegment[])_scene.Mirrors.Clone();
                var mirror = mirrors[_selectedIndex];
                mirrors[_selectedIndex] = mirror with
                {
                    Start = mirror.Start + delta,
                    End = mirror.End + delta
                };
                UpdateScene(_scene with { Mirrors = mirrors });
                break;
            case SceneItemKind.Lens when IsValidIndex(_selectedIndex, _scene.LensElements):
                var lenses = (LensSegment[])_scene.LensElements.Clone();
                var lens = lenses[_selectedIndex];
                lenses[_selectedIndex] = lens with
                {
                    Start = lens.Start + delta,
                    End = lens.End + delta
                };
                UpdateScene(_scene with { Lenses = lenses });
                break;
            default:
                return;
        }

        CommitSelectedEdit();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.Custom(new SkiaDrawOperation(new Rect(Bounds.Size), _scene, _result, _pan, _zoom,
            _raysPerSource, _tool, _placementStart, _placementPreview, _selectedKind, _selectedIndex));
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
            if (TryBeginMove(ScreenToWorld(pointerPosition)))
            {
                e.Pointer.Capture(this);
            }
        }
        else if (_tool == CanvasTool.Delete && properties.IsLeftButtonPressed)
        {
            DeleteItemAt(ScreenToWorld(pointerPosition));
        }
        else if (properties.IsLeftButtonPressed)
        {
            var world = ScreenToWorld(pointerPosition);
            if (_tool == CanvasTool.PointLight)
            {
                AddPointLight(world);
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
                CompletePlacement(_placementStart.Value, world);
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
        else if (_movingKind != SceneItemKind.None)
        {
            MoveSelectedItem(ScreenToWorld(current));
        }
        else if (_placementStart is not null)
        {
            _placementPreview = ScreenToWorld(current);
        }

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
        else if (_movingKind != SceneItemKind.None)
        {
            _movingKind = SceneItemKind.None;
            _moveDragMode = MoveDragMode.None;
            _movingIndex = -1;
            e.Pointer.Capture(null);
            if (_moveChanged)
            {
                _moveChanged = false;
                if (_moveSimulationDirty)
                {
                    Recalculate();
                    _moveSimulationDirty = false;
                }
                SceneChanged?.Invoke(this, EventArgs.Empty);
            }
            ToolStateChanged?.Invoke(this, EventArgs.Empty);
        }
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var pointer = e.GetPosition(this);
        var before = ScreenToWorld(pointer);
        _zoom = Math.Clamp(_zoom * Math.Pow(1.12, e.Delta.Y), 0.15, 8);
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

    private void CompletePlacement(Vector2D start, Vector2D end)
    {
        var delta = end - start;
        if (delta.Length < 4 / _zoom)
        {
            return;
        }

        switch (_tool)
        {
            case CanvasTool.ParallelLight:
                AddParallelLight(start, end, delta);
                break;
            case CanvasTool.Mirror:
                UpdateScene(_scene with { Mirrors = [.. _scene.Mirrors, new MirrorSegment(start, end)] });
                break;
            case CanvasTool.ConvexLens:
            case CanvasTool.ConcaveLens:
                var kind = _tool == CanvasTool.ConvexLens ? LensKind.Convex : LensKind.Concave;
                var lens = new LensSegment(start, end, kind, Math.Max(50, delta.Length * 0.75));
                UpdateScene(_scene with { Lenses = [.. _scene.LensElements, lens] });
                break;
        }

        FinishOneShotPlacement();
    }

    private void AddPointLight(Vector2D position)
    {
        var source = new LightSource(position, 0, 360, 589, LightSourceKind.Point);
        UpdateScene(_scene with { LightSources = [.. _scene.LightSources, source] });
    }

    private void FinishOneShotPlacement()
    {
        _placementStart = null;
        _placementPreview = null;
        SetAndRaise(ActiveToolProperty, ref _tool, CanvasTool.Pan);
        Recalculate();
        SceneChanged?.Invoke(this, EventArgs.Empty);
        ToolStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddParallelLight(Vector2D start, Vector2D end, Vector2D segment)
    {
        var direction = segment.Normalized().Perpendicular();
        var source = new LightSource(start, DirectionDegrees(direction), 0, 589,
            LightSourceKind.ParallelLine, end);
        UpdateScene(_scene with { LightSources = [.. _scene.LightSources, source] });
    }

    private bool TryBeginMove(Vector2D world)
    {
        _movingKind = SceneItemKind.None;
        _moveDragMode = MoveDragMode.None;
        _movingIndex = -1;

        var bestDistance = 12 / _zoom;
        for (var index = 0; index < _scene.LightSources.Length; index++)
        {
            var source = _scene.LightSources[index];
            if (source.Kind != LightSourceKind.ParallelLine || source.End is not { } end)
            {
                continue;
            }

            SelectIfCloser((world - source.Position).Length, SceneItemKind.LightSource, index,
                MoveDragMode.StartEndpoint, ref bestDistance);
            SelectIfCloser((world - end).Length, SceneItemKind.LightSource, index,
                MoveDragMode.EndEndpoint, ref bestDistance);
        }

        for (var index = 0; index < _scene.Mirrors.Length; index++)
        {
            var mirror = _scene.Mirrors[index];
            SelectIfCloser((world - mirror.Start).Length, SceneItemKind.Mirror, index,
                MoveDragMode.StartEndpoint, ref bestDistance);
            SelectIfCloser((world - mirror.End).Length, SceneItemKind.Mirror, index,
                MoveDragMode.EndEndpoint, ref bestDistance);
        }

        for (var index = 0; index < _scene.LensElements.Length; index++)
        {
            var lens = _scene.LensElements[index];
            SelectIfCloser((world - lens.Start).Length, SceneItemKind.Lens, index,
                MoveDragMode.StartEndpoint, ref bestDistance);
            SelectIfCloser((world - lens.End).Length, SceneItemKind.Lens, index,
                MoveDragMode.EndEndpoint, ref bestDistance);
        }

        if (_movingKind == SceneItemKind.None)
        {
            bestDistance = 10 / _zoom;
            FindTranslatableItem(world, ref bestDistance);
        }

        if (_movingKind == SceneItemKind.None)
        {
            ClearSelection();
            return false;
        }

        _selectedKind = _movingKind;
        _selectedIndex = _movingIndex;
        _lastMoveWorld = world;
        _moveChanged = false;
        _moveSimulationDirty = false;
        _lastMoveSimulationTimestamp = 0;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        ToolStateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void DeleteItemAt(Vector2D world)
    {
        var bestDistance = 12 / _zoom;
        var itemKind = SceneItemKind.None;
        var itemIndex = -1;

        void Consider(double distance, SceneItemKind kind, int index)
        {
            if (distance > bestDistance)
            {
                return;
            }

            bestDistance = distance;
            itemKind = kind;
            itemIndex = index;
        }

        for (var index = 0; index < _scene.LightSources.Length; index++)
        {
            var source = _scene.LightSources[index];
            var distance = source.Kind == LightSourceKind.ParallelLine && source.End is { } end
                ? DistanceToSegment(world, source.Position, end)
                : (world - source.Position).Length;
            Consider(distance, SceneItemKind.LightSource, index);
        }

        for (var index = 0; index < _scene.Mirrors.Length; index++)
        {
            var mirror = _scene.Mirrors[index];
            Consider(DistanceToSegment(world, mirror.Start, mirror.End), SceneItemKind.Mirror, index);
        }

        for (var index = 0; index < _scene.LensElements.Length; index++)
        {
            var lens = _scene.LensElements[index];
            Consider(DistanceToSegment(world, lens.Start, lens.End), SceneItemKind.Lens, index);
        }

        switch (itemKind)
        {
            case SceneItemKind.LightSource:
                UpdateScene(_scene with { LightSources = RemoveAt(_scene.LightSources, itemIndex) });
                break;
            case SceneItemKind.Mirror:
                UpdateScene(_scene with { Mirrors = RemoveAt(_scene.Mirrors, itemIndex) });
                break;
            case SceneItemKind.Lens:
                UpdateScene(_scene with { Lenses = RemoveAt(_scene.LensElements, itemIndex) });
                break;
            default:
                return;
        }

        SetAndRaise(ActiveToolProperty, ref _tool, CanvasTool.Pan);
        Recalculate();
        SceneChanged?.Invoke(this, EventArgs.Empty);
        ToolStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static T[] RemoveAt<T>(T[] items, int index) =>
        [.. items[..index], .. items[(index + 1)..]];

    private void FindTranslatableItem(Vector2D world, ref double bestDistance)
    {

        for (var index = 0; index < _scene.LightSources.Length; index++)
        {
            var source = _scene.LightSources[index];
            var distance = source.Kind == LightSourceKind.ParallelLine && source.End is { } end
                ? DistanceToSegment(world, source.Position, end)
                : (world - source.Position).Length;
            SelectIfCloser(distance, SceneItemKind.LightSource, index, MoveDragMode.Translate,
                ref bestDistance);
        }

        for (var index = 0; index < _scene.Mirrors.Length; index++)
        {
            var mirror = _scene.Mirrors[index];
            SelectIfCloser(DistanceToSegment(world, mirror.Start, mirror.End), SceneItemKind.Mirror,
                index, MoveDragMode.Translate, ref bestDistance);
        }

        for (var index = 0; index < _scene.LensElements.Length; index++)
        {
            var lens = _scene.LensElements[index];
            SelectIfCloser(DistanceToSegment(world, lens.Start, lens.End), SceneItemKind.Lens,
                index, MoveDragMode.Translate, ref bestDistance);
        }
    }

    private void SelectIfCloser(double distance, SceneItemKind kind, int index, MoveDragMode dragMode,
        ref double bestDistance)
    {
        if (distance > bestDistance)
        {
            return;
        }

        bestDistance = distance;
        _movingKind = kind;
        _movingIndex = index;
        _moveDragMode = dragMode;
    }

    private void MoveSelectedItem(Vector2D world)
    {
        var delta = world - _lastMoveWorld;
        if (delta.LengthSquared <= 1e-12)
        {
            return;
        }

        _lastMoveWorld = world;
        var updated = false;
        switch (_movingKind)
        {
            case SceneItemKind.LightSource:
                var sources = (LightSource[])_scene.LightSources.Clone();
                var source = sources[_movingIndex];
                if (_moveDragMode == MoveDragMode.Translate || source.End is null)
                {
                    sources[_movingIndex] = source with
                    {
                        Position = source.Position + delta,
                        End = source.End is { } translatedEnd ? translatedEnd + delta : null
                    };
                }
                else
                {
                    var start = _moveDragMode == MoveDragMode.StartEndpoint ? world : source.Position;
                    var end = _moveDragMode == MoveDragMode.EndEndpoint ? world : source.End.Value;
                    if (!HasUsableLength(start, end))
                    {
                        return;
                    }
                    var direction = (end - start).Normalized().Perpendicular();
                    sources[_movingIndex] = source with
                    {
                        Position = start,
                        End = end,
                        DirectionDegrees = DirectionDegrees(direction)
                    };
                }
                UpdateScene(_scene with { LightSources = sources });
                updated = true;
                break;
            case SceneItemKind.Mirror:
                var mirrors = (MirrorSegment[])_scene.Mirrors.Clone();
                var mirror = mirrors[_movingIndex];
                var mirrorStart = _moveDragMode == MoveDragMode.StartEndpoint ? world : mirror.Start;
                var mirrorEnd = _moveDragMode == MoveDragMode.EndEndpoint ? world : mirror.End;
                if (_moveDragMode == MoveDragMode.Translate)
                {
                    mirrorStart += delta;
                    mirrorEnd += delta;
                }
                if (!HasUsableLength(mirrorStart, mirrorEnd))
                {
                    return;
                }
                mirrors[_movingIndex] = mirror with { Start = mirrorStart, End = mirrorEnd };
                UpdateScene(_scene with { Mirrors = mirrors });
                updated = true;
                break;
            case SceneItemKind.Lens:
                var lenses = (LensSegment[])_scene.LensElements.Clone();
                var lens = lenses[_movingIndex];
                var lensStart = _moveDragMode == MoveDragMode.StartEndpoint ? world : lens.Start;
                var lensEnd = _moveDragMode == MoveDragMode.EndEndpoint ? world : lens.End;
                if (_moveDragMode == MoveDragMode.Translate)
                {
                    lensStart += delta;
                    lensEnd += delta;
                }
                if (!HasUsableLength(lensStart, lensEnd))
                {
                    return;
                }
                lenses[_movingIndex] = lens with { Start = lensStart, End = lensEnd };
                UpdateScene(_scene with { Lenses = lenses });
                updated = true;
                break;
        }

        if (!updated)
        {
            return;
        }

        _moveChanged = true;
        _moveSimulationDirty = true;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        RecalculateDuringMoveIfDue();
    }

    private bool HasUsableLength(Vector2D start, Vector2D end) =>
        (end - start).Length >= 4 / _zoom;

    private void UpdateScene(OpticalScene scene) =>
        SetAndRaise(SceneProperty, ref _scene, scene);

    private void CommitSelectedEdit()
    {
        Recalculate();
        SceneChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private CanvasSelection? CreateSelection()
    {
        switch (_selectedKind)
        {
            case SceneItemKind.LightSource when IsValidIndex(_selectedIndex, _scene.LightSources):
                var source = _scene.LightSources[_selectedIndex];
                if (source.Kind == LightSourceKind.ParallelLine && source.End is { } sourceEnd)
                {
                    var sourceOrigin = (source.Position + sourceEnd) / 2;
                    return new CanvasSelection(CanvasSelectionKind.ParallelLight, "线平行光源", true,
                        sourceOrigin.X, sourceOrigin.Y,
                        SegmentAngleDegrees(source.Position, sourceEnd), null,
                        (sourceEnd - sourceOrigin).Length * 2);
                }
                return new CanvasSelection(CanvasSelectionKind.PointLight, "点光源", false,
                    source.Position.X, source.Position.Y, 0, null, null);
            case SceneItemKind.Mirror when IsValidIndex(_selectedIndex, _scene.Mirrors):
                var mirror = _scene.Mirrors[_selectedIndex];
                var mirrorOrigin = (mirror.Start + mirror.End) / 2;
                return new CanvasSelection(CanvasSelectionKind.Mirror, "平面反光镜", true,
                    mirrorOrigin.X, mirrorOrigin.Y, SegmentAngleDegrees(mirror.Start, mirror.End), null,
                    (mirror.End - mirrorOrigin).Length * 2);
            case SceneItemKind.Lens when IsValidIndex(_selectedIndex, _scene.LensElements):
                var lens = _scene.LensElements[_selectedIndex];
                var lensOrigin = (lens.Start + lens.End) / 2;
                var selectionKind = lens.Kind == LensKind.Convex
                    ? CanvasSelectionKind.ConvexLens
                    : CanvasSelectionKind.ConcaveLens;
                var displayName = lens.Kind == LensKind.Convex ? "凸透镜" : "凹透镜";
                return new CanvasSelection(selectionKind, displayName, true,
                    lensOrigin.X, lensOrigin.Y, SegmentAngleDegrees(lens.Start, lens.End), lens.FocalLength,
                    (lens.End - lensOrigin).Length * 2);
            default:
                return null;
        }
    }

    private void ClearSelection()
    {
        if (_selectedKind == SceneItemKind.None && _selectedIndex < 0)
        {
            return;
        }

        _selectedKind = SceneItemKind.None;
        _selectedIndex = -1;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    private static bool IsValidIndex<T>(int index, T[] items) =>
        index >= 0 && index < items.Length;

    private static (Vector2D Start, Vector2D End) RotateSegment(
        Vector2D start,
        Vector2D end,
        double radians)
    {
        var midpoint = (start + end) / 2;
        var halfLength = (end - start).Length / 2;
        var offset = Vector2D.FromAngle(radians) * halfLength;
        return (midpoint - offset, midpoint + offset);
    }

    private static (Vector2D Start, Vector2D End) ResizeSegment(
        Vector2D start,
        Vector2D end,
        double length)
    {
        var midpoint = (start + end) / 2;
        var offset = (end - start).Normalized() * (length / 2);
        return (midpoint - offset, midpoint + offset);
    }

    private static double SegmentAngleDegrees(Vector2D start, Vector2D end) =>
        NormalizeDegrees(Math.Atan2(end.Y - start.Y, end.X - start.X) * 180 / Math.PI);

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private void RecalculateDuringMoveIfDue()
    {
        var now = Stopwatch.GetTimestamp();
        var refreshMilliseconds = _result.InitialRayCount > 1500 ? 60 : 33;
        var elapsedMilliseconds = _lastMoveSimulationTimestamp == 0
            ? double.PositiveInfinity
            : (now - _lastMoveSimulationTimestamp) * 1000d / Stopwatch.Frequency;
        if (elapsedMilliseconds < refreshMilliseconds)
        {
            InvalidateVisual();
            return;
        }

        _lastMoveSimulationTimestamp = now;
        _moveSimulationDirty = false;
        Recalculate();
    }

    private static double DistanceToSegment(Vector2D point, Vector2D start, Vector2D end)
    {
        var edge = end - start;
        if (edge.LengthSquared <= 1e-12)
        {
            return (point - start).Length;
        }

        var ratio = Math.Clamp((point - start).Dot(edge) / edge.LengthSquared, 0, 1);
        return (point - (start + edge * ratio)).Length;
    }

    private Vector2D ScreenToWorld(Point point) =>
        new((point.X - _pan.X) / _zoom, (point.Y - _pan.Y) / _zoom);

    private void Recalculate()
    {
        _result = _rayTracer.Trace(_scene, new SimulationOptions(_raysPerSource));
        SimulationCompleted?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    private static double DirectionDegrees(Vector2D direction) =>
        Math.Atan2(direction.Y, direction.X) * 180 / Math.PI;

    private sealed class SkiaDrawOperation(
        Rect bounds,
        OpticalScene scene,
        SimulationResult result,
        Vector2D pan,
        double zoom,
        int raysPerSource,
        CanvasTool tool,
        Vector2D? placementStart,
        Vector2D? placementPreview,
        SceneItemKind selectedKind,
        int selectedIndex) : ICustomDrawOperation
    {
        private const string CoordinateUnit = "p";

        public Rect Bounds { get; } = bounds;
        public void Dispose() { }
        public bool Equals(ICustomDrawOperation? other) => false;
        public bool HitTest(Point point) => Bounds.Contains(point);

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null) return;
            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            canvas.Save();
            canvas.ClipRect(SKRect.Create((float)Bounds.Width, (float)Bounds.Height));
            canvas.Clear(new SKColor(8, 13, 24));
            DrawGrid(canvas);
            DrawAxis(canvas);
            DrawRays(canvas);
            DrawMirrors(canvas);
            DrawLenses(canvas);
            DrawSources(canvas);
            DrawPlacementPreview(canvas);
            DrawLegend(canvas);
            canvas.Restore();
        }

        private SKPoint ToScreen(Vector2D point) =>
            new((float)(pan.X + point.X * zoom), (float)(pan.Y + point.Y * zoom));

        private void DrawGrid(SKCanvas canvas)
        {
            var gridWorld = SelectGridStep(zoom);
            var gridScreen = gridWorld * zoom;
            var startX = PositiveModulo(pan.X, gridScreen);
            var startY = PositiveModulo(pan.Y, gridScreen);
            using var paint = new SKPaint { Color = new SKColor(43, 57, 78, 95), StrokeWidth = 1, IsAntialias = false };
            for (var x = startX; x < Bounds.Width; x += gridScreen) canvas.DrawLine((float)x, 0, (float)x, (float)Bounds.Height, paint);
            for (var y = startY; y < Bounds.Height; y += gridScreen) canvas.DrawLine(0, (float)y, (float)Bounds.Width, (float)y, paint);
        }

        private void DrawAxis(SKCanvas canvas)
        {
            var step = SelectGridStep(zoom);
            var horizontalAxisVisible = pan.Y >= 0 && pan.Y <= Bounds.Height;
            var verticalAxisVisible = pan.X >= 0 && pan.X <= Bounds.Width;
            using var axisPaint = new SKPaint
            {
                Color = new SKColor(122, 145, 180, 210),
                StrokeWidth = 1.2f,
                IsAntialias = true
            };
            using var tickPaint = new SKPaint
            {
                Color = new SKColor(150, 170, 201, 225),
                StrokeWidth = 1,
                IsAntialias = true
            };
            using var textPaint = new SKPaint
            {
                Color = new SKColor(184, 201, 226, 235),
                IsAntialias = true
            };
            using var font = new SKFont(SKTypeface.Default, 11);

            if (horizontalAxisVisible)
            {
                canvas.DrawLine(0, (float)pan.Y, (float)Bounds.Width, (float)pan.Y, axisPaint);
                DrawHorizontalTicks(canvas, step, font, textPaint, tickPaint);
            }

            if (verticalAxisVisible)
            {
                canvas.DrawLine((float)pan.X, 0, (float)pan.X, (float)Bounds.Height, axisPaint);
                DrawVerticalTicks(canvas, step, font, textPaint, tickPaint, horizontalAxisVisible);
            }
        }

        private void DrawHorizontalTicks(
            SKCanvas canvas,
            double step,
            SKFont font,
            SKPaint textPaint,
            SKPaint tickPaint)
        {
            var minimum = -pan.X / zoom;
            var maximum = (Bounds.Width - pan.X) / zoom;
            var firstTick = Math.Ceiling(minimum / step) * step;
            var labelsBelowAxis = pan.Y <= Bounds.Height - 18;
            var labelBaseline = (float)(labelsBelowAxis ? pan.Y + 15 : pan.Y - 6);
            var previousLabelRight = double.NegativeInfinity;

            for (var coordinate = firstTick; coordinate <= maximum + step * 1e-9; coordinate += step)
            {
                var x = (float)(pan.X + coordinate * zoom);
                canvas.DrawLine(x, (float)pan.Y - 4, x, (float)pan.Y + 4, tickPaint);
                if (Math.Abs(coordinate) < step * 1e-9)
                {
                    continue;
                }

                var label = FormatCoordinate(coordinate);
                var labelWidth = font.MeasureText(label, textPaint);
                var labelLeft = x - labelWidth / 2;
                var labelRight = x + labelWidth / 2;
                if (labelLeft < 2 || labelRight > Bounds.Width - 2 || labelLeft < previousLabelRight + 6)
                {
                    continue;
                }

                canvas.DrawText(label, x, labelBaseline, SKTextAlign.Center, font, textPaint);
                previousLabelRight = labelRight;
            }
        }

        private void DrawVerticalTicks(
            SKCanvas canvas,
            double step,
            SKFont font,
            SKPaint textPaint,
            SKPaint tickPaint,
            bool horizontalAxisVisible)
        {
            var minimum = -pan.Y / zoom;
            var maximum = (Bounds.Height - pan.Y) / zoom;
            var firstTick = Math.Ceiling(minimum / step) * step;
            var labelsRightOfAxis = pan.X <= Bounds.Width - 48;
            var labelX = (float)(labelsRightOfAxis ? pan.X + 7 : pan.X - 7);
            var alignment = labelsRightOfAxis ? SKTextAlign.Left : SKTextAlign.Right;

            for (var coordinate = firstTick; coordinate <= maximum + step * 1e-9; coordinate += step)
            {
                var y = (float)(pan.Y + coordinate * zoom);
                canvas.DrawLine((float)pan.X - 4, y, (float)pan.X + 4, y, tickPaint);
                if (Math.Abs(coordinate) < step * 1e-9)
                {
                    continue;
                }

                if (y < 12 || y > Bounds.Height - 2)
                {
                    continue;
                }

                var label = FormatCoordinate(coordinate);
                canvas.DrawText(label, labelX, y - 3, alignment, font, textPaint);
            }

            if (horizontalAxisVisible)
            {
                var originX = (float)Math.Clamp(pan.X + 6, 2, Bounds.Width - 24);
                var originY = (float)Math.Clamp(pan.Y - 6, 11, Bounds.Height - 3);
                canvas.DrawText($"0{CoordinateUnit}", originX, originY, SKTextAlign.Left, font, textPaint);
            }
        }

        private static string FormatCoordinate(double coordinate) =>
            $"{Math.Round(coordinate).ToString(CultureInfo.InvariantCulture)}{CoordinateUnit}";

        private void DrawRays(SKCanvas canvas)
        {
            using var paint = new SKPaint
            {
                Color = new SKColor(255, 224, 92, result.InitialRayCount > 1200 ? (byte)115 : (byte)180),
                StrokeWidth = Math.Max(0.8f, (float)(1.05 * Math.Sqrt(zoom))),
                Style = SKPaintStyle.Stroke,
                IsAntialias = result.InitialRayCount <= 1200,
                StrokeCap = SKStrokeCap.Round,
                BlendMode = SKBlendMode.SrcOver
            };
            using var path = new SKPath();
            foreach (var segment in result.Segments)
            {
                path.MoveTo(ToScreen(segment.Start));
                path.LineTo(ToScreen(segment.End));
            }
            if (result.InitialRayCount <= 600)
            {
                using var glow = new SKPaint
                {
                    Color = new SKColor(255, 192, 58, 32),
                    StrokeWidth = Math.Max(2.2f, (float)(2.8 * Math.Sqrt(zoom))),
                    Style = SKPaintStyle.Stroke,
                    IsAntialias = true,
                    StrokeCap = SKStrokeCap.Round,
                    BlendMode = SKBlendMode.SrcOver
                };
                canvas.DrawPath(path, glow);
            }
            canvas.DrawPath(path, paint);
        }

        private void DrawMirrors(SKCanvas canvas)
        {
            using var glow = SegmentPaint(new SKColor(47, 213, 255, 50), 9);
            using var paint = SegmentPaint(new SKColor(117, 225, 255), 3);
            for (var index = 0; index < scene.Mirrors.Length; index++)
            {
                var mirror = scene.Mirrors[index];
                canvas.DrawLine(ToScreen(mirror.Start), ToScreen(mirror.End), glow);
                canvas.DrawLine(ToScreen(mirror.Start), ToScreen(mirror.End), paint);
                if (tool == CanvasTool.Move)
                {
                    DrawHandles(canvas, mirror.Start, mirror.End, paint);
                    DrawOrigin(canvas, (mirror.Start + mirror.End) / 2,
                        selectedKind == SceneItemKind.Mirror && selectedIndex == index);
                }
            }
        }

        private void DrawLenses(SKCanvas canvas)
        {
            for (var index = 0; index < scene.LensElements.Length; index++)
            {
                var lens = scene.LensElements[index];
                var color = lens.Kind == LensKind.Convex ? new SKColor(101, 238, 196) : new SKColor(183, 142, 255);
                using var glow = SegmentPaint(color.WithAlpha(45), 11);
                using var paint = SegmentPaint(color, 3);
                var tangent = (lens.End - lens.Start).Normalized();
                var arrowInset = Math.Min(12 / zoom, (lens.End - lens.Start).Length * 0.3);
                var bodyStart = lens.Kind == LensKind.Concave
                    ? lens.Start + tangent * arrowInset
                    : lens.Start;
                var bodyEnd = lens.Kind == LensKind.Concave
                    ? lens.End - tangent * arrowInset
                    : lens.End;
                canvas.DrawLine(ToScreen(bodyStart), ToScreen(bodyEnd), glow);
                canvas.DrawLine(ToScreen(bodyStart), ToScreen(bodyEnd), paint);
                DrawLensArrows(canvas, lens, paint, arrowInset);
                if (tool == CanvasTool.Move)
                {
                    DrawHandles(canvas, lens.Start, lens.End, paint);
                    DrawOrigin(canvas, (lens.Start + lens.End) / 2,
                        selectedKind == SceneItemKind.Lens && selectedIndex == index);
                }
            }
        }

        private void DrawLensArrows(SKCanvas canvas, LensSegment lens, SKPaint paint, double arrowInset)
        {
            var tangent = (lens.End - lens.Start).Normalized();
            var normal = tangent.Perpendicular();
            var amount = Math.Min(9 / zoom, (lens.End - lens.Start).Length * 0.22);
            var isConvex = lens.Kind == LensKind.Convex;
            foreach (var endpoint in new[] { lens.Start, lens.End })
            {
                var inward = endpoint == lens.Start ? tangent : -tangent;
                var innerPoint = endpoint + inward * arrowInset;
                var wingA = endpoint + normal * amount;
                var wingB = endpoint - normal * amount;
                if (isConvex)
                {
                    // Convex (converging) lens: arrowhead vertex sits at the endpoint, wings splay inward.
                    canvas.DrawLine(ToScreen(endpoint), ToScreen(innerPoint + normal * amount), paint);
                    canvas.DrawLine(ToScreen(endpoint), ToScreen(innerPoint - normal * amount), paint);
                }
                else
                {
                    // Concave (diverging) lens: arrowhead vertex sits inward, wings splay out to the endpoint.
                    canvas.DrawLine(ToScreen(innerPoint), ToScreen(wingA), paint);
                    canvas.DrawLine(ToScreen(innerPoint), ToScreen(wingB), paint);
                }
            }
        }

        private void DrawSources(SKCanvas canvas)
        {
            using var fill = new SKPaint { Color = new SKColor(255, 208, 62), Style = SKPaintStyle.Fill, IsAntialias = true };
            using var outline = new SKPaint { Color = new SKColor(255, 245, 188), Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
            for (var index = 0; index < scene.LightSources.Length; index++)
            {
                var source = scene.LightSources[index];
                if (source.Kind == LightSourceKind.ParallelLine && source.End is { } end)
                {
                    canvas.DrawLine(ToScreen(source.Position), ToScreen(end), outline);
                    canvas.DrawCircle(ToScreen(source.Position), 4.5f, outline);
                    canvas.DrawCircle(ToScreen(end), 4.5f, outline);
                    var middle = (source.Position + end) / 2;
                    var direction = Vector2D.FromAngle(source.DirectionDegrees * Math.PI / 180);
                    DrawArrow(canvas, middle, middle + direction * (34 / zoom), outline);
                    if (tool == CanvasTool.Move)
                    {
                        DrawOrigin(canvas, middle,
                            selectedKind == SceneItemKind.LightSource && selectedIndex == index);
                    }
                }
                else
                {
                    var position = ToScreen(source.Position);
                    canvas.DrawCircle(position, 8, fill);
                    canvas.DrawCircle(position, 12, outline);
                    if (tool == CanvasTool.Move)
                    {
                        DrawOrigin(canvas, source.Position,
                            selectedKind == SceneItemKind.LightSource && selectedIndex == index);
                    }
                }
            }
        }

        private void DrawPlacementPreview(SKCanvas canvas)
        {
            if (placementStart is not { } start || placementPreview is not { } end) return;
            using var preview = new SKPaint { Color = new SKColor(255, 255, 255, 185), StrokeWidth = 2, IsAntialias = true, PathEffect = SKPathEffect.CreateDash([8, 6], 0) };
            canvas.DrawLine(ToScreen(start), ToScreen(end), preview);
            canvas.DrawCircle(ToScreen(start), 5, preview);
            canvas.DrawCircle(ToScreen(end), 5, preview);
            if (tool == CanvasTool.ParallelLight)
            {
                var middle = (start + end) / 2;
                DrawArrow(canvas, middle, middle + (end - start).Normalized().Perpendicular() * (40 / zoom), preview);
            }
        }

        private void DrawLegend(SKCanvas canvas)
        {
            using var paint = new SKPaint { Color = new SKColor(195, 209, 229), IsAntialias = true };
            using var matchedTypeface = SKFontManager.Default.MatchCharacter('光');
            using var font = new SKFont(matchedTypeface ?? SKTypeface.Default, 14);
            canvas.DrawText($"{scene.Name}  ·  每光源 {raysPerSource} 条 / 共 {result.InitialRayCount} 条  ·  {result.ReflectedRayCount} 次反射  ·  {result.RefractedRayCount} 次折射",
                18, 28, SKTextAlign.Left, font, paint);
        }

        private SKPaint SegmentPaint(SKColor color, double width) => new()
        {
            Color = color,
            StrokeWidth = (float)Math.Clamp(width * Math.Sqrt(zoom), 2, width * 2),
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };

        private void DrawHandles(SKCanvas canvas, Vector2D start, Vector2D end, SKPaint paint)
        {
            canvas.DrawCircle(ToScreen(start), 4.5f, paint);
            canvas.DrawCircle(ToScreen(end), 4.5f, paint);
        }

        private void DrawOrigin(SKCanvas canvas, Vector2D origin, bool isSelected)
        {
            var point = ToScreen(origin);
            using var paint = new SKPaint
            {
                Color = isSelected ? new SKColor(255, 255, 255) : new SKColor(151, 190, 220, 175),
                StrokeWidth = isSelected ? 1.8f : 1.2f,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };
            canvas.DrawCircle(point, isSelected ? 5.5f : 4.5f, paint);
            canvas.DrawLine(point.X - 8, point.Y, point.X + 8, point.Y, paint);
            canvas.DrawLine(point.X, point.Y - 8, point.X, point.Y + 8, paint);
        }

        private void DrawArrow(SKCanvas canvas, Vector2D start, Vector2D end, SKPaint paint)
        {
            canvas.DrawLine(ToScreen(start), ToScreen(end), paint);
            var direction = (end - start).Normalized();
            var side = direction.Perpendicular();
            var size = 7 / zoom;
            canvas.DrawLine(ToScreen(end), ToScreen(end - direction * size + side * size * 0.55), paint);
            canvas.DrawLine(ToScreen(end), ToScreen(end - direction * size - side * size * 0.55), paint);
        }

        private static double SelectGridStep(double currentZoom)
        {
            var steps = new[] { 10d, 20d, 50d, 100d, 200d, 500d, 1000d };
            return steps.First(step => step * currentZoom >= 24);
        }

        private static double PositiveModulo(double value, double modulus) => ((value % modulus) + modulus) % modulus;
    }
}
