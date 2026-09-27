namespace LightDraw.Core.Scene;

/// <summary>Applies the same legacy defaults when opening, saving and editing a scene.</summary>
public static class OpticalSceneNormalizer
{
    public static OpticalScene Normalize(OpticalScene scene)
    {
        var normalized = scene with
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
                        ? LightSource.CompositeGreenWavelengthNanometers
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
            ConcaveGratings = scene.ConcaveGratingElements
                .Select((item, index) => item with { Id = EnsureId(item.Id),
                    Name = NormalizeName(item.Name, "Concave Grating", index + 1) }).ToArray(),
            BeamSplitters = scene.BeamSplitterElements.Select((item, index) => item with { Id = EnsureId(item.Id),
                Name = NormalizeName(item.Name, "Beam Splitter", index + 1) }).ToArray(),
        };
        return normalized with { Groups = NormalizeGroups(normalized) };
    }

    private static Guid EnsureId(Guid id) => id == Guid.Empty ? Guid.NewGuid() : id;

    private static string NormalizeName(string? name, string baseName, int number) =>
        string.IsNullOrWhiteSpace(name) ? $"{baseName} {number}" : name.Trim();

    private static ElementGroup[] NormalizeGroups(OpticalScene scene)
    {
        var valid = scene.LightSources.Select(item => item.Id)
            .Concat(scene.Mirrors.Select(item => item.Id))
            .Concat(scene.ConcaveSphericalMirrorElements.Select(item => item.Id))
            .Concat(scene.ConvexSphericalMirrorElements.Select(item => item.Id))
            .Concat(scene.BeamSplitterElements.Select(item => item.Id))
            .Concat(scene.ScreenElements.Select(item => item.Id))
            .Concat(scene.ApertureElements.Select(item => item.Id))
            .Concat(scene.ReflectionGratingElements.Select(item => item.Id))
            .Concat(scene.ConcaveGratingElements.Select(item => item.Id))
            .Concat(scene.LensElements.Select(item => item.Id))
            .Where(id => id != Guid.Empty).ToHashSet();
        var claimed = new HashSet<Guid>();
        return scene.ElementGroups.Select((group, index) =>
        {
            var members = (group.MemberIds ?? []).Where(id => valid.Contains(id) && !claimed.Contains(id))
                .Distinct().ToArray();
            if (members.Length >= 2) claimed.UnionWith(members);
            return group with
            {
                Id = EnsureId(group.Id), MemberIds = members,
                Name = NormalizeName(group.Name, "Group", index + 1),
                PrimaryMemberId = members.Contains(group.PrimaryMemberId)
                    ? group.PrimaryMemberId : members.FirstOrDefault()
            };
        }).Where(group => group.MemberIds.Length >= 2).ToArray();
    }

    private static double NormalizeMonochromaticWavelength(double wavelengthNanometers) =>
        double.IsFinite(wavelengthNanometers) && wavelengthNanometers > 0
            ? wavelengthNanometers
            : LightSource.MonochromaticWavelengthNanometers;

}
