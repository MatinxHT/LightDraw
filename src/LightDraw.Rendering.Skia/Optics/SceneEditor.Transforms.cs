using LightDraw.Core.Geometry;
using LightDraw.Core.Scene;

namespace LightDraw.Rendering.Skia.Optics;

internal sealed partial class SceneEditor
{
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

        if (_selectedGroupId is { } groupId && FindGroup(groupId) is { } group &&
            SceneGeometry.Find(_scene, group.PrimaryMemberId) is { } primary)
        {
            var current = SceneGeometry.AngleDegrees(_scene, primary);
            var delta = NormalizeSignedDegrees(degrees - current);
            if (Math.Abs(delta) <= 1e-9) return;
            var pivot = SceneGeometry.Origin(_scene, primary);
            UpdateScene(SceneGeometry.Rotate(_scene, group.MemberIds.ToHashSet(), pivot,
                delta * Math.PI / 180));
            CommitSelectedEdit();
            return;
        }

        var radians = NormalizeDegrees(degrees) * Math.PI / 180;
        switch (_selectedKind)
        {
            case SceneItemKind.LightSource when IsValidIndex(_selectedIndex, _scene.LightSources):
                var sources = (LightSource[])_scene.LightSources.Clone();
                var source = sources[_selectedIndex];
                if (source.Kind == LightSourceKind.Point)
                {
                    sources[_selectedIndex] = source with { DirectionDegrees = NormalizeDegrees(degrees) };
                    UpdateScene(_scene with { LightSources = sources });
                    break;
                }
                if (source.End is not { } sourceEnd)
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
            case SceneItemKind.ConcaveSphericalMirror when IsValidIndex(
                _selectedIndex, _scene.ConcaveSphericalMirrorElements):
                var sphericalMirrors = (ConcaveSphericalMirror[])_scene.ConcaveSphericalMirrorElements.Clone();
                var sphericalMirror = sphericalMirrors[_selectedIndex];
                sphericalMirrors[_selectedIndex] = sphericalMirror with
                {
                    CenterOfCurvature = sphericalMirror.Vertex + Vector2D.FromAngle(radians) * sphericalMirror.Radius
                };
                UpdateScene(_scene with { ConcaveSphericalMirrors = sphericalMirrors });
                break;
            case SceneItemKind.ConvexSphericalMirror when IsValidIndex(
                _selectedIndex, _scene.ConvexSphericalMirrorElements):
                var convexSphericalMirrors = (ConvexSphericalMirror[])_scene.ConvexSphericalMirrorElements.Clone();
                var convexSphericalMirror = convexSphericalMirrors[_selectedIndex];
                convexSphericalMirrors[_selectedIndex] = convexSphericalMirror with
                {
                    CenterOfCurvature = convexSphericalMirror.Vertex +
                                        Vector2D.FromAngle(radians) * convexSphericalMirror.Radius
                };
                UpdateScene(_scene with { ConvexSphericalMirrors = convexSphericalMirrors });
                break;
            case SceneItemKind.BeamSplitter when IsValidIndex(_selectedIndex, _scene.BeamSplitterElements):
                var beamSplitters = (BeamSplitterSegment[])_scene.BeamSplitterElements.Clone();
                var beamSplitter = beamSplitters[_selectedIndex];
                var rotatedBeamSplitter = RotateSegment(beamSplitter.Start, beamSplitter.End, radians);
                beamSplitters[_selectedIndex] = beamSplitter with
                {
                    Start = rotatedBeamSplitter.Start,
                    End = rotatedBeamSplitter.End
                };
                UpdateScene(_scene with { BeamSplitters = beamSplitters });
                break;
            case SceneItemKind.Screen when IsValidIndex(_selectedIndex, _scene.ScreenElements):
                var screens = (ScreenSegment[])_scene.ScreenElements.Clone();
                var screen = screens[_selectedIndex];
                var rotatedScreen = RotateSegment(screen.Start, screen.End, radians);
                screens[_selectedIndex] = screen with
                {
                    Start = rotatedScreen.Start,
                    End = rotatedScreen.End
                };
                UpdateScene(_scene with { Screens = screens });
                break;
            case SceneItemKind.Aperture when IsValidIndex(_selectedIndex, _scene.ApertureElements):
                var apertures = (ApertureSegment[])_scene.ApertureElements.Clone();
                var aperture = apertures[_selectedIndex];
                var rotatedAperture = RotateSegment(aperture.Start, aperture.End, radians);
                apertures[_selectedIndex] = aperture with
                {
                    Start = rotatedAperture.Start,
                    End = rotatedAperture.End
                };
                UpdateScene(_scene with { Apertures = apertures });
                break;
            case SceneItemKind.ReflectionGrating when IsValidIndex(_selectedIndex, _scene.ReflectionGratingElements):
                var gratings = (ReflectionGratingSegment[])_scene.ReflectionGratingElements.Clone();
                var grating = gratings[_selectedIndex];
                var rotatedGrating = RotateSegment(grating.Start, grating.End, radians);
                gratings[_selectedIndex] = grating with
                {
                    Start = rotatedGrating.Start,
                    End = rotatedGrating.End
                };
                UpdateScene(_scene with { ReflectionGratings = gratings });
                break;
            case SceneItemKind.ConcaveGrating when IsValidIndex(_selectedIndex, _scene.ConcaveGratingElements):
                var concaveGratings = (ConcaveGrating[])_scene.ConcaveGratingElements.Clone();
                var concaveGrating = concaveGratings[_selectedIndex];
                concaveGratings[_selectedIndex] = concaveGrating with
                {
                    CenterOfCurvature = concaveGrating.Vertex +
                        Vector2D.FromAngle(radians) * concaveGrating.Radius
                };
                UpdateScene(_scene with { ConcaveGratings = concaveGratings });
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
            case SceneItemKind.BeamSplitter when IsValidIndex(_selectedIndex, _scene.BeamSplitterElements):
                var beamSplitters = (BeamSplitterSegment[])_scene.BeamSplitterElements.Clone();
                var beamSplitter = beamSplitters[_selectedIndex];
                var resizedBeamSplitter = ResizeSegment(beamSplitter.Start, beamSplitter.End, clamped);
                beamSplitters[_selectedIndex] = beamSplitter with
                {
                    Start = resizedBeamSplitter.Start,
                    End = resizedBeamSplitter.End
                };
                UpdateScene(_scene with { BeamSplitters = beamSplitters });
                break;
            case SceneItemKind.Screen when IsValidIndex(_selectedIndex, _scene.ScreenElements):
                var screens = (ScreenSegment[])_scene.ScreenElements.Clone();
                var screen = screens[_selectedIndex];
                var resizedScreen = ResizeSegment(screen.Start, screen.End, clamped);
                screens[_selectedIndex] = screen with
                {
                    Start = resizedScreen.Start,
                    End = resizedScreen.End
                };
                UpdateScene(_scene with { Screens = screens });
                break;
            case SceneItemKind.Aperture when IsValidIndex(_selectedIndex, _scene.ApertureElements):
                var apertures = (ApertureSegment[])_scene.ApertureElements.Clone();
                var aperture = apertures[_selectedIndex];
                var resizedAperture = ResizeSegment(aperture.Start, aperture.End, clamped);
                apertures[_selectedIndex] = aperture with
                {
                    Start = resizedAperture.Start,
                    End = resizedAperture.End,
                    OpeningSize = Math.Min(aperture.OpeningSize, clamped)
                };
                UpdateScene(_scene with { Apertures = apertures });
                break;
            case SceneItemKind.ReflectionGrating when IsValidIndex(_selectedIndex, _scene.ReflectionGratingElements):
                var gratings = (ReflectionGratingSegment[])_scene.ReflectionGratingElements.Clone();
                var grating = gratings[_selectedIndex];
                var resizedGrating = ResizeSegment(grating.Start, grating.End, clamped);
                gratings[_selectedIndex] = grating with
                {
                    Start = resizedGrating.Start,
                    End = resizedGrating.End
                };
                UpdateScene(_scene with { ReflectionGratings = gratings });
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

        if (_selectedGroupId is { } groupId && FindGroup(groupId) is { } group)
        {
            UpdateScene(SceneGeometry.Translate(_scene, group.MemberIds.ToHashSet(), delta));
            CommitSelectedEdit();
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
            case SceneItemKind.ConcaveSphericalMirror when IsValidIndex(
                _selectedIndex, _scene.ConcaveSphericalMirrorElements):
                var sphericalMirrors = (ConcaveSphericalMirror[])_scene.ConcaveSphericalMirrorElements.Clone();
                var sphericalMirror = sphericalMirrors[_selectedIndex];
                sphericalMirrors[_selectedIndex] = sphericalMirror with
                {
                    Vertex = sphericalMirror.Vertex + delta,
                    CenterOfCurvature = sphericalMirror.CenterOfCurvature + delta
                };
                UpdateScene(_scene with { ConcaveSphericalMirrors = sphericalMirrors });
                break;
            case SceneItemKind.ConvexSphericalMirror when IsValidIndex(
                _selectedIndex, _scene.ConvexSphericalMirrorElements):
                var convexSphericalMirrors = (ConvexSphericalMirror[])_scene.ConvexSphericalMirrorElements.Clone();
                var convexSphericalMirror = convexSphericalMirrors[_selectedIndex];
                convexSphericalMirrors[_selectedIndex] = convexSphericalMirror with
                {
                    Vertex = convexSphericalMirror.Vertex + delta,
                    CenterOfCurvature = convexSphericalMirror.CenterOfCurvature + delta
                };
                UpdateScene(_scene with { ConvexSphericalMirrors = convexSphericalMirrors });
                break;
            case SceneItemKind.BeamSplitter when IsValidIndex(_selectedIndex, _scene.BeamSplitterElements):
                var beamSplitters = (BeamSplitterSegment[])_scene.BeamSplitterElements.Clone();
                var beamSplitter = beamSplitters[_selectedIndex];
                beamSplitters[_selectedIndex] = beamSplitter with
                {
                    Start = beamSplitter.Start + delta,
                    End = beamSplitter.End + delta
                };
                UpdateScene(_scene with { BeamSplitters = beamSplitters });
                break;
            case SceneItemKind.Screen when IsValidIndex(_selectedIndex, _scene.ScreenElements):
                var screens = (ScreenSegment[])_scene.ScreenElements.Clone();
                var screen = screens[_selectedIndex];
                screens[_selectedIndex] = screen with
                {
                    Start = screen.Start + delta,
                    End = screen.End + delta
                };
                UpdateScene(_scene with { Screens = screens });
                break;
            case SceneItemKind.Aperture when IsValidIndex(_selectedIndex, _scene.ApertureElements):
                var apertures = (ApertureSegment[])_scene.ApertureElements.Clone();
                var aperture = apertures[_selectedIndex];
                apertures[_selectedIndex] = aperture with
                {
                    Start = aperture.Start + delta,
                    End = aperture.End + delta
                };
                UpdateScene(_scene with { Apertures = apertures });
                break;
            case SceneItemKind.ReflectionGrating when IsValidIndex(_selectedIndex, _scene.ReflectionGratingElements):
                var gratings = (ReflectionGratingSegment[])_scene.ReflectionGratingElements.Clone();
                var grating = gratings[_selectedIndex];
                gratings[_selectedIndex] = grating with
                {
                    Start = grating.Start + delta,
                    End = grating.End + delta
                };
                UpdateScene(_scene with { ReflectionGratings = gratings });
                break;
            case SceneItemKind.ConcaveGrating when IsValidIndex(_selectedIndex, _scene.ConcaveGratingElements):
                var concaveGratings = (ConcaveGrating[])_scene.ConcaveGratingElements.Clone();
                var concaveGrating = concaveGratings[_selectedIndex];
                concaveGratings[_selectedIndex] = concaveGrating with
                {
                    Vertex = concaveGrating.Vertex + delta,
                    CenterOfCurvature = concaveGrating.CenterOfCurvature + delta
                };
                UpdateScene(_scene with { ConcaveGratings = concaveGratings });
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

    public void SetSelectedSecondOrigin(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            return;
        }

        var center = new Vector2D(x, y);
        if (_selectedKind == SceneItemKind.ConcaveSphericalMirror &&
            IsValidIndex(_selectedIndex, _scene.ConcaveSphericalMirrorElements))
        {
            var mirrors = (ConcaveSphericalMirror[])_scene.ConcaveSphericalMirrorElements.Clone();
            var mirror = mirrors[_selectedIndex];
            if ((center - mirror.Vertex).Length < 2 ||
                (center - mirror.CenterOfCurvature).LengthSquared <= 1e-12)
            {
                return;
            }
            mirrors[_selectedIndex] = mirror with { CenterOfCurvature = center };
            UpdateScene(_scene with { ConcaveSphericalMirrors = mirrors });
        }
        else if (_selectedKind == SceneItemKind.ConvexSphericalMirror &&
                 IsValidIndex(_selectedIndex, _scene.ConvexSphericalMirrorElements))
        {
            var mirrors = (ConvexSphericalMirror[])_scene.ConvexSphericalMirrorElements.Clone();
            var mirror = mirrors[_selectedIndex];
            if ((center - mirror.Vertex).Length < 2 ||
                (center - mirror.CenterOfCurvature).LengthSquared <= 1e-12)
            {
                return;
            }
            mirrors[_selectedIndex] = mirror with { CenterOfCurvature = center };
            UpdateScene(_scene with { ConvexSphericalMirrors = mirrors });
        }
        else if (_selectedKind == SceneItemKind.ConcaveGrating &&
                 IsValidIndex(_selectedIndex, _scene.ConcaveGratingElements))
        {
            var gratings = (ConcaveGrating[])_scene.ConcaveGratingElements.Clone();
            var grating = gratings[_selectedIndex];
            if ((center - grating.Vertex).Length < 2 ||
                (center - grating.CenterOfCurvature).LengthSquared <= 1e-12) return;
            gratings[_selectedIndex] = grating with { CenterOfCurvature = center };
            UpdateScene(_scene with { ConcaveGratings = gratings });
        }
        else if (CreateSelection() is { } selection)
        {
            var direction = (center - new Vector2D(selection.OriginX, selection.OriginY)).Normalized();
            if (direction.LengthSquared <= 1e-12)
            {
                return;
            }

            var handleAngle = DirectionDegrees(direction);
            SetSelectedAngle(_selectedKind == SceneItemKind.LightSource &&
                             IsValidIndex(_selectedIndex, _scene.LightSources) &&
                             _scene.LightSources[_selectedIndex].Kind == LightSourceKind.Point
                ? handleAngle
                : handleAngle - 90);
            return;
        }
        else
        {
            return;
        }
        CommitSelectedEdit();
    }
}
