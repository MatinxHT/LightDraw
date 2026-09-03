using System.Diagnostics;
using LightDraw.Core.Geometry;
using LightDraw.Core.Scene;

namespace LightDraw.Rendering.Skia.Optics;

internal sealed class SceneEditor
{
    private const double RotationHandleOffset = 100;
    private const double DefaultLensFocalLength = 300;
    private OpticalScene _scene = OpticalScene.CreateEmpty();
    private double _zoom = 1;
    private SceneItemKind _movingKind;
    private MoveDragMode _moveDragMode;
    private int _movingIndex = -1;
    private Vector2D _lastMoveWorld;
    private bool _moveChanged;
    private bool _moveSimulationDirty;
    private long _lastMoveSimulationTimestamp;
    private SceneItemKind _selectedKind;
    private int _selectedIndex = -1;
    private readonly HashSet<Guid> _selectedIds = [];
    private Guid? _selectedGroupId;
    private Guid? _activeElementId;
    private Guid? _movingGroupId;

    public OpticalScene Scene => _scene;
    public CanvasSelection? Selection => CreateSelection();
    public SceneItemKind SelectedKind => _selectedKind;
    public int SelectedIndex => _selectedIndex;
    public IReadOnlySet<Guid> SelectedIds => _selectedIds;
    public Guid? SelectedGroupId => _selectedGroupId;
    public Guid? ActiveElementId => _activeElementId;
    public bool IsMoving => _movingKind != SceneItemKind.None || _movingGroupId is not null;

    public event EventHandler? SceneUpdated;
    public event EventHandler? SceneCommitted;
    public event EventHandler? PreviewRequested;
    public event EventHandler? SelectionChanged;
    public event EventHandler? InteractionStateChanged;

    public void SetScene(OpticalScene scene)
    {
        _scene = Normalize(scene);
        _movingKind = SceneItemKind.None;
        _moveDragMode = MoveDragMode.None;
        _movingIndex = -1;
        _moveChanged = false;
        _moveSimulationDirty = false;
        _selectedIds.Clear();
        _selectedGroupId = null;
        _activeElementId = null;
        _movingGroupId = null;
        ClearSelection();
    }

    public void SetZoom(double zoom) => _zoom = zoom;

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

    public bool EndMove()
    {
        if (!IsMoving) return false;
        _movingKind = SceneItemKind.None;
        _movingGroupId = null;
        _moveDragMode = MoveDragMode.None;
        _movingIndex = -1;
        var changed = _moveChanged;
        _moveChanged = false;
        if (changed)
        {
            if (_moveSimulationDirty)
            {
                PreviewRequested?.Invoke(this, EventArgs.Empty);
                _moveSimulationDirty = false;
            }
            SceneCommitted?.Invoke(this, EventArgs.Empty);
        }
        InteractionStateChanged?.Invoke(this, EventArgs.Empty);
        return changed;
    }

    private static OpticalScene Normalize(OpticalScene scene) => scene with
    {
        LightSources = (scene.LightSources ?? [])
            .Select((source, index) => source with
            {
                Id = EnsureId(source.Id),
                Name = NormalizeName(source.Name,
                    source.Kind == LightSourceKind.Point
                        ? source.Spectrum == LightSpectrumKind.Composite ? "Composite Point Source" : "Point Source"
                        : source.Spectrum == LightSpectrumKind.Composite ? "Composite Parallel Source" : "Parallel Source",
                    index + 1),
                WavelengthNanometers = source.Spectrum == LightSpectrumKind.Composite
                    ? ReferenceWavelength(source.Spectrum)
                    : NormalizeMonochromaticWavelength(source.WavelengthNanometers)
            })
            .ToArray(),
        Mirrors = (scene.Mirrors ?? []).Select((item, index) => item with
            { Id = EnsureId(item.Id), Name = NormalizeName(item.Name, "Mirror", index + 1) }).ToArray(),
        ConcaveSphericalMirrors = scene.ConcaveSphericalMirrorElements
            .Select((item, index) => item with { Id = EnsureId(item.Id),
                Name = NormalizeName(item.Name, "Concave Spherical Mirror", index + 1) }).ToArray(),
        ConvexSphericalMirrors = scene.ConvexSphericalMirrorElements
            .Select((item, index) => item with { Id = EnsureId(item.Id),
                Name = NormalizeName(item.Name, "Convex Spherical Mirror", index + 1) }).ToArray(),
        Lenses = scene.LensElements.Select((lens, index) => lens with
        {
            Id = EnsureId(lens.Id),
            Name = NormalizeName(lens.Name, lens.Kind == LensKind.Convex ? "Convex Lens" : "Concave Lens", index + 1),
            DispersionMode = Enum.IsDefined(lens.DispersionMode)
                ? lens.DispersionMode
                : LensDispersionMode.None,
            DispersionLevel = Math.Clamp(lens.DispersionLevel, 0, 10)
        }).ToArray(),
        Screens = scene.ScreenElements.Select((item, index) => item with { Id = EnsureId(item.Id),
            Name = NormalizeName(item.Name, "Screen", index + 1) }).ToArray(),
        Apertures = scene.ApertureElements.Select((item, index) => item with { Id = EnsureId(item.Id),
            Name = NormalizeName(item.Name, "Aperture", index + 1) }).ToArray(),
        ReflectionGratings = scene.ReflectionGratingElements
            .Select((item, index) => item with { Id = EnsureId(item.Id),
                Name = NormalizeName(item.Name, "Reflection Grating", index + 1) }).ToArray(),
        BeamSplitters = scene.BeamSplitterElements.Select((item, index) => item with { Id = EnsureId(item.Id),
            Name = NormalizeName(item.Name, "Beam Splitter", index + 1) }).ToArray(),
        Groups = NormalizeGroups(scene)
    };

    private static Guid EnsureId(Guid id) => id == Guid.Empty ? Guid.NewGuid() : id;

    private static string NormalizeName(string? name, string baseName, int number) =>
        string.IsNullOrWhiteSpace(name) ? $"{baseName} {number}" : name.Trim();

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
        foreach (var item in _scene.LensElements) if (!string.IsNullOrWhiteSpace(item.Name)) yield return item.Name;
        foreach (var item in _scene.ElementGroups) if (!string.IsNullOrWhiteSpace(item.Name)) yield return item.Name;
    }

    private static ElementGroup[] NormalizeGroups(OpticalScene scene)
    {
        var valid = SceneGeometry.Enumerate(scene).Select(item => item.Id)
            .Where(id => id != Guid.Empty).ToHashSet();
        var claimed = new HashSet<Guid>();
        return scene.ElementGroups.Select((group, index) =>
        {
            var members = (group.MemberIds ?? []).Where(id => valid.Contains(id) && claimed.Add(id))
                .Distinct().ToArray();
            return group with
            {
                Id = EnsureId(group.Id), MemberIds = members,
                Name = NormalizeName(group.Name, "Group", index + 1),
                PrimaryMemberId = members.Contains(group.PrimaryMemberId)
                    ? group.PrimaryMemberId : members.FirstOrDefault()
            };
        }).Where(group => group.MemberIds.Length >= 2).ToArray();
    }

    private static double ReferenceWavelength(LightSpectrumKind spectrum) =>
        spectrum == LightSpectrumKind.Composite
            ? LightSource.CompositeGreenWavelengthNanometers
            : LightSource.MonochromaticWavelengthNanometers;

    private static double NormalizeMonochromaticWavelength(double wavelengthNanometers) =>
        double.IsFinite(wavelengthNanometers) && wavelengthNanometers > 0
            ? wavelengthNanometers
            : LightSource.MonochromaticWavelengthNanometers;

    public void RotateSelectedBy(double degrees)
    {
        var selection = CreateSelection();
        if (selection is null || !selection.CanRotate)
        {
            return;
        }

        SetSelectedAngle(selection.AngleDegrees + degrees);
    }

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
            case SceneItemKind.Lens when IsValidIndex(_selectedIndex, _scene.LensElements):
                UpdateScene(_scene with { Lenses = _scene.LensElements.Select((item, index) =>
                    index == _selectedIndex ? item with { IsTemporarilyHidden = value } : item).ToArray() });
                break;
            default:
                return;
        }

        CommitSelectedEdit();
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
        if (_selectedKind != SceneItemKind.ReflectionGrating ||
            !IsValidIndex(_selectedIndex, _scene.ReflectionGratingElements) ||
            !double.IsFinite(grooveDensity))
        {
            return;
        }

        var gratings = (ReflectionGratingSegment[])_scene.ReflectionGratingElements.Clone();
        var grating = gratings[_selectedIndex];
        var clamped = Math.Clamp(grooveDensity, 1, 5000);
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
    public bool TryBeginMove(Vector2D world)
    {
        if (TryBeginGroupInteraction(world, out var beganGroupMove))
        {
            return beganGroupMove;
        }

        var previouslySelectedKind = _selectedKind;
        var previouslySelectedIndex = _selectedIndex;
        _movingKind = SceneItemKind.None;
        _movingGroupId = null;
        _moveDragMode = MoveDragMode.None;
        _movingIndex = -1;

        var bestDistance = 12 / _zoom;
        for (var index = 0; index < _scene.LightSources.Length; index++)
        {
            var source = _scene.LightSources[index];
            if (source.Kind == LightSourceKind.Point)
            {
                SelectIfCloser((world - source.Position).Length,
                    SceneItemKind.LightSource, index, MoveDragMode.Translate, ref bestDistance);
                SelectIfCloser((world - PointLightRotationHandle(source)).Length,
                    SceneItemKind.LightSource, index, MoveDragMode.RotationHandle,
                    ref bestDistance);
                continue;
            }
            if (source.End is not { } end)
            {
                continue;
            }

            SelectIfCloser((world - (source.Position + end) / 2).Length,
                SceneItemKind.LightSource, index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - RotationHandle(source.Position, end)).Length,
                SceneItemKind.LightSource, index, MoveDragMode.RotationHandle, ref bestDistance);
        }

        for (var index = 0; index < _scene.Mirrors.Length; index++)
        {
            var mirror = _scene.Mirrors[index];
            SelectIfCloser((world - (mirror.Start + mirror.End) / 2).Length,
                SceneItemKind.Mirror, index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - RotationHandle(mirror.Start, mirror.End)).Length,
                SceneItemKind.Mirror, index, MoveDragMode.RotationHandle, ref bestDistance);
        }

        for (var index = 0; index < _scene.ConcaveSphericalMirrorElements.Length; index++)
        {
            var mirror = _scene.ConcaveSphericalMirrorElements[index];
            SelectIfCloser((world - mirror.Vertex).Length, SceneItemKind.ConcaveSphericalMirror,
                index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - mirror.CenterOfCurvature).Length,
                SceneItemKind.ConcaveSphericalMirror, index, MoveDragMode.DirectionHandle,
                ref bestDistance);
            SelectIfCloser((world - SphericalMirrorRotationHandle(
                    mirror.Vertex, mirror.CenterOfCurvature)).Length,
                SceneItemKind.ConcaveSphericalMirror, index, MoveDragMode.RotationHandle,
                ref bestDistance);
        }

        for (var index = 0; index < _scene.ConvexSphericalMirrorElements.Length; index++)
        {
            var mirror = _scene.ConvexSphericalMirrorElements[index];
            SelectIfCloser((world - mirror.Vertex).Length, SceneItemKind.ConvexSphericalMirror,
                index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - mirror.CenterOfCurvature).Length,
                SceneItemKind.ConvexSphericalMirror, index, MoveDragMode.DirectionHandle,
                ref bestDistance);
            SelectIfCloser((world - SphericalMirrorRotationHandle(
                    mirror.Vertex, mirror.CenterOfCurvature)).Length,
                SceneItemKind.ConvexSphericalMirror, index, MoveDragMode.RotationHandle,
                ref bestDistance);
        }

        for (var index = 0; index < _scene.BeamSplitterElements.Length; index++)
        {
            var beamSplitter = _scene.BeamSplitterElements[index];
            SelectIfCloser((world - (beamSplitter.Start + beamSplitter.End) / 2).Length,
                SceneItemKind.BeamSplitter, index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - RotationHandle(beamSplitter.Start, beamSplitter.End)).Length,
                SceneItemKind.BeamSplitter, index, MoveDragMode.RotationHandle, ref bestDistance);
        }

        for (var index = 0; index < _scene.ScreenElements.Length; index++)
        {
            var screen = _scene.ScreenElements[index];
            SelectIfCloser((world - (screen.Start + screen.End) / 2).Length,
                SceneItemKind.Screen, index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - RotationHandle(screen.Start, screen.End)).Length,
                SceneItemKind.Screen, index, MoveDragMode.RotationHandle, ref bestDistance);
        }

        for (var index = 0; index < _scene.ApertureElements.Length; index++)
        {
            var aperture = _scene.ApertureElements[index];
            SelectIfCloser((world - (aperture.Start + aperture.End) / 2).Length,
                SceneItemKind.Aperture, index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - RotationHandle(aperture.Start, aperture.End)).Length,
                SceneItemKind.Aperture, index, MoveDragMode.RotationHandle, ref bestDistance);
        }

        for (var index = 0; index < _scene.ReflectionGratingElements.Length; index++)
        {
            var grating = _scene.ReflectionGratingElements[index];
            SelectIfCloser((world - (grating.Start + grating.End) / 2).Length,
                SceneItemKind.ReflectionGrating, index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - RotationHandle(grating.Start, grating.End)).Length,
                SceneItemKind.ReflectionGrating, index, MoveDragMode.RotationHandle,
                ref bestDistance);
        }

        for (var index = 0; index < _scene.LensElements.Length; index++)
        {
            var lens = _scene.LensElements[index];
            SelectIfCloser((world - (lens.Start + lens.End) / 2).Length,
                SceneItemKind.Lens, index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - RotationHandle(lens.Start, lens.End)).Length,
                SceneItemKind.Lens, index, MoveDragMode.RotationHandle, ref bestDistance);
        }

        if (_movingKind == SceneItemKind.None)
        {
            bestDistance = 10 / _zoom;
            FindTranslatableItem(world, ref bestDistance);
            if (_movingKind != SceneItemKind.None)
            {
                _selectedKind = _movingKind;
                _selectedIndex = _movingIndex;
                SelectSingleLegacyItem();
                _movingKind = SceneItemKind.None;
                _movingIndex = -1;
                _moveDragMode = MoveDragMode.None;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                return false;
            }
        }

        if (_movingKind == SceneItemKind.None)
        {
            ClearSelection();
            return false;
        }

        if (_movingKind != previouslySelectedKind || _movingIndex != previouslySelectedIndex)
        {
            _selectedKind = _movingKind;
            _selectedIndex = _movingIndex;
            SelectSingleLegacyItem();
            _movingKind = SceneItemKind.None;
            _movingIndex = -1;
            _moveDragMode = MoveDragMode.None;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            return false;
        }

        _selectedKind = _movingKind;
        _selectedIndex = _movingIndex;
        SelectSingleLegacyItem();
        _lastMoveWorld = world;
        _moveChanged = false;
        _moveSimulationDirty = false;
        _lastMoveSimulationTimestamp = 0;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InteractionStateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

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

    public bool GroupSelection()
    {
        if (_selectedIds.Count < 2) return false;
        var members = _selectedIds.Where(id => SceneGeometry.Find(_scene, id) is not null)
            .Distinct().ToArray();
        if (members.Length < 2) return false;
        var primary = _activeElementId is { } active && members.Contains(active) ? active : members[0];
        var intersectingGroups = _scene.ElementGroups
            .Where(group => group.MemberIds.Any(members.Contains)).Select(group => group.Id).ToHashSet();
        var newGroup = new ElementGroup(Guid.NewGuid(), members, primary, NextElementName("Group"));
        UpdateScene(_scene with
        {
            Groups = [.. _scene.ElementGroups.Where(group => !intersectingGroups.Contains(group.Id)), newGroup]
        });
        _selectedGroupId = newGroup.Id;
        _activeElementId = primary;
        ClearLegacySelection();
        CommitSelectedEdit();
        return true;
    }

    public bool UngroupSelection()
    {
        if (_selectedGroupId is not { } groupId || FindGroup(groupId) is not { } group) return false;
        UpdateScene(_scene with { Groups = _scene.ElementGroups.Where(item => item.Id != groupId).ToArray() });
        _selectedGroupId = null;
        _selectedIds.Clear();
        _selectedIds.UnionWith(group.MemberIds);
        _activeElementId = group.PrimaryMemberId;
        ClearLegacySelection();
        CommitSelectedEdit();
        return true;
    }

    public bool SetActiveMemberAsPrimary()
    {
        if (_selectedGroupId is not { } groupId || _activeElementId is not { } active ||
            FindGroup(groupId) is not { } group || !group.MemberIds.Contains(active) ||
            group.PrimaryMemberId == active) return false;
        UpdateScene(_scene with
        {
            Groups = _scene.ElementGroups.Select(item => item.Id == groupId
                ? item with { PrimaryMemberId = active } : item).ToArray()
        });
        CommitSelectedEdit();
        return true;
    }

    public void DeleteItemAt(Vector2D world)
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

        for (var index = 0; index < _scene.ConcaveSphericalMirrorElements.Length; index++)
        {
            var mirror = _scene.ConcaveSphericalMirrorElements[index];
            Consider(DistanceToArc(world, mirror), SceneItemKind.ConcaveSphericalMirror, index);
        }

        for (var index = 0; index < _scene.ConvexSphericalMirrorElements.Length; index++)
        {
            var mirror = _scene.ConvexSphericalMirrorElements[index];
            Consider(DistanceToArc(world, mirror.Vertex, mirror.CenterOfCurvature,
                mirror.ArcAngleDegrees), SceneItemKind.ConvexSphericalMirror, index);
        }

        for (var index = 0; index < _scene.BeamSplitterElements.Length; index++)
        {
            var beamSplitter = _scene.BeamSplitterElements[index];
            Consider(DistanceToSegment(world, beamSplitter.Start, beamSplitter.End),
                SceneItemKind.BeamSplitter, index);
        }

        for (var index = 0; index < _scene.ScreenElements.Length; index++)
        {
            var screen = _scene.ScreenElements[index];
            Consider(DistanceToSegment(world, screen.Start, screen.End), SceneItemKind.Screen, index);
        }

        for (var index = 0; index < _scene.ApertureElements.Length; index++)
        {
            var aperture = _scene.ApertureElements[index];
            Consider(DistanceToSegment(world, aperture.Start, aperture.End), SceneItemKind.Aperture, index);
        }

        for (var index = 0; index < _scene.ReflectionGratingElements.Length; index++)
        {
            var grating = _scene.ReflectionGratingElements[index];
            Consider(DistanceToSegment(world, grating.Start, grating.End),
                SceneItemKind.ReflectionGrating, index);
        }

        for (var index = 0; index < _scene.LensElements.Length; index++)
        {
            var lens = _scene.LensElements[index];
            Consider(DistanceToSegment(world, lens.Start, lens.End), SceneItemKind.Lens, index);
        }

        var hitItem = SceneGeometry.Enumerate(_scene)
            .FirstOrDefault(item => item.Kind == itemKind && item.Index == itemIndex);
        if (hitItem.Id != Guid.Empty && FindGroupContaining(hitItem.Id) is { } hitGroup)
        {
            UpdateScene(RemoveItems(_scene, hitGroup.MemberIds.ToHashSet()) with
            {
                Groups = _scene.ElementGroups.Where(group => group.Id != hitGroup.Id).ToArray()
            });
            ClearSelection();
            PreviewRequested?.Invoke(this, EventArgs.Empty);
            SceneCommitted?.Invoke(this, EventArgs.Empty);
            InteractionStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        switch (itemKind)
        {
            case SceneItemKind.LightSource:
                UpdateScene(_scene with { LightSources = RemoveAt(_scene.LightSources, itemIndex) });
                break;
            case SceneItemKind.Mirror:
                UpdateScene(_scene with { Mirrors = RemoveAt(_scene.Mirrors, itemIndex) });
                break;
            case SceneItemKind.ConcaveSphericalMirror:
                UpdateScene(_scene with
                {
                    ConcaveSphericalMirrors = RemoveAt(_scene.ConcaveSphericalMirrorElements, itemIndex)
                });
                break;
            case SceneItemKind.ConvexSphericalMirror:
                UpdateScene(_scene with
                {
                    ConvexSphericalMirrors = RemoveAt(_scene.ConvexSphericalMirrorElements, itemIndex)
                });
                break;
            case SceneItemKind.BeamSplitter:
                UpdateScene(_scene with
                {
                    BeamSplitters = RemoveAt(_scene.BeamSplitterElements, itemIndex)
                });
                break;
            case SceneItemKind.Screen:
                UpdateScene(_scene with { Screens = RemoveAt(_scene.ScreenElements, itemIndex) });
                break;
            case SceneItemKind.Aperture:
                UpdateScene(_scene with { Apertures = RemoveAt(_scene.ApertureElements, itemIndex) });
                break;
            case SceneItemKind.ReflectionGrating:
                UpdateScene(_scene with
                {
                    ReflectionGratings = RemoveAt(_scene.ReflectionGratingElements, itemIndex)
                });
                break;
            case SceneItemKind.Lens:
                UpdateScene(_scene with { Lenses = RemoveAt(_scene.LensElements, itemIndex) });
                break;
            default:
                return;
        }

        PreviewRequested?.Invoke(this, EventArgs.Empty);
        SceneCommitted?.Invoke(this, EventArgs.Empty);
        InteractionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static OpticalScene RemoveItems(OpticalScene scene, IReadOnlySet<Guid> ids) => scene with
    {
        LightSources = scene.LightSources.Where(item => !ids.Contains(item.Id)).ToArray(),
        Mirrors = scene.Mirrors.Where(item => !ids.Contains(item.Id)).ToArray(),
        ConcaveSphericalMirrors = scene.ConcaveSphericalMirrorElements.Where(item => !ids.Contains(item.Id)).ToArray(),
        ConvexSphericalMirrors = scene.ConvexSphericalMirrorElements.Where(item => !ids.Contains(item.Id)).ToArray(),
        BeamSplitters = scene.BeamSplitterElements.Where(item => !ids.Contains(item.Id)).ToArray(),
        Screens = scene.ScreenElements.Where(item => !ids.Contains(item.Id)).ToArray(),
        Apertures = scene.ApertureElements.Where(item => !ids.Contains(item.Id)).ToArray(),
        ReflectionGratings = scene.ReflectionGratingElements.Where(item => !ids.Contains(item.Id)).ToArray(),
        Lenses = scene.LensElements.Where(item => !ids.Contains(item.Id)).ToArray()
    };

    private static T[] RemoveAt<T>(T[] items, int index) =>
        [.. items[..index], .. items[(index + 1)..]];

    private bool TryBeginGroupInteraction(Vector2D world, out bool beganMove)
    {
        beganMove = false;
        var tolerance = 12 / _zoom;
        if (_selectedGroupId is { } selectedGroupId && FindGroup(selectedGroupId) is { } selectedGroup &&
            SceneGeometry.Find(_scene, selectedGroup.PrimaryMemberId) is { } primary)
        {
            var origin = SceneGeometry.Origin(_scene, primary);
            var handle = GroupRotationHandle(primary);
            if ((world - origin).Length <= tolerance || (world - handle).Length <= tolerance)
            {
                _movingGroupId = selectedGroupId;
                _moveDragMode = (world - handle).Length <= tolerance
                    ? MoveDragMode.RotationHandle : MoveDragMode.Translate;
                _lastMoveWorld = world;
                _moveChanged = false;
                _moveSimulationDirty = false;
                _lastMoveSimulationTimestamp = 0;
                beganMove = true;
                InteractionStateChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
        }

        if (FindItemAt(world) is not { } hit || FindGroupContaining(hit.Id) is not { } group)
            return false;

        _selectedIds.Clear();
        _selectedIds.UnionWith(group.MemberIds);
        _selectedGroupId = group.Id;
        _activeElementId = hit.Id;
        ClearLegacySelection();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        return true;
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
                SceneItemKind.Lens => DistanceToSegment(world, _scene.LensElements[item.Index].Start,
                    _scene.LensElements[item.Index].End),
                _ => double.PositiveInfinity
            };
            Consider(distance, item);
        }
        return best;
    }

    private ElementGroup? FindGroup(Guid id) => _scene.ElementGroups.FirstOrDefault(group => group.Id == id);
    private ElementGroup? FindGroupContaining(Guid elementId) =>
        _scene.ElementGroups.FirstOrDefault(group => group.MemberIds.Contains(elementId));

    private Vector2D GroupRotationHandle(SceneItemRef primary) =>
        SceneGeometry.Origin(_scene, primary) +
        Vector2D.FromAngle(SceneGeometry.AngleDegrees(_scene, primary) * Math.PI / 180) * RotationHandleOffset;

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

        for (var index = 0; index < _scene.ConcaveSphericalMirrorElements.Length; index++)
        {
            var mirror = _scene.ConcaveSphericalMirrorElements[index];
            SelectIfCloser(DistanceToArc(world, mirror), SceneItemKind.ConcaveSphericalMirror,
                index, MoveDragMode.Translate, ref bestDistance);
        }

        for (var index = 0; index < _scene.ConvexSphericalMirrorElements.Length; index++)
        {
            var mirror = _scene.ConvexSphericalMirrorElements[index];
            SelectIfCloser(DistanceToArc(world, mirror.Vertex, mirror.CenterOfCurvature,
                    mirror.ArcAngleDegrees), SceneItemKind.ConvexSphericalMirror,
                index, MoveDragMode.Translate, ref bestDistance);
        }

        for (var index = 0; index < _scene.BeamSplitterElements.Length; index++)
        {
            var beamSplitter = _scene.BeamSplitterElements[index];
            SelectIfCloser(DistanceToSegment(world, beamSplitter.Start, beamSplitter.End),
                SceneItemKind.BeamSplitter, index, MoveDragMode.Translate, ref bestDistance);
        }

        for (var index = 0; index < _scene.ScreenElements.Length; index++)
        {
            var screen = _scene.ScreenElements[index];
            SelectIfCloser(DistanceToSegment(world, screen.Start, screen.End), SceneItemKind.Screen,
                index, MoveDragMode.Translate, ref bestDistance);
        }

        for (var index = 0; index < _scene.ApertureElements.Length; index++)
        {
            var aperture = _scene.ApertureElements[index];
            SelectIfCloser(DistanceToSegment(world, aperture.Start, aperture.End), SceneItemKind.Aperture,
                index, MoveDragMode.Translate, ref bestDistance);
        }

        for (var index = 0; index < _scene.ReflectionGratingElements.Length; index++)
        {
            var grating = _scene.ReflectionGratingElements[index];
            SelectIfCloser(DistanceToSegment(world, grating.Start, grating.End),
                SceneItemKind.ReflectionGrating, index, MoveDragMode.Translate, ref bestDistance);
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

    public void MoveSelectedItem(Vector2D world)
    {
        if (_movingGroupId is { } movingGroupId && FindGroup(movingGroupId) is { } movingGroup &&
            SceneGeometry.Find(_scene, movingGroup.PrimaryMemberId) is { } primary)
        {
            var memberIds = movingGroup.MemberIds.ToHashSet();
            if (_moveDragMode == MoveDragMode.Translate)
            {
                var groupDelta = world - _lastMoveWorld;
                if (groupDelta.LengthSquared <= 1e-12) return;
                _scene = SceneGeometry.Translate(_scene, memberIds, groupDelta);
            }
            else
            {
                var pivot = SceneGeometry.Origin(_scene, primary);
                var direction = world - pivot;
                if (direction.LengthSquared <= 1e-12) return;
                var targetAngle = DirectionDegrees(direction);
                var currentAngle = SceneGeometry.AngleDegrees(_scene, primary);
                var deltaDegrees = NormalizeSignedDegrees(targetAngle - currentAngle);
                if (Math.Abs(deltaDegrees) <= 1e-9) return;
                _scene = SceneGeometry.Rotate(_scene, memberIds, pivot, deltaDegrees * Math.PI / 180);
            }
            _lastMoveWorld = world;
            SceneUpdated?.Invoke(this, EventArgs.Empty);
            _moveChanged = true;
            _moveSimulationDirty = true;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            RecalculateDuringMoveIfDue();
            return;
        }

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
                if (_moveDragMode == MoveDragMode.RotationHandle && source.Kind == LightSourceKind.Point)
                {
                    var direction = (world - source.Position).Normalized();
                    if (direction.LengthSquared <= 1e-12)
                    {
                        return;
                    }
                    sources[_movingIndex] = source with
                    {
                        DirectionDegrees = DirectionDegrees(direction)
                    };
                }
                else if (_moveDragMode == MoveDragMode.Translate || source.End is null)
                {
                    sources[_movingIndex] = source with
                    {
                        Position = source.Position + delta,
                        End = source.End is { } translatedEnd ? translatedEnd + delta : null
                    };
                }
                else
                {
                    var start = source.Position;
                    var end = source.End.Value;
                    if (_moveDragMode == MoveDragMode.RotationHandle &&
                        !TryRotateSegmentFromHandle(source.Position, source.End.Value, world,
                            out start, out end))
                    {
                        return;
                    }
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
                var mirrorStart = mirror.Start;
                var mirrorEnd = mirror.End;
                if (_moveDragMode == MoveDragMode.Translate)
                {
                    mirrorStart += delta;
                    mirrorEnd += delta;
                }
                else if (_moveDragMode == MoveDragMode.RotationHandle &&
                         !TryRotateSegmentFromHandle(mirror.Start, mirror.End, world,
                             out mirrorStart, out mirrorEnd))
                {
                    return;
                }
                if (!HasUsableLength(mirrorStart, mirrorEnd))
                {
                    return;
                }
                mirrors[_movingIndex] = mirror with { Start = mirrorStart, End = mirrorEnd };
                UpdateScene(_scene with { Mirrors = mirrors });
                updated = true;
                break;
            case SceneItemKind.ConcaveSphericalMirror:
                var sphericalMirrors = (ConcaveSphericalMirror[])_scene.ConcaveSphericalMirrorElements.Clone();
                var sphericalMirror = sphericalMirrors[_movingIndex];
                if (_moveDragMode is MoveDragMode.DirectionHandle or MoveDragMode.RotationHandle)
                {
                    var direction = (world - sphericalMirror.Vertex).Normalized();
                    if (direction.LengthSquared <= 1e-12)
                    {
                        return;
                    }
                    sphericalMirrors[_movingIndex] = sphericalMirror with
                    {
                        CenterOfCurvature = sphericalMirror.Vertex + direction * sphericalMirror.Radius
                    };
                }
                else
                {
                    sphericalMirrors[_movingIndex] = sphericalMirror with
                    {
                        Vertex = sphericalMirror.Vertex + delta,
                        CenterOfCurvature = sphericalMirror.CenterOfCurvature + delta
                    };
                }
                UpdateScene(_scene with { ConcaveSphericalMirrors = sphericalMirrors });
                updated = true;
                break;
            case SceneItemKind.ConvexSphericalMirror:
                var convexSphericalMirrors = (ConvexSphericalMirror[])_scene.ConvexSphericalMirrorElements.Clone();
                var convexSphericalMirror = convexSphericalMirrors[_movingIndex];
                if (_moveDragMode is MoveDragMode.DirectionHandle or MoveDragMode.RotationHandle)
                {
                    var direction = (world - convexSphericalMirror.Vertex).Normalized();
                    if (direction.LengthSquared <= 1e-12)
                    {
                        return;
                    }
                    convexSphericalMirrors[_movingIndex] = convexSphericalMirror with
                    {
                        CenterOfCurvature = convexSphericalMirror.Vertex +
                                            direction * convexSphericalMirror.Radius
                    };
                }
                else
                {
                    convexSphericalMirrors[_movingIndex] = convexSphericalMirror with
                    {
                        Vertex = convexSphericalMirror.Vertex + delta,
                        CenterOfCurvature = convexSphericalMirror.CenterOfCurvature + delta
                    };
                }
                UpdateScene(_scene with { ConvexSphericalMirrors = convexSphericalMirrors });
                updated = true;
                break;
            case SceneItemKind.BeamSplitter:
                var beamSplitters = (BeamSplitterSegment[])_scene.BeamSplitterElements.Clone();
                var beamSplitter = beamSplitters[_movingIndex];
                var beamSplitterStart = beamSplitter.Start;
                var beamSplitterEnd = beamSplitter.End;
                if (_moveDragMode == MoveDragMode.Translate)
                {
                    beamSplitterStart += delta;
                    beamSplitterEnd += delta;
                }
                else if (_moveDragMode == MoveDragMode.RotationHandle &&
                         !TryRotateSegmentFromHandle(beamSplitter.Start, beamSplitter.End, world,
                             out beamSplitterStart, out beamSplitterEnd))
                {
                    return;
                }
                if (!HasUsableLength(beamSplitterStart, beamSplitterEnd))
                {
                    return;
                }
                beamSplitters[_movingIndex] = beamSplitter with
                {
                    Start = beamSplitterStart,
                    End = beamSplitterEnd
                };
                UpdateScene(_scene with { BeamSplitters = beamSplitters });
                updated = true;
                break;
            case SceneItemKind.Screen:
                var screens = (ScreenSegment[])_scene.ScreenElements.Clone();
                var screen = screens[_movingIndex];
                var screenStart = screen.Start;
                var screenEnd = screen.End;
                if (_moveDragMode == MoveDragMode.Translate)
                {
                    screenStart += delta;
                    screenEnd += delta;
                }
                else if (_moveDragMode == MoveDragMode.RotationHandle &&
                         !TryRotateSegmentFromHandle(screen.Start, screen.End, world,
                             out screenStart, out screenEnd))
                {
                    return;
                }
                if (!HasUsableLength(screenStart, screenEnd))
                {
                    return;
                }
                screens[_movingIndex] = screen with { Start = screenStart, End = screenEnd };
                UpdateScene(_scene with { Screens = screens });
                updated = true;
                break;
            case SceneItemKind.Aperture:
                var apertures = (ApertureSegment[])_scene.ApertureElements.Clone();
                var aperture = apertures[_movingIndex];
                var apertureStart = aperture.Start;
                var apertureEnd = aperture.End;
                if (_moveDragMode == MoveDragMode.Translate)
                {
                    apertureStart += delta;
                    apertureEnd += delta;
                }
                else if (_moveDragMode == MoveDragMode.RotationHandle &&
                         !TryRotateSegmentFromHandle(aperture.Start, aperture.End, world,
                             out apertureStart, out apertureEnd))
                {
                    return;
                }
                if (!HasUsableLength(apertureStart, apertureEnd))
                {
                    return;
                }
                var apertureLength = (apertureEnd - apertureStart).Length;
                apertures[_movingIndex] = aperture with
                {
                    Start = apertureStart,
                    End = apertureEnd,
                    OpeningSize = Math.Min(aperture.OpeningSize, apertureLength)
                };
                UpdateScene(_scene with { Apertures = apertures });
                updated = true;
                break;
            case SceneItemKind.ReflectionGrating:
                var gratings = (ReflectionGratingSegment[])_scene.ReflectionGratingElements.Clone();
                var grating = gratings[_movingIndex];
                var gratingStart = grating.Start;
                var gratingEnd = grating.End;
                if (_moveDragMode == MoveDragMode.Translate)
                {
                    gratingStart += delta;
                    gratingEnd += delta;
                }
                else if (_moveDragMode == MoveDragMode.RotationHandle &&
                         !TryRotateSegmentFromHandle(grating.Start, grating.End, world,
                             out gratingStart, out gratingEnd))
                {
                    return;
                }
                if (!HasUsableLength(gratingStart, gratingEnd))
                {
                    return;
                }
                gratings[_movingIndex] = grating with { Start = gratingStart, End = gratingEnd };
                UpdateScene(_scene with { ReflectionGratings = gratings });
                updated = true;
                break;
            case SceneItemKind.Lens:
                var lenses = (LensSegment[])_scene.LensElements.Clone();
                var lens = lenses[_movingIndex];
                var lensStart = lens.Start;
                var lensEnd = lens.End;
                if (_moveDragMode == MoveDragMode.Translate)
                {
                    lensStart += delta;
                    lensEnd += delta;
                }
                else if (_moveDragMode == MoveDragMode.RotationHandle &&
                         !TryRotateSegmentFromHandle(lens.Start, lens.End, world,
                             out lensStart, out lensEnd))
                {
                    return;
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

    private static Vector2D RotationHandle(Vector2D start, Vector2D end)
    {
        var midpoint = (start + end) / 2;
        return midpoint + (end - start).Normalized().Perpendicular() * RotationHandleOffset;
    }

    private static Vector2D SphericalMirrorRotationHandle(
        Vector2D vertex, Vector2D centerOfCurvature) =>
        vertex + (centerOfCurvature - vertex).Normalized() * RotationHandleOffset;

    private static Vector2D PointLightRotationHandle(LightSource source) =>
        source.Position + Vector2D.FromAngle(source.DirectionDegrees * Math.PI / 180) *
        RotationHandleOffset;

    private static bool TryRotateSegmentFromHandle(
        Vector2D start, Vector2D end, Vector2D handlePosition,
        out Vector2D rotatedStart, out Vector2D rotatedEnd)
    {
        var midpoint = (start + end) / 2;
        var handleDirection = (handlePosition - midpoint).Normalized();
        if (handleDirection.LengthSquared <= 1e-12)
        {
            rotatedStart = start;
            rotatedEnd = end;
            return false;
        }

        var tangent = -handleDirection.Perpendicular();
        var halfLength = (end - start).Length / 2;
        rotatedStart = midpoint - tangent * halfLength;
        rotatedEnd = midpoint + tangent * halfLength;
        return true;
    }

    private void UpdateScene(OpticalScene scene)
    {
        _scene = scene;
        SceneUpdated?.Invoke(this, EventArgs.Empty);
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
        SceneItemKind.Lens => _scene.LensElements[item.Index].IsTemporarilyHidden,
        _ => false
    };

    private void CommitSelectedEdit()
    {
        PreviewRequested?.Invoke(this, EventArgs.Empty);
        SceneCommitted?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
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

    private static double NormalizeSignedDegrees(double degrees)
    {
        var normalized = NormalizeDegrees(degrees);
        return normalized > 180 ? normalized - 360 : normalized;
    }

    private void RecalculateDuringMoveIfDue()
    {
        var now = Stopwatch.GetTimestamp();
        const int refreshMilliseconds = 33;
        var elapsedMilliseconds = _lastMoveSimulationTimestamp == 0
            ? double.PositiveInfinity
            : (now - _lastMoveSimulationTimestamp) * 1000d / Stopwatch.Frequency;
        if (elapsedMilliseconds < refreshMilliseconds)
        {
            return;
        }

        _lastMoveSimulationTimestamp = now;
        _moveSimulationDirty = false;
        PreviewRequested?.Invoke(this, EventArgs.Empty);
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

    private static double DistanceToArc(Vector2D point, ConcaveSphericalMirror mirror) =>
        DistanceToArc(point, mirror.Vertex, mirror.CenterOfCurvature, mirror.ArcAngleDegrees);

    private static double DistanceToArc(
        Vector2D point,
        Vector2D vertex,
        Vector2D centerOfCurvature,
        double arcAngleDegrees)
    {
        var radius = (centerOfCurvature - vertex).Length;
        if (radius <= 1e-12)
        {
            return (point - vertex).Length;
        }

        var centerAngle = Math.Atan2(
            vertex.Y - centerOfCurvature.Y,
            vertex.X - centerOfCurvature.X);
        var pointAngle = Math.Atan2(
            point.Y - centerOfCurvature.Y,
            point.X - centerOfCurvature.X);
        var difference = Math.Atan2(Math.Sin(pointAngle - centerAngle), Math.Cos(pointAngle - centerAngle));
        var halfAngle = Math.Clamp(Math.Abs(arcAngleDegrees), 1, 359.9) * Math.PI / 360;
        if (Math.Abs(difference) <= halfAngle)
        {
            return Math.Abs((point - centerOfCurvature).Length - radius);
        }

        var endpointA = centerOfCurvature + Vector2D.FromAngle(centerAngle - halfAngle) * radius;
        var endpointB = centerOfCurvature + Vector2D.FromAngle(centerAngle + halfAngle) * radius;
        return Math.Min((point - endpointA).Length, (point - endpointB).Length);
    }

    private static double DirectionDegrees(Vector2D direction) =>
        Math.Atan2(direction.Y, direction.X) * 180 / Math.PI;

}
