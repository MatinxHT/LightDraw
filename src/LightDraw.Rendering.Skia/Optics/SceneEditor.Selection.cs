using LightDraw.Core.Geometry;
using LightDraw.Core.Scene;

namespace LightDraw.Rendering.Skia.Optics;

internal sealed partial class SceneEditor
{
    public bool HitTest(Vector2D world)
    {
        var tolerance = 12 / _zoom;
        if (_selectedGroupId is { } groupId && FindGroup(groupId) is { } group &&
            SceneGeometry.Find(_scene, group.PrimaryMemberId) is { } primary)
        {
            if ((world - SceneGeometry.Origin(_scene, primary)).Length <= tolerance ||
                (world - GroupRotationHandle(primary)).Length <= tolerance)
                return true;
        }
        if (GetSelectedLegacyItem() is { } selected &&
            SelectedInteractionHandles(selected).Any(handle => (world - handle).Length <= tolerance))
            return true;
        return FindItemAt(world) is not null;
    }

    private IEnumerable<Vector2D> SelectedInteractionHandles(SceneItemRef item)
    {
        yield return SceneGeometry.Origin(_scene, item);
        switch (item.Kind)
        {
            case SceneItemKind.LightSource:
                var source = _scene.LightSources[item.Index];
                yield return source.Kind == LightSourceKind.Point
                    ? PointLightRotationHandle(source)
                    : RotationHandle(source.Position, source.End!.Value);
                break;
            case SceneItemKind.ConcaveSphericalMirror:
                var concave = _scene.ConcaveSphericalMirrorElements[item.Index];
                yield return concave.CenterOfCurvature;
                yield return SphericalMirrorRotationHandle(concave.Vertex, concave.CenterOfCurvature);
                break;
            case SceneItemKind.ConvexSphericalMirror:
                var convex = _scene.ConvexSphericalMirrorElements[item.Index];
                yield return convex.CenterOfCurvature;
                yield return SphericalMirrorRotationHandle(convex.Vertex, convex.CenterOfCurvature);
                break;
            case SceneItemKind.ConcaveGrating:
                var concaveGrating = _scene.ConcaveGratingElements[item.Index];
                yield return concaveGrating.CenterOfCurvature;
                yield return SphericalMirrorRotationHandle(
                    concaveGrating.Vertex, concaveGrating.CenterOfCurvature);
                break;
            default:
                var origin = SceneGeometry.Origin(_scene, item);
                var radians = SceneGeometry.AngleDegrees(_scene, item) * Math.PI / 180;
                yield return origin + Vector2D.FromAngle(radians).Perpendicular() * RotationHandleOffset;
                break;
        }
    }

    public void SelectInRectangle(Vector2D first, Vector2D second)
    {
        var rectangle = WorldBounds.FromCorners(first, second);
        var selected = new HashSet<Guid>();
        var handledGroups = new HashSet<Guid>();
        foreach (var item in SceneGeometry.Enumerate(_scene))
        {
            var group = FindGroupContaining(item.Id);
            if (group is not null)
            {
                if (!handledGroups.Add(group.Id)) continue;
                if (rectangle.Contains(SceneGeometry.Bounds(_scene, group.MemberIds)))
                    selected.UnionWith(group.MemberIds);
            }
            else if (rectangle.Contains(SceneGeometry.Bounds(_scene, item)))
            {
                selected.Add(item.Id);
            }
        }
        ApplySelection(selected, Guid.Empty);
    }

    private SceneItemRef? FindItemAt(Vector2D world)
    {
        var bestDistance = 10 / _zoom;
        SceneItemRef? best = null;
        void Consider(double distance, SceneItemRef item)
        {
            if (distance > bestDistance) return;
            bestDistance = distance;
            best = item;
        }

        foreach (var item in SceneGeometry.Enumerate(_scene))
        {
            var distance = item.Kind switch
            {
                SceneItemKind.LightSource => _scene.LightSources[item.Index].Kind == LightSourceKind.ParallelLine &&
                                             _scene.LightSources[item.Index].End is { } sourceEnd
                    ? DistanceToSegment(world, _scene.LightSources[item.Index].Position, sourceEnd)
                    : (world - _scene.LightSources[item.Index].Position).Length,
                SceneItemKind.Mirror => DistanceToSegment(world, _scene.Mirrors[item.Index].Start,
                    _scene.Mirrors[item.Index].End),
                SceneItemKind.ConcaveSphericalMirror => DistanceToArc(world,
                    _scene.ConcaveSphericalMirrorElements[item.Index]),
                SceneItemKind.ConvexSphericalMirror => DistanceToArc(world,
                    _scene.ConvexSphericalMirrorElements[item.Index].Vertex,
                    _scene.ConvexSphericalMirrorElements[item.Index].CenterOfCurvature,
                    _scene.ConvexSphericalMirrorElements[item.Index].ArcAngleDegrees),
                SceneItemKind.BeamSplitter => DistanceToSegment(world,
                    _scene.BeamSplitterElements[item.Index].Start, _scene.BeamSplitterElements[item.Index].End),
                SceneItemKind.Screen => DistanceToSegment(world, _scene.ScreenElements[item.Index].Start,
                    _scene.ScreenElements[item.Index].End),
                SceneItemKind.Aperture => DistanceToSegment(world, _scene.ApertureElements[item.Index].Start,
                    _scene.ApertureElements[item.Index].End),
                SceneItemKind.ReflectionGrating => DistanceToSegment(world,
                    _scene.ReflectionGratingElements[item.Index].Start,
                    _scene.ReflectionGratingElements[item.Index].End),
                SceneItemKind.ConcaveGrating => DistanceToArc(world,
                    _scene.ConcaveGratingElements[item.Index].Vertex,
                    _scene.ConcaveGratingElements[item.Index].CenterOfCurvature,
                    _scene.ConcaveGratingElements[item.Index].ArcAngleDegrees),
                SceneItemKind.Lens => DistanceToSegment(world, _scene.LensElements[item.Index].Start,
                    _scene.LensElements[item.Index].End),
                _ => double.PositiveInfinity
            };
            Consider(distance, item);
        }
        return best;
    }

    private void SelectSingleLegacyItem()
    {
        if (GetSelectedLegacyItem() is not { } item) return;
        _selectedIds.Clear();
        _selectedIds.Add(item.Id);
        _selectedGroupId = null;
        _activeElementId = item.Id;
    }

    private SceneItemRef? GetSelectedLegacyItem() =>
        SceneGeometry.Enumerate(_scene).FirstOrDefault(item =>
            item.Kind == _selectedKind && item.Index == _selectedIndex) is { Id: var id } item && id != Guid.Empty
            ? item : null;

    private void ApplySelection(IEnumerable<Guid> ids, Guid activeId)
    {
        _selectedIds.Clear();
        _selectedIds.UnionWith(ids);
        var exactGroup = _scene.ElementGroups.FirstOrDefault(group =>
            group.MemberIds.Length == _selectedIds.Count && group.MemberIds.All(_selectedIds.Contains));
        _selectedGroupId = exactGroup?.Id;
        _activeElementId = activeId != Guid.Empty
            ? activeId
            : exactGroup?.PrimaryMemberId ?? _selectedIds.FirstOrDefault();
        ClearLegacySelection();
        if (_selectedIds.Count == 1 && _selectedGroupId is null &&
            SceneGeometry.Find(_scene, _selectedIds.First()) is { } item)
        {
            _selectedKind = item.Kind;
            _selectedIndex = item.Index;
        }
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearLegacySelection()
    {
        _selectedKind = SceneItemKind.None;
        _selectedIndex = -1;
    }

    private CanvasSelection? CreateSelection()
    {
        if (_selectedGroupId is { } groupId && FindGroup(groupId) is { } group &&
            SceneGeometry.Find(_scene, group.PrimaryMemberId) is { } primary)
        {
            var origin = SceneGeometry.Origin(_scene, primary);
            var hideableMembers = group.MemberIds
                .Select(id => SceneGeometry.Find(_scene, id))
                .Where(item => item is { Kind: not SceneItemKind.LightSource })
                .Select(item => item!.Value)
                .ToArray();
            return new CanvasSelection(CanvasSelectionKind.Group, $"组合（{group.MemberIds.Length} 个元件）",
                true, origin.X, origin.Y, SceneGeometry.AngleDegrees(_scene, primary), null, null,
                MemberCount: group.MemberIds.Length, CanUngroup: true,
                CanSetPrimary: _activeElementId is { } active && active != group.PrimaryMemberId,
                ElementName: group.Name, CanRename: true,
                CanTemporarilyHide: hideableMembers.Length > 0,
                IsTemporarilyHidden: hideableMembers.Length > 0 &&
                    hideableMembers.All(IsTemporarilyHidden));
        }
        if (_selectedIds.Count > 1)
        {
            return new CanvasSelection(CanvasSelectionKind.Multiple, $"已选择 {_selectedIds.Count} 个元件",
                false, 0, 0, 0, null, null, MemberCount: _selectedIds.Count, CanGroup: true);
        }

        switch (_selectedKind)
        {
            case SceneItemKind.LightSource when IsValidIndex(_selectedIndex, _scene.LightSources):
                var source = _scene.LightSources[_selectedIndex];
                if (source.Kind == LightSourceKind.ParallelLine && source.End is { } sourceEnd)
                {
                    var sourceOrigin = (source.Position + sourceEnd) / 2;
                    return new CanvasSelection(CanvasSelectionKind.ParallelLight,
                        source.Spectrum == LightSpectrumKind.Composite ? "复色平行光源" : "单色平行光源",
                        true,
                        sourceOrigin.X, sourceOrigin.Y,
                        SegmentAngleDegrees(source.Position, sourceEnd), null,
                        (sourceEnd - sourceOrigin).Length * 2,
                        SecondOriginX: RotationHandle(source.Position, sourceEnd).X,
                        SecondOriginY: RotationHandle(source.Position, sourceEnd).Y,
                        WavelengthNanometers: source.Spectrum == LightSpectrumKind.Monochromatic
                            ? source.WavelengthNanometers
                            : null,
                        ElementName: source.Name, CanRename: true);
                }
                var pointLightHandle = PointLightRotationHandle(source);
                return new CanvasSelection(CanvasSelectionKind.PointLight,
                    source.Spectrum == LightSpectrumKind.Composite ? "复色点光源" : "单色点光源",
                    true,
                    source.Position.X, source.Position.Y, source.DirectionDegrees, null, null,
                    SecondOriginX: pointLightHandle.X,
                    SecondOriginY: pointLightHandle.Y,
                    EmissionAngleDegrees: Math.Clamp(Math.Abs(source.SpreadDegrees), 1, 360),
                    WavelengthNanometers: source.Spectrum == LightSpectrumKind.Monochromatic
                        ? source.WavelengthNanometers
                        : null,
                    ElementName: source.Name, CanRename: true);
            case SceneItemKind.Mirror when IsValidIndex(_selectedIndex, _scene.Mirrors):
                var mirror = _scene.Mirrors[_selectedIndex];
                var mirrorOrigin = (mirror.Start + mirror.End) / 2;
                return new CanvasSelection(CanvasSelectionKind.Mirror, "平面反光镜", true,
                    mirrorOrigin.X, mirrorOrigin.Y, SegmentAngleDegrees(mirror.Start, mirror.End), null,
                    (mirror.End - mirrorOrigin).Length * 2,
                    SecondOriginX: RotationHandle(mirror.Start, mirror.End).X,
                    SecondOriginY: RotationHandle(mirror.Start, mirror.End).Y,
                    ElementName: mirror.Name, CanRename: true, CanTemporarilyHide: true,
                    IsTemporarilyHidden: mirror.IsTemporarilyHidden);
            case SceneItemKind.ConcaveSphericalMirror when IsValidIndex(
                _selectedIndex, _scene.ConcaveSphericalMirrorElements):
                var sphericalMirror = _scene.ConcaveSphericalMirrorElements[_selectedIndex];
                return new CanvasSelection(
                    CanvasSelectionKind.ConcaveSphericalMirror,
                    "理想凹球面镜",
                    true,
                    sphericalMirror.Vertex.X,
                    sphericalMirror.Vertex.Y,
                    SegmentAngleDegrees(sphericalMirror.Vertex, sphericalMirror.CenterOfCurvature),
                    sphericalMirror.FocalLength,
                    null,
                    Radius: sphericalMirror.Radius,
                    ArcAngleDegrees: sphericalMirror.ArcAngleDegrees,
                    SecondOriginX: sphericalMirror.CenterOfCurvature.X,
                    SecondOriginY: sphericalMirror.CenterOfCurvature.Y,
                    ElementName: sphericalMirror.Name, CanRename: true, CanTemporarilyHide: true,
                    IsTemporarilyHidden: sphericalMirror.IsTemporarilyHidden);
            case SceneItemKind.ConvexSphericalMirror when IsValidIndex(
                _selectedIndex, _scene.ConvexSphericalMirrorElements):
                var convexSphericalMirror = _scene.ConvexSphericalMirrorElements[_selectedIndex];
                return new CanvasSelection(
                    CanvasSelectionKind.ConvexSphericalMirror,
                    "理想凸球面镜",
                    true,
                    convexSphericalMirror.Vertex.X,
                    convexSphericalMirror.Vertex.Y,
                    SegmentAngleDegrees(convexSphericalMirror.Vertex,
                        convexSphericalMirror.CenterOfCurvature),
                    convexSphericalMirror.FocalLength,
                    null,
                    Radius: convexSphericalMirror.Radius,
                    ArcAngleDegrees: convexSphericalMirror.ArcAngleDegrees,
                    SecondOriginX: convexSphericalMirror.CenterOfCurvature.X,
                    SecondOriginY: convexSphericalMirror.CenterOfCurvature.Y,
                    ElementName: convexSphericalMirror.Name, CanRename: true, CanTemporarilyHide: true,
                    IsTemporarilyHidden: convexSphericalMirror.IsTemporarilyHidden);
            case SceneItemKind.BeamSplitter when IsValidIndex(_selectedIndex, _scene.BeamSplitterElements):
                var beamSplitter = _scene.BeamSplitterElements[_selectedIndex];
                var beamSplitterOrigin = (beamSplitter.Start + beamSplitter.End) / 2;
                return new CanvasSelection(CanvasSelectionKind.BeamSplitter, "平面分光镜", true,
                    beamSplitterOrigin.X, beamSplitterOrigin.Y,
                    SegmentAngleDegrees(beamSplitter.Start, beamSplitter.End), null,
                    (beamSplitter.End - beamSplitterOrigin).Length * 2,
                    SecondOriginX: RotationHandle(beamSplitter.Start, beamSplitter.End).X,
                    SecondOriginY: RotationHandle(beamSplitter.Start, beamSplitter.End).Y,
                    ElementName: beamSplitter.Name, CanRename: true, CanTemporarilyHide: true,
                    IsTemporarilyHidden: beamSplitter.IsTemporarilyHidden);
            case SceneItemKind.Screen when IsValidIndex(_selectedIndex, _scene.ScreenElements):
                var screen = _scene.ScreenElements[_selectedIndex];
                var screenOrigin = (screen.Start + screen.End) / 2;
                return new CanvasSelection(CanvasSelectionKind.Screen, "光屏", true,
                    screenOrigin.X, screenOrigin.Y, SegmentAngleDegrees(screen.Start, screen.End), null,
                    (screen.End - screenOrigin).Length * 2,
                    SecondOriginX: RotationHandle(screen.Start, screen.End).X,
                    SecondOriginY: RotationHandle(screen.Start, screen.End).Y,
                    ElementName: screen.Name, CanRename: true, CanTemporarilyHide: true,
                    IsTemporarilyHidden: screen.IsTemporarilyHidden);
            case SceneItemKind.Aperture when IsValidIndex(_selectedIndex, _scene.ApertureElements):
                var aperture = _scene.ApertureElements[_selectedIndex];
                var apertureOrigin = (aperture.Start + aperture.End) / 2;
                return new CanvasSelection(CanvasSelectionKind.Aperture, "光阑", true,
                    apertureOrigin.X, apertureOrigin.Y, SegmentAngleDegrees(aperture.Start, aperture.End), null,
                    (aperture.End - apertureOrigin).Length * 2, aperture.OpeningSize,
                    SecondOriginX: RotationHandle(aperture.Start, aperture.End).X,
                    SecondOriginY: RotationHandle(aperture.Start, aperture.End).Y,
                    ElementName: aperture.Name, CanRename: true, CanTemporarilyHide: true,
                    IsTemporarilyHidden: aperture.IsTemporarilyHidden);
            case SceneItemKind.ReflectionGrating when IsValidIndex(_selectedIndex, _scene.ReflectionGratingElements):
                var grating = _scene.ReflectionGratingElements[_selectedIndex];
                var gratingOrigin = (grating.Start + grating.End) / 2;
                return new CanvasSelection(CanvasSelectionKind.ReflectionGrating, "反射光栅", true,
                    gratingOrigin.X, gratingOrigin.Y, SegmentAngleDegrees(grating.Start, grating.End), null,
                    (grating.End - gratingOrigin).Length * 2, null,
                    grating.GrooveDensityLinesPerMillimeter,
                    SecondOriginX: RotationHandle(grating.Start, grating.End).X,
                    SecondOriginY: RotationHandle(grating.Start, grating.End).Y,
                    ElementName: grating.Name, CanRename: true, CanTemporarilyHide: true,
                    IsTemporarilyHidden: grating.IsTemporarilyHidden);
            case SceneItemKind.ConcaveGrating when IsValidIndex(_selectedIndex, _scene.ConcaveGratingElements):
                var concaveGrating = _scene.ConcaveGratingElements[_selectedIndex];
                return new CanvasSelection(
                    CanvasSelectionKind.ConcaveGrating, "凹面光栅", true,
                    concaveGrating.Vertex.X, concaveGrating.Vertex.Y,
                    SegmentAngleDegrees(concaveGrating.Vertex, concaveGrating.CenterOfCurvature),
                    concaveGrating.FocalLength, null,
                    GrooveDensity: concaveGrating.GrooveDensityLinesPerMillimeter,
                    Radius: concaveGrating.Radius,
                    ArcAngleDegrees: concaveGrating.ArcAngleDegrees,
                    SecondOriginX: concaveGrating.CenterOfCurvature.X,
                    SecondOriginY: concaveGrating.CenterOfCurvature.Y,
                    ElementName: concaveGrating.Name, CanRename: true, CanTemporarilyHide: true,
                    IsTemporarilyHidden: concaveGrating.IsTemporarilyHidden);
            case SceneItemKind.Lens when IsValidIndex(_selectedIndex, _scene.LensElements):
                var lens = _scene.LensElements[_selectedIndex];
                var lensOrigin = (lens.Start + lens.End) / 2;
                var selectionKind = lens.Kind == LensKind.Convex
                    ? CanvasSelectionKind.ConvexLens
                    : CanvasSelectionKind.ConcaveLens;
                var displayName = lens.Kind == LensKind.Convex ? "凸透镜" : "凹透镜";
                return new CanvasSelection(selectionKind, displayName, true,
                    lensOrigin.X, lensOrigin.Y, SegmentAngleDegrees(lens.Start, lens.End), lens.FocalLength,
                    (lens.End - lensOrigin).Length * 2,
                    SecondOriginX: RotationHandle(lens.Start, lens.End).X,
                    SecondOriginY: RotationHandle(lens.Start, lens.End).Y,
                    DispersionMode: lens.DispersionMode,
                    DispersionLevel: lens.DispersionLevel,
                    ElementName: lens.Name, CanRename: true, CanTemporarilyHide: true,
                    IsTemporarilyHidden: lens.IsTemporarilyHidden);
            default:
                return null;
        }
    }

    public void ClearSelection()
    {
        if (_selectedKind == SceneItemKind.None && _selectedIndex < 0 && _selectedIds.Count == 0)
        {
            return;
        }

        _selectedKind = SceneItemKind.None;
        _selectedIndex = -1;
        _selectedIds.Clear();
        _selectedGroupId = null;
        _activeElementId = null;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool IsTemporarilyHidden(SceneItemRef item) => item.Kind switch
    {
        SceneItemKind.Mirror => _scene.Mirrors[item.Index].IsTemporarilyHidden,
        SceneItemKind.ConcaveSphericalMirror => _scene.ConcaveSphericalMirrorElements[item.Index].IsTemporarilyHidden,
        SceneItemKind.ConvexSphericalMirror => _scene.ConvexSphericalMirrorElements[item.Index].IsTemporarilyHidden,
        SceneItemKind.BeamSplitter => _scene.BeamSplitterElements[item.Index].IsTemporarilyHidden,
        SceneItemKind.Screen => _scene.ScreenElements[item.Index].IsTemporarilyHidden,
        SceneItemKind.Aperture => _scene.ApertureElements[item.Index].IsTemporarilyHidden,
        SceneItemKind.ReflectionGrating => _scene.ReflectionGratingElements[item.Index].IsTemporarilyHidden,
        SceneItemKind.ConcaveGrating => _scene.ConcaveGratingElements[item.Index].IsTemporarilyHidden,
        SceneItemKind.Lens => _scene.LensElements[item.Index].IsTemporarilyHidden,
        _ => false
    };
}
