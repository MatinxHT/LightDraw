using LightDraw.Core.Scene;

namespace LightDraw.Rendering.Skia.Optics;

internal sealed partial class SceneEditor
{
    public void SetSelectedName(string value)
    {
        var name = value.Trim();
        if (name.Length == 0 || name.Length > 120) return;

        if (_selectedGroupId is { } groupId && FindGroup(groupId) is not null)
        {
            UpdateScene(_scene with
            {
                Groups = _scene.ElementGroups.Select(group => group.Id == groupId
                    ? group with { Name = name } : group).ToArray()
            });
            CommitSelectedEdit();
            return;
        }

        switch (_selectedKind)
        {
            case SceneItemKind.LightSource when IsValidIndex(_selectedIndex, _scene.LightSources):
                _scene = _scene with { LightSources = _scene.LightSources.Select((item, index) =>
                    index == _selectedIndex ? item with { Name = name } : item).ToArray() };
                break;
            case SceneItemKind.Mirror when IsValidIndex(_selectedIndex, _scene.Mirrors):
                _scene = _scene with { Mirrors = _scene.Mirrors.Select((item, index) =>
                    index == _selectedIndex ? item with { Name = name } : item).ToArray() };
                break;
            case SceneItemKind.ConcaveSphericalMirror when IsValidIndex(_selectedIndex, _scene.ConcaveSphericalMirrorElements):
                _scene = _scene with { ConcaveSphericalMirrors = _scene.ConcaveSphericalMirrorElements.Select((item, index) =>
                    index == _selectedIndex ? item with { Name = name } : item).ToArray() };
                break;
            case SceneItemKind.ConvexSphericalMirror when IsValidIndex(_selectedIndex, _scene.ConvexSphericalMirrorElements):
                _scene = _scene with { ConvexSphericalMirrors = _scene.ConvexSphericalMirrorElements.Select((item, index) =>
                    index == _selectedIndex ? item with { Name = name } : item).ToArray() };
                break;
            case SceneItemKind.BeamSplitter when IsValidIndex(_selectedIndex, _scene.BeamSplitterElements):
                _scene = _scene with { BeamSplitters = _scene.BeamSplitterElements.Select((item, index) =>
                    index == _selectedIndex ? item with { Name = name } : item).ToArray() };
                break;
            case SceneItemKind.Screen when IsValidIndex(_selectedIndex, _scene.ScreenElements):
                _scene = _scene with { Screens = _scene.ScreenElements.Select((item, index) =>
                    index == _selectedIndex ? item with { Name = name } : item).ToArray() };
                break;
            case SceneItemKind.Aperture when IsValidIndex(_selectedIndex, _scene.ApertureElements):
                _scene = _scene with { Apertures = _scene.ApertureElements.Select((item, index) =>
                    index == _selectedIndex ? item with { Name = name } : item).ToArray() };
                break;
            case SceneItemKind.ReflectionGrating when IsValidIndex(_selectedIndex, _scene.ReflectionGratingElements):
                _scene = _scene with { ReflectionGratings = _scene.ReflectionGratingElements.Select((item, index) =>
                    index == _selectedIndex ? item with { Name = name } : item).ToArray() };
                break;
            case SceneItemKind.ConcaveGrating when IsValidIndex(_selectedIndex, _scene.ConcaveGratingElements):
                _scene = _scene with { ConcaveGratings = _scene.ConcaveGratingElements.Select((item, index) =>
                    index == _selectedIndex ? item with { Name = name } : item).ToArray() };
                break;
            case SceneItemKind.Lens when IsValidIndex(_selectedIndex, _scene.LensElements):
                _scene = _scene with { Lenses = _scene.LensElements.Select((item, index) =>
                    index == _selectedIndex ? item with { Name = name } : item).ToArray() };
                break;
            default:
                return;
        }

        SceneUpdated?.Invoke(this, EventArgs.Empty);
        CommitSelectedEdit();
    }

    public void SetSelectedTemporarilyHidden(bool value)
    {
        if (_selectedGroupId is { } groupId && FindGroup(groupId) is { } group)
        {
            var memberIds = group.MemberIds
                .Where(id => SceneGeometry.Find(_scene, id) is { } item && item.Kind != SceneItemKind.LightSource)
                .ToHashSet();
            if (memberIds.Count == 0)
            {
                return;
            }

            UpdateScene(_scene with
            {
                Mirrors = _scene.Mirrors.Select(item => memberIds.Contains(item.Id)
                    ? item with { IsTemporarilyHidden = value } : item).ToArray(),
                ConcaveSphericalMirrors = _scene.ConcaveSphericalMirrorElements.Select(item => memberIds.Contains(item.Id)
                    ? item with { IsTemporarilyHidden = value } : item).ToArray(),
                ConvexSphericalMirrors = _scene.ConvexSphericalMirrorElements.Select(item => memberIds.Contains(item.Id)
                    ? item with { IsTemporarilyHidden = value } : item).ToArray(),
                BeamSplitters = _scene.BeamSplitterElements.Select(item => memberIds.Contains(item.Id)
                    ? item with { IsTemporarilyHidden = value } : item).ToArray(),
                Screens = _scene.ScreenElements.Select(item => memberIds.Contains(item.Id)
                    ? item with { IsTemporarilyHidden = value } : item).ToArray(),
                Apertures = _scene.ApertureElements.Select(item => memberIds.Contains(item.Id)
                    ? item with { IsTemporarilyHidden = value } : item).ToArray(),
                ReflectionGratings = _scene.ReflectionGratingElements.Select(item => memberIds.Contains(item.Id)
                    ? item with { IsTemporarilyHidden = value } : item).ToArray(),
                ConcaveGratings = _scene.ConcaveGratingElements.Select(item => memberIds.Contains(item.Id)
                    ? item with { IsTemporarilyHidden = value } : item).ToArray(),
                Lenses = _scene.LensElements.Select(item => memberIds.Contains(item.Id)
                    ? item with { IsTemporarilyHidden = value } : item).ToArray()
            });
            CommitSelectedEdit();
            return;
        }

        switch (_selectedKind)
        {
            case SceneItemKind.Mirror when IsValidIndex(_selectedIndex, _scene.Mirrors):
                UpdateScene(_scene with { Mirrors = _scene.Mirrors.Select((item, index) =>
                    index == _selectedIndex ? item with { IsTemporarilyHidden = value } : item).ToArray() });
                break;
            case SceneItemKind.ConcaveSphericalMirror when IsValidIndex(_selectedIndex, _scene.ConcaveSphericalMirrorElements):
                UpdateScene(_scene with { ConcaveSphericalMirrors = _scene.ConcaveSphericalMirrorElements.Select((item, index) =>
                    index == _selectedIndex ? item with { IsTemporarilyHidden = value } : item).ToArray() });
                break;
            case SceneItemKind.ConvexSphericalMirror when IsValidIndex(_selectedIndex, _scene.ConvexSphericalMirrorElements):
                UpdateScene(_scene with { ConvexSphericalMirrors = _scene.ConvexSphericalMirrorElements.Select((item, index) =>
                    index == _selectedIndex ? item with { IsTemporarilyHidden = value } : item).ToArray() });
                break;
            case SceneItemKind.BeamSplitter when IsValidIndex(_selectedIndex, _scene.BeamSplitterElements):
                UpdateScene(_scene with { BeamSplitters = _scene.BeamSplitterElements.Select((item, index) =>
                    index == _selectedIndex ? item with { IsTemporarilyHidden = value } : item).ToArray() });
                break;
            case SceneItemKind.Screen when IsValidIndex(_selectedIndex, _scene.ScreenElements):
                UpdateScene(_scene with { Screens = _scene.ScreenElements.Select((item, index) =>
                    index == _selectedIndex ? item with { IsTemporarilyHidden = value } : item).ToArray() });
                break;
            case SceneItemKind.Aperture when IsValidIndex(_selectedIndex, _scene.ApertureElements):
                UpdateScene(_scene with { Apertures = _scene.ApertureElements.Select((item, index) =>
                    index == _selectedIndex ? item with { IsTemporarilyHidden = value } : item).ToArray() });
                break;
            case SceneItemKind.ReflectionGrating when IsValidIndex(_selectedIndex, _scene.ReflectionGratingElements):
                UpdateScene(_scene with { ReflectionGratings = _scene.ReflectionGratingElements.Select((item, index) =>
                    index == _selectedIndex ? item with { IsTemporarilyHidden = value } : item).ToArray() });
                break;
            case SceneItemKind.ConcaveGrating when IsValidIndex(_selectedIndex, _scene.ConcaveGratingElements):
                UpdateScene(_scene with { ConcaveGratings = _scene.ConcaveGratingElements.Select((item, index) =>
                    index == _selectedIndex ? item with { IsTemporarilyHidden = value } : item).ToArray() });
                break;
            case SceneItemKind.Lens when IsValidIndex(_selectedIndex, _scene.LensElements):
                UpdateScene(_scene with { Lenses = _scene.LensElements.Select((item, index) =>
                    index == _selectedIndex ? item with { IsTemporarilyHidden = value } : item).ToArray() });
                break;
            default:
                return;
        }

        CommitSelectedEdit();
    }

    public void SetSelectedPointLightEmissionAngle(double angleDegrees)
    {
        if (!double.IsFinite(angleDegrees) || _selectedKind != SceneItemKind.LightSource ||
            !IsValidIndex(_selectedIndex, _scene.LightSources))
        {
            return;
        }

        var sources = (LightSource[])_scene.LightSources.Clone();
        var source = sources[_selectedIndex];
        if (source.Kind != LightSourceKind.Point)
        {
            return;
        }

        var clamped = Math.Clamp(Math.Abs(angleDegrees), 1, 360);
        if (Math.Abs(source.SpreadDegrees - clamped) <= 1e-9)
        {
            return;
        }

        sources[_selectedIndex] = source with { SpreadDegrees = clamped };
        UpdateScene(_scene with { LightSources = sources });
        CommitSelectedEdit();
    }

    public void SetSelectedCentralAngle(double angleDegrees)
    {
        if (_selectedKind == SceneItemKind.LightSource &&
            IsValidIndex(_selectedIndex, _scene.LightSources) &&
            _scene.LightSources[_selectedIndex].Kind == LightSourceKind.Point)
        {
            SetSelectedPointLightEmissionAngle(angleDegrees);
        }
        else
        {
            SetSelectedSphericalMirrorArcAngle(angleDegrees);
        }
    }

    public void SetSelectedFocalLength(double focalLength)
    {
        if (!double.IsFinite(focalLength))
        {
            return;
        }

        var clamped = Math.Clamp(Math.Abs(focalLength), 1, 10000);
        if (_selectedKind == SceneItemKind.Lens && IsValidIndex(_selectedIndex, _scene.LensElements))
        {
            var lenses = (LensSegment[])_scene.LensElements.Clone();
            var lens = lenses[_selectedIndex];
            if (Math.Abs(lens.FocalLength - clamped) <= 1e-9)
            {
                return;
            }

            lenses[_selectedIndex] = lens with { FocalLength = clamped };
            UpdateScene(_scene with { Lenses = lenses });
        }
        else if (_selectedKind == SceneItemKind.ConcaveSphericalMirror &&
                 IsValidIndex(_selectedIndex, _scene.ConcaveSphericalMirrorElements))
        {
            SetSelectedSphericalMirrorRadius(clamped * 2);
            return;
        }
        else if (_selectedKind == SceneItemKind.ConvexSphericalMirror &&
                 IsValidIndex(_selectedIndex, _scene.ConvexSphericalMirrorElements))
        {
            SetSelectedSphericalMirrorRadius(clamped * 2);
            return;
        }
        else if (_selectedKind == SceneItemKind.ConcaveGrating &&
                 IsValidIndex(_selectedIndex, _scene.ConcaveGratingElements))
        {
            SetSelectedSphericalMirrorRadius(clamped * 2);
            return;
        }
        else
        {
            return;
        }

        CommitSelectedEdit();
    }

    public void SetSelectedSphericalMirrorRadius(double radius)
    {
        if (!double.IsFinite(radius))
        {
            return;
        }

        var clamped = Math.Clamp(Math.Abs(radius), 2, 20000);
        if (_selectedKind == SceneItemKind.ConcaveSphericalMirror &&
            IsValidIndex(_selectedIndex, _scene.ConcaveSphericalMirrorElements))
        {
            var mirrors = (ConcaveSphericalMirror[])_scene.ConcaveSphericalMirrorElements.Clone();
            var mirror = mirrors[_selectedIndex];
            if (Math.Abs(mirror.Radius - clamped) <= 1e-9)
            {
                return;
            }
            var direction = (mirror.CenterOfCurvature - mirror.Vertex).Normalized();
            mirrors[_selectedIndex] = mirror with
            {
                CenterOfCurvature = mirror.Vertex + direction * clamped
            };
            UpdateScene(_scene with { ConcaveSphericalMirrors = mirrors });
        }
        else if (_selectedKind == SceneItemKind.ConvexSphericalMirror &&
                 IsValidIndex(_selectedIndex, _scene.ConvexSphericalMirrorElements))
        {
            var mirrors = (ConvexSphericalMirror[])_scene.ConvexSphericalMirrorElements.Clone();
            var mirror = mirrors[_selectedIndex];
            if (Math.Abs(mirror.Radius - clamped) <= 1e-9)
            {
                return;
            }
            var direction = (mirror.CenterOfCurvature - mirror.Vertex).Normalized();
            mirrors[_selectedIndex] = mirror with
            {
                CenterOfCurvature = mirror.Vertex + direction * clamped
            };
            UpdateScene(_scene with { ConvexSphericalMirrors = mirrors });
        }
        else if (_selectedKind == SceneItemKind.ConcaveGrating &&
                 IsValidIndex(_selectedIndex, _scene.ConcaveGratingElements))
        {
            var gratings = (ConcaveGrating[])_scene.ConcaveGratingElements.Clone();
            var grating = gratings[_selectedIndex];
            if (Math.Abs(grating.Radius - clamped) <= 1e-9) return;
            var direction = (grating.CenterOfCurvature - grating.Vertex).Normalized();
            gratings[_selectedIndex] = grating with
                { CenterOfCurvature = grating.Vertex + direction * clamped };
            UpdateScene(_scene with { ConcaveGratings = gratings });
        }
        else
        {
            return;
        }
        CommitSelectedEdit();
    }

    public void SetSelectedSphericalMirrorArcAngle(double angleDegrees)
    {
        if (!double.IsFinite(angleDegrees))
        {
            return;
        }

        var clamped = Math.Clamp(Math.Abs(angleDegrees), 1, 359.9);
        if (_selectedKind == SceneItemKind.ConcaveSphericalMirror &&
            IsValidIndex(_selectedIndex, _scene.ConcaveSphericalMirrorElements))
        {
            var mirrors = (ConcaveSphericalMirror[])_scene.ConcaveSphericalMirrorElements.Clone();
            var mirror = mirrors[_selectedIndex];
            if (Math.Abs(mirror.ArcAngleDegrees - clamped) <= 1e-9)
            {
                return;
            }
            mirrors[_selectedIndex] = mirror with { ArcAngleDegrees = clamped };
            UpdateScene(_scene with { ConcaveSphericalMirrors = mirrors });
        }
        else if (_selectedKind == SceneItemKind.ConvexSphericalMirror &&
                 IsValidIndex(_selectedIndex, _scene.ConvexSphericalMirrorElements))
        {
            var mirrors = (ConvexSphericalMirror[])_scene.ConvexSphericalMirrorElements.Clone();
            var mirror = mirrors[_selectedIndex];
            if (Math.Abs(mirror.ArcAngleDegrees - clamped) <= 1e-9)
            {
                return;
            }
            mirrors[_selectedIndex] = mirror with { ArcAngleDegrees = clamped };
            UpdateScene(_scene with { ConvexSphericalMirrors = mirrors });
        }
        else if (_selectedKind == SceneItemKind.ConcaveGrating &&
                 IsValidIndex(_selectedIndex, _scene.ConcaveGratingElements))
        {
            var gratings = (ConcaveGrating[])_scene.ConcaveGratingElements.Clone();
            var grating = gratings[_selectedIndex];
            if (Math.Abs(grating.ArcAngleDegrees - clamped) <= 1e-9) return;
            gratings[_selectedIndex] = grating with { ArcAngleDegrees = clamped };
            UpdateScene(_scene with { ConcaveGratings = gratings });
        }
        else
        {
            return;
        }
        CommitSelectedEdit();
    }

    public void SetSelectedApertureOpening(double openingSize)
    {
        if (_selectedKind != SceneItemKind.Aperture ||
            !IsValidIndex(_selectedIndex, _scene.ApertureElements) ||
            !double.IsFinite(openingSize))
        {
            return;
        }

        var apertures = (ApertureSegment[])_scene.ApertureElements.Clone();
        var aperture = apertures[_selectedIndex];
        var length = (aperture.End - aperture.Start).Length;
        var clamped = Math.Clamp(openingSize, 0, length);
        if (Math.Abs(aperture.OpeningSize - clamped) <= 1e-9)
        {
            return;
        }

        apertures[_selectedIndex] = aperture with { OpeningSize = clamped };
        UpdateScene(_scene with { Apertures = apertures });
        CommitSelectedEdit();
    }

    public void SetSelectedGrooveDensity(double grooveDensity)
    {
        if (!double.IsFinite(grooveDensity)) return;
        var clamped = Math.Clamp(grooveDensity, 1, 5000);
        if (_selectedKind == SceneItemKind.ConcaveGrating &&
            IsValidIndex(_selectedIndex, _scene.ConcaveGratingElements))
        {
            var concaveGratings = (ConcaveGrating[])_scene.ConcaveGratingElements.Clone();
            var concaveGrating = concaveGratings[_selectedIndex];
            if (Math.Abs(concaveGrating.GrooveDensityLinesPerMillimeter - clamped) <= 1e-9) return;
            concaveGratings[_selectedIndex] = concaveGrating with
                { GrooveDensityLinesPerMillimeter = clamped };
            UpdateScene(_scene with { ConcaveGratings = concaveGratings });
            CommitSelectedEdit();
            return;
        }
        if (_selectedKind != SceneItemKind.ReflectionGrating ||
            !IsValidIndex(_selectedIndex, _scene.ReflectionGratingElements)) return;
        var gratings = (ReflectionGratingSegment[])_scene.ReflectionGratingElements.Clone();
        var grating = gratings[_selectedIndex];
        if (Math.Abs(grating.GrooveDensityLinesPerMillimeter - clamped) <= 1e-9)
        {
            return;
        }

        gratings[_selectedIndex] = grating with { GrooveDensityLinesPerMillimeter = clamped };
        UpdateScene(_scene with { ReflectionGratings = gratings });
        CommitSelectedEdit();
    }

    public void SetSelectedLensDispersionMode(LensDispersionMode mode)
    {
        if (!Enum.IsDefined(mode) || _selectedKind != SceneItemKind.Lens ||
            !IsValidIndex(_selectedIndex, _scene.LensElements))
        {
            return;
        }

        var lenses = (LensSegment[])_scene.LensElements.Clone();
        var lens = lenses[_selectedIndex];
        if (lens.DispersionMode == mode) return;
        lenses[_selectedIndex] = lens with { DispersionMode = mode };
        UpdateScene(_scene with { Lenses = lenses });
        CommitSelectedEdit();
    }

    public void SetSelectedLensDispersionLevel(int level)
    {
        if (_selectedKind != SceneItemKind.Lens ||
            !IsValidIndex(_selectedIndex, _scene.LensElements))
        {
            return;
        }

        var clamped = Math.Clamp(level, 0, 10);
        var lenses = (LensSegment[])_scene.LensElements.Clone();
        var lens = lenses[_selectedIndex];
        if (lens.DispersionLevel == clamped) return;
        lenses[_selectedIndex] = lens with { DispersionLevel = clamped };
        UpdateScene(_scene with { Lenses = lenses });
        CommitSelectedEdit();
    }

    public void SetSelectedWavelength(double wavelengthNanometers)
    {
        if (_selectedKind != SceneItemKind.LightSource ||
            !IsValidIndex(_selectedIndex, _scene.LightSources) ||
            !double.IsFinite(wavelengthNanometers))
        {
            return;
        }

        var sources = (LightSource[])_scene.LightSources.Clone();
        var source = sources[_selectedIndex];
        if (source.Spectrum != LightSpectrumKind.Monochromatic)
        {
            return;
        }

        var clamped = Math.Clamp(wavelengthNanometers, 1, 1000000);
        if (Math.Abs(source.WavelengthNanometers - clamped) <= 1e-9)
        {
            return;
        }

        sources[_selectedIndex] = source with { WavelengthNanometers = clamped };
        UpdateScene(_scene with { LightSources = sources });
        CommitSelectedEdit();
    }
}
