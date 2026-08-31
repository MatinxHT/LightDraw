using System.Text.Json;
using System.Text.Json.Serialization;
using LightDraw.Core.Scene;

namespace LightDraw.Core.Persistence;

public static class SceneSerializer
{
    public const int CurrentDataVersion = 12;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task SaveAsync(OpticalScene scene, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(stream);
        scene = NormalizeScene(scene);
        await JsonSerializer.SerializeAsync(
            stream,
            new SceneDocument(CurrentDataVersion, scene),
            Options,
            cancellationToken);
    }

    public static async Task<OpticalScene> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var document = await JsonSerializer.DeserializeAsync<SceneDocument>(stream, Options, cancellationToken)
            ?? throw new InvalidDataException("场景文件为空或格式无效。");

        if (document.DataVersion is < 1 or > CurrentDataVersion)
        {
            throw new InvalidDataException($"暂不支持场景数据版本 {document.DataVersion}。");
        }

        var scene = document.Scene ?? throw new InvalidDataException("场景文件缺少 scene 节点。");
        return NormalizeScene(scene);
    }

    private static OpticalScene NormalizeScene(OpticalScene scene) => scene with
    {
        LightSources = (scene.LightSources ?? [])
            .Select(source => source with
            {
                Id = EnsureId(source.Id),
                WavelengthNanometers = source.Spectrum == LightSpectrumKind.Composite
                    ? LightSource.CompositeGreenWavelengthNanometers
                    : NormalizeMonochromaticWavelength(source.WavelengthNanometers)
            })
            .ToArray(),
        Mirrors = (scene.Mirrors ?? []).Select(item => item with { Id = EnsureId(item.Id) }).ToArray(),
        ConcaveSphericalMirrors = scene.ConcaveSphericalMirrorElements
            .Select(item => item with { Id = EnsureId(item.Id) }).ToArray(),
        ConvexSphericalMirrors = scene.ConvexSphericalMirrorElements
            .Select(item => item with { Id = EnsureId(item.Id) }).ToArray(),
        BeamSplitters = scene.BeamSplitterElements
            .Select(item => item with { Id = EnsureId(item.Id) }).ToArray(),
        Screens = scene.ScreenElements.Select(item => item with { Id = EnsureId(item.Id) }).ToArray(),
        Apertures = scene.ApertureElements.Select(item => item with { Id = EnsureId(item.Id) }).ToArray(),
        ReflectionGratings = scene.ReflectionGratingElements
            .Select(item => item with { Id = EnsureId(item.Id) }).ToArray(),
        Lenses = scene.LensElements
            .Select(lens => lens with
            {
                Id = EnsureId(lens.Id),
                DispersionMode = Enum.IsDefined(lens.DispersionMode)
                    ? lens.DispersionMode
                    : LensDispersionMode.None,
                DispersionLevel = Math.Clamp(lens.DispersionLevel, 0, 10)
            })
            .ToArray(),
        Groups = NormalizeGroups(scene)
    };

    private static ElementGroup[] NormalizeGroups(OpticalScene scene)
    {
        var validIds = EnumerateIds(scene).Where(id => id != Guid.Empty).ToHashSet();
        var claimed = new HashSet<Guid>();
        var groups = new List<ElementGroup>();
        foreach (var group in scene.ElementGroups)
        {
            var members = (group.MemberIds ?? [])
                .Where(id => validIds.Contains(id) && claimed.Add(id))
                .Distinct()
                .ToArray();
            if (members.Length < 2) continue;
            var primary = members.Contains(group.PrimaryMemberId) ? group.PrimaryMemberId : members[0];
            groups.Add(group with
            {
                Id = EnsureId(group.Id),
                MemberIds = members,
                PrimaryMemberId = primary
            });
        }
        return groups.ToArray();
    }

    private static IEnumerable<Guid> EnumerateIds(OpticalScene scene) =>
        scene.LightSources.Select(item => item.Id)
            .Concat(scene.Mirrors.Select(item => item.Id))
            .Concat(scene.ConcaveSphericalMirrorElements.Select(item => item.Id))
            .Concat(scene.ConvexSphericalMirrorElements.Select(item => item.Id))
            .Concat(scene.BeamSplitterElements.Select(item => item.Id))
            .Concat(scene.ScreenElements.Select(item => item.Id))
            .Concat(scene.ApertureElements.Select(item => item.Id))
            .Concat(scene.ReflectionGratingElements.Select(item => item.Id))
            .Concat(scene.LensElements.Select(item => item.Id));

    private static Guid EnsureId(Guid id) => id == Guid.Empty ? Guid.NewGuid() : id;

    private static double NormalizeMonochromaticWavelength(double wavelengthNanometers) =>
        double.IsFinite(wavelengthNanometers) && wavelengthNanometers > 0
            ? wavelengthNanometers
            : LightSource.MonochromaticWavelengthNanometers;

    private sealed record SceneDocument(int DataVersion, OpticalScene? Scene);
}
