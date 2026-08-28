using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using System.Globalization;
using LightDraw.Core.Geometry;
using LightDraw.Core.Scene;
using LightDraw.Core.Simulation;
using SkiaSharp;

namespace LightDraw.Rendering.Skia.Optics;

internal sealed class SkiaSceneDrawOperation(
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
    private const string CoordinateUnit = "mm";
    private const double RotationHandleOffset = 100;

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
        DrawConcaveSphericalMirrors(canvas);
        DrawConvexSphericalMirrors(canvas);
        DrawBeamSplitters(canvas);
        DrawScreens(canvas);
        DrawApertures(canvas);
        DrawReflectionGratings(canvas);
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
        var segmentCount = result.Segments.Count;
        var alpha = segmentCount > 5000 ? (byte)80 : segmentCount > 1200 ? (byte)115 : (byte)180;
        foreach (var rayGroup in result.Segments.GroupBy(segment =>
                     (Order: Math.Abs(segment.DiffractionOrder), segment.Intensity,
                         segment.WavelengthNanometers, segment.SpectrumState)))
        {
            var color = RayColor(rayGroup.Key.WavelengthNanometers, rayGroup.Key.SpectrumState);
            var intensity = Math.Clamp(rayGroup.Key.Intensity, 0, 1);
            var rayAlpha = (byte)Math.Clamp(Math.Round(alpha * intensity), 1, byte.MaxValue);
            using var paint = new SKPaint
            {
                Color = color.WithAlpha(rayAlpha),
                StrokeWidth = Math.Max(0.8f, (float)(1.05 * Math.Sqrt(zoom))),
                Style = SKPaintStyle.Stroke,
                IsAntialias = segmentCount <= 3000,
                StrokeCap = SKStrokeCap.Round,
                BlendMode = SKBlendMode.SrcOver
            };
            using var path = new SKPath();
            foreach (var segment in rayGroup)
            {
                path.MoveTo(ToScreen(segment.Start));
                path.LineTo(ToScreen(segment.End));
            }
            if (segmentCount <= 1200)
            {
                using var glow = new SKPaint
                {
                    Color = color.WithAlpha((byte)Math.Clamp(Math.Round(32 * intensity), 1, byte.MaxValue)),
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
    }

    private static readonly SKColor MixedLightColor = new(255, 224, 92);
    private const double ColorCenterWavelengthNanometers = 580;
    private static readonly (double WavelengthNanometers, SKColor Color)[] WavelengthColors =
    [
        (390, new SKColor(168, 92, 255)),
        (450, new SKColor(72, 151, 255)),
        (550, new SKColor(72, 220, 132)),
        (580, new SKColor(255, 224, 92)),
        (650, new SKColor(255, 82, 82))
    ];

    private static SKColor RayColor(double wavelengthNanometers, RaySpectrumState spectrumState)
    {
        if (spectrumState == RaySpectrumState.Composite)
        {
            return MixedLightColor;
        }

        var closestColor = WavelengthColors[0];
        var closestDistance = Math.Abs(wavelengthNanometers - closestColor.WavelengthNanometers);
        foreach (var candidate in WavelengthColors[1..])
        {
            var distance = Math.Abs(wavelengthNanometers - candidate.WavelengthNanometers);
            var isCloser = distance < closestDistance;
            var isMidpointTie = Math.Abs(distance - closestDistance) <= 1e-9;
            var isCloserToYellowCenter =
                Math.Abs(candidate.WavelengthNanometers - ColorCenterWavelengthNanometers) <
                Math.Abs(closestColor.WavelengthNanometers - ColorCenterWavelengthNanometers);
            if (isCloser || isMidpointTie && isCloserToYellowCenter)
            {
                closestColor = candidate;
                closestDistance = distance;
            }
        }

        return closestColor.Color;
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
                DrawOrigin(canvas, (mirror.Start + mirror.End) / 2,
                    selectedKind == SceneItemKind.Mirror && selectedIndex == index);
                DrawRotationHandle(canvas, mirror.Start, mirror.End);
            }
        }
    }

    private void DrawConcaveSphericalMirrors(SKCanvas canvas)
    {
        using var glow = SegmentPaint(new SKColor(64, 198, 255, 55), 10);
        using var paint = SegmentPaint(new SKColor(91, 212, 255), 3.2);
        using var guide = new SKPaint
        {
            Color = new SKColor(151, 190, 220, 110),
            StrokeWidth = 1.2f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash([5, 5], 0)
        };

        for (var index = 0; index < scene.ConcaveSphericalMirrorElements.Length; index++)
        {
            var mirror = scene.ConcaveSphericalMirrorElements[index];
            var radius = mirror.Radius;
            if (radius <= 1e-12)
            {
                continue;
            }

            var center = ToScreen(mirror.CenterOfCurvature);
            var screenRadius = (float)(radius * zoom);
            var oval = new SKRect(center.X - screenRadius, center.Y - screenRadius,
                center.X + screenRadius, center.Y + screenRadius);
            var middleAngle = Math.Atan2(
                mirror.Vertex.Y - mirror.CenterOfCurvature.Y,
                mirror.Vertex.X - mirror.CenterOfCurvature.X) * 180 / Math.PI;
            var sweep = (float)Math.Clamp(Math.Abs(mirror.ArcAngleDegrees), 1, 359.9);
            var startAngle = (float)(middleAngle - sweep / 2);
            canvas.DrawArc(oval, startAngle, sweep, false, glow);
            canvas.DrawArc(oval, startAngle, sweep, false, paint);

            if (tool == CanvasTool.Move)
            {
                canvas.DrawLine(ToScreen(mirror.Vertex), center, guide);
                canvas.DrawCircle(ToScreen(mirror.Vertex), 4.5f, paint);
                canvas.DrawCircle(center, 4.5f, paint);
                DrawOrigin(canvas, mirror.Vertex,
                    selectedKind == SceneItemKind.ConcaveSphericalMirror && selectedIndex == index);
                DrawSphericalMirrorRotationHandle(canvas, mirror.Vertex, mirror.CenterOfCurvature);
            }
        }
    }

    private void DrawConvexSphericalMirrors(SKCanvas canvas)
    {
        using var glow = SegmentPaint(new SKColor(255, 166, 72, 55), 10);
        using var paint = SegmentPaint(new SKColor(255, 184, 92), 3.2);
        using var guide = new SKPaint
        {
            Color = new SKColor(255, 201, 138, 110),
            StrokeWidth = 1.2f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash([5, 5], 0)
        };

        for (var index = 0; index < scene.ConvexSphericalMirrorElements.Length; index++)
        {
            var mirror = scene.ConvexSphericalMirrorElements[index];
            var radius = mirror.Radius;
            if (radius <= 1e-12)
            {
                continue;
            }

            var center = ToScreen(mirror.CenterOfCurvature);
            var screenRadius = (float)(radius * zoom);
            var oval = new SKRect(center.X - screenRadius, center.Y - screenRadius,
                center.X + screenRadius, center.Y + screenRadius);
            var middleAngle = Math.Atan2(
                mirror.Vertex.Y - mirror.CenterOfCurvature.Y,
                mirror.Vertex.X - mirror.CenterOfCurvature.X) * 180 / Math.PI;
            var sweep = (float)Math.Clamp(Math.Abs(mirror.ArcAngleDegrees), 1, 359.9);
            var startAngle = (float)(middleAngle - sweep / 2);
            canvas.DrawArc(oval, startAngle, sweep, false, glow);
            canvas.DrawArc(oval, startAngle, sweep, false, paint);

            if (tool == CanvasTool.Move)
            {
                canvas.DrawLine(ToScreen(mirror.Vertex), center, guide);
                canvas.DrawCircle(ToScreen(mirror.Vertex), 4.5f, paint);
                canvas.DrawCircle(center, 4.5f, paint);
                DrawOrigin(canvas, mirror.Vertex,
                    selectedKind == SceneItemKind.ConvexSphericalMirror && selectedIndex == index);
                DrawSphericalMirrorRotationHandle(canvas, mirror.Vertex, mirror.CenterOfCurvature);
            }
        }
    }

    private void DrawScreens(SKCanvas canvas)
    {
        using var glow = SegmentPaint(new SKColor(148, 163, 184, 42), 9);
        using var paint = SegmentPaint(new SKColor(148, 163, 184), 3);
        for (var index = 0; index < scene.ScreenElements.Length; index++)
        {
            var screen = scene.ScreenElements[index];
            canvas.DrawLine(ToScreen(screen.Start), ToScreen(screen.End), glow);
            canvas.DrawLine(ToScreen(screen.Start), ToScreen(screen.End), paint);
            if (tool == CanvasTool.Move)
            {
                DrawOrigin(canvas, (screen.Start + screen.End) / 2,
                    selectedKind == SceneItemKind.Screen && selectedIndex == index);
                DrawRotationHandle(canvas, screen.Start, screen.End);
            }
        }
    }

    private void DrawBeamSplitters(SKCanvas canvas)
    {
        using var glow = SegmentPaint(new SKColor(47, 213, 255, 50), 9);
        using var paint = SegmentPaint(new SKColor(117, 225, 255), 3);
        for (var index = 0; index < scene.BeamSplitterElements.Length; index++)
        {
            var beamSplitter = scene.BeamSplitterElements[index];
            canvas.DrawLine(ToScreen(beamSplitter.Start), ToScreen(beamSplitter.End), glow);
            canvas.DrawLine(ToScreen(beamSplitter.Start), ToScreen(beamSplitter.End), paint);
            if (tool == CanvasTool.Move)
            {
                DrawOrigin(canvas, (beamSplitter.Start + beamSplitter.End) / 2,
                    selectedKind == SceneItemKind.BeamSplitter && selectedIndex == index);
                DrawRotationHandle(canvas, beamSplitter.Start, beamSplitter.End);
            }
        }
    }

    private void DrawApertures(SKCanvas canvas)
    {
        using var glow = SegmentPaint(new SKColor(148, 163, 184, 42), 9);
        using var paint = SegmentPaint(new SKColor(148, 163, 184), 3);
        for (var index = 0; index < scene.ApertureElements.Length; index++)
        {
            var aperture = scene.ApertureElements[index];
            var edge = aperture.End - aperture.Start;
            var length = edge.Length;
            if (length <= 1e-12)
            {
                continue;
            }

            var tangent = edge / length;
            var normal = tangent.Perpendicular();
            var midpoint = (aperture.Start + aperture.End) / 2;
            var halfOpening = Math.Clamp(aperture.OpeningSize, 0, length) / 2;
            var openingStart = midpoint - tangent * halfOpening;
            var openingEnd = midpoint + tangent * halfOpening;
            canvas.DrawLine(ToScreen(aperture.Start), ToScreen(openingStart), glow);
            canvas.DrawLine(ToScreen(openingEnd), ToScreen(aperture.End), glow);
            canvas.DrawLine(ToScreen(aperture.Start), ToScreen(openingStart), paint);
            canvas.DrawLine(ToScreen(openingEnd), ToScreen(aperture.End), paint);

            var markerSize = Math.Min(7 / zoom, length * 0.12);
            canvas.DrawLine(ToScreen(openingStart - normal * markerSize),
                ToScreen(openingStart + normal * markerSize), paint);
            canvas.DrawLine(ToScreen(openingEnd - normal * markerSize),
                ToScreen(openingEnd + normal * markerSize), paint);

            if (tool == CanvasTool.Move)
            {
                DrawOrigin(canvas, midpoint,
                    selectedKind == SceneItemKind.Aperture && selectedIndex == index);
                DrawRotationHandle(canvas, aperture.Start, aperture.End);
            }
        }
    }

    private void DrawReflectionGratings(SKCanvas canvas)
    {
        using var glow = SegmentPaint(new SKColor(148, 163, 184, 42), 9);
        using var paint = SegmentPaint(new SKColor(148, 163, 184), 3);
        using var groovePaint = SegmentPaint(new SKColor(203, 213, 225), 1.2);
        for (var index = 0; index < scene.ReflectionGratingElements.Length; index++)
        {
            var grating = scene.ReflectionGratingElements[index];
            var edge = grating.End - grating.Start;
            var length = edge.Length;
            if (length <= 1e-12)
            {
                continue;
            }

            canvas.DrawLine(ToScreen(grating.Start), ToScreen(grating.End), glow);
            canvas.DrawLine(ToScreen(grating.Start), ToScreen(grating.End), paint);
            var tangent = edge / length;
            var normal = tangent.Perpendicular();
            var visibleGrooves = Math.Clamp((int)Math.Round(length * zoom / 14), 4, 24);
            var markerSize = Math.Min(5 / zoom, length * 0.08);
            for (var groove = 1; groove < visibleGrooves; groove++)
            {
                var point = grating.Start + edge * ((double)groove / visibleGrooves);
                canvas.DrawLine(ToScreen(point - normal * markerSize),
                    ToScreen(point + normal * markerSize), groovePaint);
            }

            if (tool == CanvasTool.Move)
            {
                DrawOrigin(canvas, (grating.Start + grating.End) / 2,
                    selectedKind == SceneItemKind.ReflectionGrating && selectedIndex == index);
                DrawRotationHandle(canvas, grating.Start, grating.End);
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
                DrawOrigin(canvas, (lens.Start + lens.End) / 2,
                    selectedKind == SceneItemKind.Lens && selectedIndex == index);
                DrawRotationHandle(canvas, lens.Start, lens.End);
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
                    DrawRotationHandle(canvas, source.Position, end);
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
                    DrawPointLightRotationHandle(canvas, source);
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
        if (tool is CanvasTool.ConcaveSphericalMirror or CanvasTool.ConvexSphericalMirror)
        {
            var radius = (end - start).Length;
            if (radius > 1e-12)
            {
                var center = ToScreen(end);
                var screenRadius = (float)(radius * zoom);
                var oval = new SKRect(center.X - screenRadius, center.Y - screenRadius,
                    center.X + screenRadius, center.Y + screenRadius);
                var middleAngle = Math.Atan2(start.Y - end.Y, start.X - end.X) * 180 / Math.PI;
                canvas.DrawArc(oval, (float)(middleAngle - 90), 180, false, preview);
            }
        }
        if (tool is CanvasTool.ParallelLight or CanvasTool.CompositeParallelLight)
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
        canvas.DrawText($"{scene.Name}  ·  每光源 {raysPerSource} 条 / 共 {result.InitialRayCount} 条  ·  {result.ReflectedRayCount} 次反射  ·  {result.RefractedRayCount} 次折射  ·  {result.DiffractedRayCount} 条衍射光线",
            18, 28, SKTextAlign.Left, font, paint);
    }

    private SKPaint SegmentPaint(SKColor color, double width) => new()
    {
        Color = color,
        StrokeWidth = (float)Math.Clamp(width * Math.Sqrt(zoom), 2, width * 2),
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        IsAntialias = true
    };

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

    private void DrawRotationHandle(SKCanvas canvas, Vector2D start, Vector2D end)
    {
        var midpoint = (start + end) / 2;
        var handle = midpoint + (end - start).Normalized().Perpendicular() * RotationHandleOffset;
        DrawRotationPoint(canvas, midpoint, handle);
    }

    private void DrawSphericalMirrorRotationHandle(
        SKCanvas canvas, Vector2D vertex, Vector2D centerOfCurvature)
    {
        var handle = vertex + (centerOfCurvature - vertex).Normalized() * RotationHandleOffset;
        DrawRotationPoint(canvas, vertex, handle);
    }

    private void DrawPointLightRotationHandle(SKCanvas canvas, LightSource source)
    {
        var handle = source.Position +
                     Vector2D.FromAngle(source.DirectionDegrees * Math.PI / 180) *
                     RotationHandleOffset;
        DrawRotationPoint(canvas, source.Position, handle);
    }

    private void DrawRotationPoint(SKCanvas canvas, Vector2D origin, Vector2D handle)
    {
        var originPoint = ToScreen(origin);
        var handlePoint = ToScreen(handle);
        using var guide = new SKPaint
        {
            Color = new SKColor(255, 255, 255, 90),
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash([3, 4], 0)
        };
        using var outline = new SKPaint
        {
            Color = new SKColor(8, 13, 24, 210),
            StrokeWidth = 1.5f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };
        using var fill = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawLine(originPoint, handlePoint, guide);
        canvas.DrawCircle(handlePoint, 5.5f, fill);
        canvas.DrawCircle(handlePoint, 5.5f, outline);
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
