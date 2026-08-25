using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using LightDraw.Core.Electromagnetics;
using LightDraw.Core.Geometry;

namespace LightDraw.Rendering.Skia;

public enum ElectrostaticTool { Pan, Move, Delete, PointCharge, ChargedPlate }
public enum ElectrostaticSelectionKind { PointCharge, ChargedPlate }
public sealed record ElectrostaticSelection(ElectrostaticSelectionKind Kind, int Index, double X, double Y,
    double? ChargeNanocoulombs = null, double? PotentialVolts = null, double? Length = null,
    double? AngleDegrees = null);

public sealed class ElectrostaticCanvas : Control
{
    public static readonly DirectProperty<ElectrostaticCanvas, ElectrostaticScene> SceneProperty =
        AvaloniaProperty.RegisterDirect<ElectrostaticCanvas, ElectrostaticScene>(nameof(Scene), c => c.Scene,
            (c, v) => c.SetScene(v), defaultBindingMode: BindingMode.TwoWay);
    public static readonly DirectProperty<ElectrostaticCanvas, ElectrostaticTool> ActiveToolProperty =
        AvaloniaProperty.RegisterDirect<ElectrostaticCanvas, ElectrostaticTool>(nameof(ActiveTool), c => c.ActiveTool,
            (c, v) => c.SelectTool(v), defaultBindingMode: BindingMode.TwoWay);
    public static readonly DirectProperty<ElectrostaticCanvas, int> LinesPerChargeProperty =
        AvaloniaProperty.RegisterDirect<ElectrostaticCanvas, int>(nameof(LinesPerCharge), c => c.LinesPerCharge,
            (c, v) => c.SetLinesPerCharge(v));

    private static readonly IBrush BackgroundBrush = Brush("#08111F");
    private static readonly IBrush TextBrush = Brush("#B8C9E2");
    private static readonly Pen MinorGridPen = Pen("#15243A", 1);
    private static readonly Pen MajorGridPen = Pen("#233955", 1);
    private static readonly Pen AxisPen = Pen("#3C5878", 1.2);
    private static readonly Pen TickPen = Pen("#96AAC9", 1);
    private static readonly Pen FieldPen = Pen("#72D9FF", 1.35);
    private static readonly Pen SelectionPen = Pen("#FFFFFF", 2);
    private static readonly Pen PositivePlatePen = Pen("#FF6B78", 5);
    private static readonly Pen NegativePlatePen = Pen("#5B9BFF", 5);
    private static readonly Pen ZeroPlatePen = Pen("#A0AEC0", 5);
    private static readonly Pen PreviewPen = new(Brush("#A6EEFF"), 2, DashStyle.Dash);
    private static readonly IBrush PositiveBrush = Brush("#F05262");
    private static readonly IBrush NegativeBrush = Brush("#3B82F6");
    private static readonly IBrush NeutralBrush = Brush("#77869C");
    private static readonly Pen ChargeSignPen = new(Brushes.White, 2.2);

    private readonly ElectrostaticSimulator _simulator = new();
    private ElectrostaticScene _scene = ElectrostaticScene.CreateEmpty();
    private ElectrostaticSimulationResult _result = new([], 0, TimeSpan.Zero);
    private ElectrostaticTool _activeTool = ElectrostaticTool.Pan;
    private int _linesPerCharge = 24;
    private Vector2D _pan = new(560, 360);
    private double _zoom = 1;
    private bool _isPanning, _isMoving, _moveChanged, _moveSimulationDirty;
    private long _lastMoveSimulationTimestamp;
    private Point _lastPointer;
    private ElectrostaticSelectionKind? _selectedKind;
    private int _selectedIndex = -1;
    private DragMode _dragMode;
    private Vector2D _moveOffset, _dragStart;
    private ChargedPlate? _dragOriginalPlate;
    private Vector2D? _plateStart;
    private Vector2D _platePreviewEnd;

    public ElectrostaticCanvas() { ClipToBounds = true; Focusable = true; Recalculate(); }
    public ElectrostaticScene Scene { get => _scene; set => SetScene(value); }
    public ElectrostaticTool ActiveTool { get => _activeTool; set => SelectTool(value); }
    public int LinesPerCharge { get => _linesPerCharge; set => SetLinesPerCharge(value); }
    public ElectrostaticSimulationResult SimulationResult => _result;
    public ElectrostaticSelection? Selection
    {
        get
        {
            if (_selectedKind == ElectrostaticSelectionKind.PointCharge && IsIndex(_selectedIndex, _scene.Charges))
            {
                var q = _scene.Charges[_selectedIndex];
                return new(_selectedKind.Value, _selectedIndex, q.Position.X, q.Position.Y, q.ChargeNanocoulombs);
            }
            if (_selectedKind == ElectrostaticSelectionKind.ChargedPlate && IsIndex(_selectedIndex, _scene.PlateElements))
            {
                var p = _scene.PlateElements[_selectedIndex];
                var center = (p.Start + p.End) / 2;
                return new(_selectedKind.Value, _selectedIndex, center.X, center.Y, null,
                    p.PotentialVolts, (p.End - p.Start).Length,
                    NormalizeDegrees(Math.Atan2(p.End.Y - p.Start.Y, p.End.X - p.Start.X) * 180 / Math.PI));
            }
            return null;
        }
    }

    public event EventHandler? SceneChanged;
    public event EventHandler? SimulationCompleted;
    public event EventHandler? ToolStateChanged;
    public event EventHandler? SelectionChanged;

    public void SetScene(ElectrostaticScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (ReferenceEquals(scene, _scene)) return;
        SetAndRaise(SceneProperty, ref _scene, scene); SetSelection(null, -1); Recalculate();
    }
    public void SelectTool(ElectrostaticTool tool)
    {
        SetAndRaise(ActiveToolProperty, ref _activeTool, tool);
        if (tool != ElectrostaticTool.Move) SetSelection(null, -1);
        if (tool != ElectrostaticTool.ChargedPlate) _plateStart = null;
        ToolStateChanged?.Invoke(this, EventArgs.Empty); InvalidateVisual();
    }
    public void SetLinesPerCharge(int value)
    {
        value = Math.Clamp(value, 8, 96); if (value == _linesPerCharge) return;
        SetAndRaise(LinesPerChargeProperty, ref _linesPerCharge, value); Recalculate();
    }
    public void ResetView()
    {
        _zoom = 1; _pan = new(Math.Max(420, Bounds.Width * .5), Math.Max(300, Bounds.Height * .52)); InvalidateVisual();
    }
    public void SetSelectedCharge(double value)
    {
        if (Selection is not { Kind: ElectrostaticSelectionKind.PointCharge } s) return;
        var items = _scene.Charges.ToArray(); items[s.Index] = items[s.Index] with { ChargeNanocoulombs = Math.Clamp(value, -1e6, 1e6) };
        CommitScene(_scene with { Charges = items });
    }
    public void SetSelectedPotential(double value)
    {
        if (Selection is not { Kind: ElectrostaticSelectionKind.ChargedPlate } s) return;
        var items = _scene.PlateElements.ToArray(); items[s.Index] = items[s.Index] with { PotentialVolts = Math.Clamp(value, -1e7, 1e7) };
        CommitScene(_scene with { Plates = items });
    }
    public void SetSelectedPlateLength(double value)
    {
        if (Selection is not { Kind: ElectrostaticSelectionKind.ChargedPlate } s || !double.IsFinite(value)) return;
        var items = _scene.PlateElements.ToArray();
        var plate = items[s.Index];
        var direction = (plate.End - plate.Start).Normalized();
        if (direction.LengthSquared < 1e-12) direction = new Vector2D(1, 0);
        var center = (plate.Start + plate.End) / 2;
        var half = direction * (Math.Clamp(value, 10, 100_000) / 2);
        items[s.Index] = plate with { Start = center - half, End = center + half };
        CommitScene(_scene with { Plates = items });
    }
    public void SetSelectedPlateAngle(double degrees)
    {
        if (Selection is not { Kind: ElectrostaticSelectionKind.ChargedPlate } s || !double.IsFinite(degrees)) return;
        var items = _scene.PlateElements.ToArray();
        var plate = items[s.Index];
        var center = (plate.Start + plate.End) / 2;
        var half = Vector2D.FromAngle(degrees * Math.PI / 180) * ((plate.End - plate.Start).Length / 2);
        items[s.Index] = plate with { Start = center - half, End = center + half };
        CommitScene(_scene with { Plates = items });
    }
    public void SetSelectedOrigin(double x, double y)
    {
        if (Selection is not { } s || !double.IsFinite(x) || !double.IsFinite(y)) return;
        var target = new Vector2D(x, y);
        if (s.Kind == ElectrostaticSelectionKind.PointCharge)
        {
            var items = _scene.Charges.ToArray(); items[s.Index] = items[s.Index] with { Position = target };
            CommitScene(_scene with { Charges = items });
        }
        else
        {
            var items = _scene.PlateElements.ToArray(); var p = items[s.Index]; var d = target - (p.Start + p.End) / 2;
            items[s.Index] = p with { Start = p.Start + d, End = p.End + d }; CommitScene(_scene with { Plates = items });
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context); context.FillRectangle(BackgroundBrush, new Rect(Bounds.Size)); DrawGrid(context);
        foreach (var line in _result.FieldLines) DrawFieldLine(context, line);
        for (var i = 0; i < _scene.PlateElements.Length; i++) DrawPlate(context, _scene.PlateElements[i], i);
        for (var i = 0; i < _scene.Charges.Length; i++) DrawCharge(context, _scene.Charges[i], i);
        if (_plateStart is { } start) { context.DrawLine(PreviewPen, ToScreen(start), ToScreen(_platePreviewEnd)); DrawHandle(context, start); DrawHandle(context, _platePreviewEnd); }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); Focus(); var screen = e.GetPosition(this); var props = e.GetCurrentPoint(this).Properties;
        if (props.IsRightButtonPressed || props.IsMiddleButtonPressed || (_activeTool == ElectrostaticTool.Pan && props.IsLeftButtonPressed))
        { _isPanning = true; _lastPointer = screen; e.Pointer.Capture(this); }
        else if (props.IsLeftButtonPressed)
        {
            var world = ToWorld(screen); var hit = HitTest(world);
            switch (_activeTool)
            {
                case ElectrostaticTool.PointCharge:
                    CommitScene(_scene with { Charges = [.. _scene.Charges, new PointCharge(world)] });
                    SelectTool(ElectrostaticTool.Move); SetSelection(ElectrostaticSelectionKind.PointCharge, _scene.Charges.Length - 1); break;
                case ElectrostaticTool.ChargedPlate when _plateStart is null:
                    _plateStart = world; _platePreviewEnd = world; break;
                case ElectrostaticTool.ChargedPlate when (world - _plateStart.Value).Length >= 10:
                    CommitScene(_scene with { Plates = [.. _scene.PlateElements, new ChargedPlate(_plateStart.Value, world)] });
                    _plateStart = null; SelectTool(ElectrostaticTool.Move); SetSelection(ElectrostaticSelectionKind.ChargedPlate, _scene.PlateElements.Length - 1); break;
                case ElectrostaticTool.Move: BeginMove(hit, world, e); break;
                case ElectrostaticTool.Delete when hit is { } h: DeleteElement(h); SelectTool(ElectrostaticTool.Pan); break;
            }
        }
        InvalidateVisual(); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); var screen = e.GetPosition(this); var world = ToWorld(screen);
        if (_isPanning) { _pan += new Vector2D(screen.X - _lastPointer.X, screen.Y - _lastPointer.Y); _lastPointer = screen; InvalidateVisual(); }
        else if (_isMoving && Selection is { } s)
        { MoveSelection(s, world); _moveChanged = _moveSimulationDirty = true; SelectionChanged?.Invoke(this, EventArgs.Empty); RecalculateDuringMoveIfDue(); InvalidateVisual(); }
        else if (_plateStart is not null) { _platePreviewEnd = world; InvalidateVisual(); }
        e.Handled = true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); _isPanning = false;
        if (_isMoving)
        {
            _isMoving = false; if (_moveChanged) { if (_moveSimulationDirty) Recalculate(); SceneChanged?.Invoke(this, EventArgs.Empty); }
            _moveChanged = _moveSimulationDirty = false;
        }
        e.Pointer.Capture(null); e.Handled = true;
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e); var p = e.GetPosition(this); var before = ToWorld(p);
        _zoom = Math.Clamp(_zoom * Math.Pow(1.12, e.Delta.Y), .15, 8); _pan = new(p.X - before.X * _zoom, p.Y + before.Y * _zoom);
        InvalidateVisual(); e.Handled = true;
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e); if (e.Key != Key.Escape) return; _plateStart = null; SelectTool(ElectrostaticTool.Pan); e.Handled = true;
    }

    private void BeginMove(ElementHit? hit, Vector2D world, PointerPressedEventArgs e)
    {
        if (hit is not { } h) { SetSelection(null, -1); return; }
        SetSelection(h.Kind, h.Index); _dragMode = h.Mode; _isMoving = true; _moveChanged = _moveSimulationDirty = false;
        _lastMoveSimulationTimestamp = 0; _dragStart = world;
        if (h.Kind == ElectrostaticSelectionKind.PointCharge) _moveOffset = _scene.Charges[h.Index].Position - world;
        else _dragOriginalPlate = _scene.PlateElements[h.Index];
        e.Pointer.Capture(this);
    }
    private void MoveSelection(ElectrostaticSelection s, Vector2D world)
    {
        if (s.Kind == ElectrostaticSelectionKind.PointCharge)
        {
            var items = _scene.Charges.ToArray(); items[s.Index] = items[s.Index] with { Position = world + _moveOffset };
            SetAndRaise(SceneProperty, ref _scene, _scene with { Charges = items }); return;
        }
        if (_dragOriginalPlate is not { } original) return;
        var plates = _scene.PlateElements.ToArray(); plates[s.Index] = _dragMode switch
        { DragMode.Start => original with { Start = world }, DragMode.End => original with { End = world }, _ => original with { Start = original.Start + world - _dragStart, End = original.End + world - _dragStart } };
        SetAndRaise(SceneProperty, ref _scene, _scene with { Plates = plates });
    }

    private void DrawGrid(DrawingContext context)
    {
        const double spacing = 50; var left = ToWorld(new(0, 0)).X; var right = ToWorld(new(Bounds.Width, 0)).X;
        var top = ToWorld(new(0, 0)).Y; var bottom = ToWorld(new(0, Bounds.Height)).Y;
        for (var x = Math.Floor(left / spacing) * spacing; x <= right; x += spacing)
            context.DrawLine(Math.Abs(x % 250) < .01 ? MajorGridPen : MinorGridPen, ToScreen(new(x, top)), ToScreen(new(x, bottom)));
        for (var y = Math.Floor(bottom / spacing) * spacing; y <= top; y += spacing)
            context.DrawLine(Math.Abs(y % 250) < .01 ? MajorGridPen : MinorGridPen, ToScreen(new(left, y)), ToScreen(new(right, y)));
        context.DrawLine(AxisPen, ToScreen(new(0, top)), ToScreen(new(0, bottom))); context.DrawLine(AxisPen, ToScreen(new(left, 0)), ToScreen(new(right, 0)));
        DrawTicks(context, left, right, bottom, top);
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
    private static void DrawCoordinate(DrawingContext c, double value, Point point) => c.DrawText(Text(Math.Round(value).ToString(CultureInfo.InvariantCulture), 11), point);

    private void DrawFieldLine(DrawingContext context, ElectricFieldLine line)
    {
        for (var i = 1; i < line.Points.Count; i++) context.DrawLine(FieldPen, ToScreen(line.Points[i - 1]), ToScreen(line.Points[i]));
        if (line.Points.Count < 2) return;
        var finite = line.Termination is ElectricFieldLineTermination.Charge or ElectricFieldLineTermination.Conductor;
        var point = finite ? PointAtDistance(line.Points, PolylineLength(line.Points) / 2) : PointAtRadius(line.Points, line.SourcePosition, 200);
        var direction = _simulator.ElectricFieldAt(point, _result.EffectiveCharges).Normalized(); if (direction.LengthSquared < 1e-12) return;
        var tip = ToScreen(point); var back = direction * (-8 / _zoom); var wing = direction.Perpendicular() * (4 / _zoom);
        context.DrawLine(FieldPen, tip, ToScreen(point + back + wing)); context.DrawLine(FieldPen, tip, ToScreen(point + back - wing));
    }
    private void DrawPlate(DrawingContext c, ChargedPlate p, int index)
    {
        var pen = p.PotentialVolts > 1e-9 ? PositivePlatePen : p.PotentialVolts < -1e-9 ? NegativePlatePen : ZeroPlatePen;
        c.DrawLine(pen, ToScreen(p.Start), ToScreen(p.End)); var center = ToScreen((p.Start + p.End) / 2);
        c.DrawText(Text($"{p.PotentialVolts:G5} V", 11), new(center.X + 7, center.Y - 21));
        if (_selectedKind != ElectrostaticSelectionKind.ChargedPlate || _selectedIndex != index) return;
        c.DrawLine(SelectionPen, ToScreen(p.Start), ToScreen(p.End)); DrawHandle(c, p.Start); DrawHandle(c, p.End);
    }
    private void DrawHandle(DrawingContext c, Vector2D p) => c.DrawEllipse(Brushes.White, TickPen, ToScreen(p), 5, 5);
    private void DrawCharge(DrawingContext c, PointCharge q, int index)
    {
        var center = ToScreen(q.Position); var brush = q.ChargeNanocoulombs > 0 ? PositiveBrush : q.ChargeNanocoulombs < 0 ? NegativeBrush : NeutralBrush;
        c.DrawEllipse(brush, null, center, 12, 12); c.DrawLine(ChargeSignPen, new(center.X - 5, center.Y), new(center.X + 5, center.Y));
        if (q.ChargeNanocoulombs > 0) c.DrawLine(ChargeSignPen, new(center.X, center.Y - 5), new(center.X, center.Y + 5));
        if (_selectedKind == ElectrostaticSelectionKind.PointCharge && _selectedIndex == index) c.DrawEllipse(null, SelectionPen, center, 17, 17);
    }

    private ElementHit? HitTest(Vector2D world)
    {
        var endpoint = 12 / _zoom;
        for (var i = _scene.PlateElements.Length - 1; i >= 0; i--)
        { var p = _scene.PlateElements[i]; if ((world - p.Start).Length <= endpoint) return new(ElectrostaticSelectionKind.ChargedPlate, i, DragMode.Start); if ((world - p.End).Length <= endpoint) return new(ElectrostaticSelectionKind.ChargedPlate, i, DragMode.End); }
        for (var i = _scene.Charges.Length - 1; i >= 0; i--) if ((world - _scene.Charges[i].Position).Length <= 18 / _zoom) return new(ElectrostaticSelectionKind.PointCharge, i, DragMode.Body);
        for (var i = _scene.PlateElements.Length - 1; i >= 0; i--) { var p = _scene.PlateElements[i]; if (DistanceToSegment(world, p.Start, p.End) <= 10 / _zoom) return new(ElectrostaticSelectionKind.ChargedPlate, i, DragMode.Body); }
        return null;
    }
    private void DeleteElement(ElementHit h)
    {
        SetSelection(null, -1);
        if (h.Kind == ElectrostaticSelectionKind.PointCharge) CommitScene(_scene with { Charges = _scene.Charges.Where((_, i) => i != h.Index).ToArray() });
        else CommitScene(_scene with { Plates = _scene.PlateElements.Where((_, i) => i != h.Index).ToArray() });
    }
    private void SetSelection(ElectrostaticSelectionKind? kind, int index)
    { if (_selectedKind == kind && _selectedIndex == index) return; _selectedKind = kind; _selectedIndex = index; SelectionChanged?.Invoke(this, EventArgs.Empty); InvalidateVisual(); }
    private void CommitScene(ElectrostaticScene scene)
    { SetAndRaise(SceneProperty, ref _scene, scene); Recalculate(); SceneChanged?.Invoke(this, EventArgs.Empty); SelectionChanged?.Invoke(this, EventArgs.Empty); }
    private void Recalculate()
    { _result = _simulator.Simulate(_scene, new(_linesPerCharge)); SimulationCompleted?.Invoke(this, EventArgs.Empty); InvalidateVisual(); }
    private void RecalculateDuringMoveIfDue()
    {
        var now = Stopwatch.GetTimestamp(); var elapsed = _lastMoveSimulationTimestamp == 0 ? double.PositiveInfinity : (now - _lastMoveSimulationTimestamp) * 1000d / Stopwatch.Frequency;
        if (elapsed < 33) return; _lastMoveSimulationTimestamp = now; _moveSimulationDirty = false; Recalculate();
    }

    private static double PolylineLength(IReadOnlyList<Vector2D> points) { var n = 0d; for (var i = 1; i < points.Count; i++) n += (points[i] - points[i - 1]).Length; return n; }
    private static Vector2D PointAtDistance(IReadOnlyList<Vector2D> p, double distance)
    { var n = 0d; for (var i = 1; i < p.Count; i++) { var d = p[i] - p[i - 1]; if (n + d.Length >= distance) return p[i - 1] + d * ((distance - n) / d.Length); n += d.Length; } return p[^1]; }
    private static Vector2D PointAtRadius(IReadOnlyList<Vector2D> p, Vector2D source, double radius)
    { for (var i = 1; i < p.Count; i++) { var a = (p[i - 1] - source).Length; var b = (p[i] - source).Length; if (a <= radius && b >= radius) return p[i - 1] + (p[i] - p[i - 1]) * ((radius - a) / Math.Max(b - a, 1e-12)); } return p[^1]; }
    private static double DistanceToSegment(Vector2D p, Vector2D a, Vector2D b) { var d = b - a; if (d.LengthSquared < 1e-12) return (p - a).Length; return (p - (a + d * Math.Clamp((p - a).Dot(d) / d.LengthSquared, 0, 1))).Length; }
    private static bool IsIndex<T>(int index, IReadOnlyList<T> items) => index >= 0 && index < items.Count;
    private static double NormalizeDegrees(double degrees) => (degrees % 360 + 360) % 360;
    private Point ToScreen(Vector2D p) => new(p.X * _zoom + _pan.X, _pan.Y - p.Y * _zoom);
    private Vector2D ToWorld(Point p) => new((p.X - _pan.X) / _zoom, (_pan.Y - p.Y) / _zoom);
    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
    private static Pen Pen(string color, double width) => new(Brush(color), width);
    private static FormattedText Text(string value, double size) => new(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new("Inter"), size, TextBrush);
    private readonly record struct ElementHit(ElectrostaticSelectionKind Kind, int Index, DragMode Mode);
    private enum DragMode { Body, Start, End }
}
