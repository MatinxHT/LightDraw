using LightDraw.Core.Geometry;
using LightDraw.Core.Scene;

namespace LightDraw.Rendering.Skia.Optics;

internal sealed partial class SceneEditor
{
    public void AddPointLight(Vector2D position, LightSpectrumKind spectrum)
    {
        UpdateScene(_scene with
        {
            LightSources = [.. _scene.LightSources,
                new LightSource(position, 0, 360, ReferenceWavelength(spectrum),
                    LightSourceKind.Point, Spectrum: spectrum, Id: Guid.NewGuid(),
                    Name: NextElementName(spectrum == LightSpectrumKind.Composite
                        ? "Composite Point Source" : "Point Source"))]
        });
        CommitSelectedEdit();
    }

    public bool AddElement(CanvasTool tool, Vector2D start, Vector2D end)
    {
        var delta = end - start;
        if (!HasUsableLength(start, end)) return false;

        switch (tool)
        {
            case CanvasTool.ParallelLight:
            case CanvasTool.CompositeParallelLight:
                var direction = delta.Normalized().Perpendicular();
                var spectrum = tool == CanvasTool.CompositeParallelLight
                    ? LightSpectrumKind.Composite
                    : LightSpectrumKind.Monochromatic;
                var source = new LightSource(start, DirectionDegrees(direction), 0,
                    ReferenceWavelength(spectrum), LightSourceKind.ParallelLine, end, spectrum,
                    Guid.NewGuid(), NextElementName(spectrum == LightSpectrumKind.Composite
                        ? "Composite Parallel Source" : "Parallel Source"));
                UpdateScene(_scene with { LightSources = [.. _scene.LightSources, source] });
                break;
            case CanvasTool.Mirror:
                UpdateScene(_scene with { Mirrors = [.. _scene.Mirrors,
                    new MirrorSegment(start, end, Guid.NewGuid(), NextElementName("Mirror"))] });
                break;
            case CanvasTool.ConcaveSphericalMirror:
                UpdateScene(_scene with
                {
                    ConcaveSphericalMirrors =
                    [.. _scene.ConcaveSphericalMirrorElements, new ConcaveSphericalMirror(start, end,
                        Id: Guid.NewGuid(), Name: NextElementName("Concave Spherical Mirror"))]
                });
                break;
            case CanvasTool.ConvexSphericalMirror:
                UpdateScene(_scene with
                {
                    ConvexSphericalMirrors =
                    [.. _scene.ConvexSphericalMirrorElements, new ConvexSphericalMirror(start, end,
                        Id: Guid.NewGuid(), Name: NextElementName("Convex Spherical Mirror"))]
                });
                break;
            case CanvasTool.BeamSplitter:
                UpdateScene(_scene with
                {
                    BeamSplitters =
                    [.. _scene.BeamSplitterElements, new BeamSplitterSegment(start, end, Guid.NewGuid(),
                        NextElementName("Beam Splitter"))]
                });
                break;
            case CanvasTool.Screen:
                UpdateScene(_scene with { Screens = [.. _scene.ScreenElements,
                    new ScreenSegment(start, end, Guid.NewGuid(), NextElementName("Screen"))] });
                break;
            case CanvasTool.Aperture:
                UpdateScene(_scene with
                {
                    Apertures = [.. _scene.ApertureElements,
                    new ApertureSegment(start, end, Math.Min(60, delta.Length * 0.3), Guid.NewGuid(),
                        NextElementName("Aperture"))]
                });
                break;
            case CanvasTool.ReflectionGrating:
                UpdateScene(_scene with
                {
                    ReflectionGratings = [.. _scene.ReflectionGratingElements,
                    new ReflectionGratingSegment(start, end, 600, Guid.NewGuid(),
                        NextElementName("Reflection Grating"))]
                });
                break;
            case CanvasTool.ConcaveGrating:
                UpdateScene(_scene with
                {
                    ConcaveGratings = [.. _scene.ConcaveGratingElements,
                        new ConcaveGrating(start, end, Id: Guid.NewGuid(),
                            Name: NextElementName("Concave Grating"))]
                });
                break;
            case CanvasTool.ConvexLens:
            case CanvasTool.ConcaveLens:
                var kind = tool == CanvasTool.ConvexLens ? LensKind.Convex : LensKind.Concave;
                UpdateScene(_scene with
                {
                    Lenses = [.. _scene.LensElements,
                    new LensSegment(start, end, kind, DefaultLensFocalLength, Id: Guid.NewGuid(),
                        Name: NextElementName(kind == LensKind.Convex ? "Convex Lens" : "Concave Lens"))]
                });
                break;
            default:
                return false;
        }

        CommitSelectedEdit();
        return true;
    }

    private string NextElementName(string baseName)
    {
        var names = EnumerateElementNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var number = 1; ; number++)
        {
            var candidate = $"{baseName} {number}";
            if (!names.Contains(candidate)) return candidate;
        }
    }

    private IEnumerable<string> EnumerateElementNames()
    {
        foreach (var item in _scene.LightSources) if (!string.IsNullOrWhiteSpace(item.Name)) yield return item.Name;
        foreach (var item in _scene.Mirrors) if (!string.IsNullOrWhiteSpace(item.Name)) yield return item.Name;
        foreach (var item in _scene.ConcaveSphericalMirrorElements) if (!string.IsNullOrWhiteSpace(item.Name)) yield return item.Name;
        foreach (var item in _scene.ConvexSphericalMirrorElements) if (!string.IsNullOrWhiteSpace(item.Name)) yield return item.Name;
        foreach (var item in _scene.BeamSplitterElements) if (!string.IsNullOrWhiteSpace(item.Name)) yield return item.Name;
        foreach (var item in _scene.ScreenElements) if (!string.IsNullOrWhiteSpace(item.Name)) yield return item.Name;
        foreach (var item in _scene.ApertureElements) if (!string.IsNullOrWhiteSpace(item.Name)) yield return item.Name;
        foreach (var item in _scene.ReflectionGratingElements) if (!string.IsNullOrWhiteSpace(item.Name)) yield return item.Name;
        foreach (var item in _scene.ConcaveGratingElements) if (!string.IsNullOrWhiteSpace(item.Name)) yield return item.Name;
        foreach (var item in _scene.LensElements) if (!string.IsNullOrWhiteSpace(item.Name)) yield return item.Name;
        foreach (var item in _scene.ElementGroups) if (!string.IsNullOrWhiteSpace(item.Name)) yield return item.Name;
    }

    private static double ReferenceWavelength(LightSpectrumKind spectrum) =>
        spectrum == LightSpectrumKind.Composite
            ? LightSource.CompositeGreenWavelengthNanometers
            : LightSource.MonochromaticWavelengthNanometers;
}
