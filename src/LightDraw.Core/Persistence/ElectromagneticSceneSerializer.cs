using System.Text.Json;
using System.Text.Json.Serialization;
using LightDraw.Core.Electromagnetics;
using LightDraw.Core.Scene;

namespace LightDraw.Core.Persistence;

public static class ElectromagneticSceneSerializer
{
    public const int CurrentDataVersion = 1;
    private static readonly ElectromagneticJsonContext Context = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    });

    public static async Task<ElectrostaticScene> LoadElectrostaticAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var document = await SceneFileReader.ReadAsync(stream, cancellationToken);
        var scene = SceneFileReader.Scene(document, "electrostatic", CurrentDataVersion)
            .Deserialize(Context.ElectrostaticScene) ?? throw new InvalidDataException("Missing scene.");
        SceneValidation.Validate(scene);
        return ElectromagneticSceneNormalizer.Normalize(scene);
    }

    public static async Task<MagnetostaticScene> LoadMagnetostaticAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var document = await SceneFileReader.ReadAsync(stream, cancellationToken);
        var scene = SceneFileReader.Scene(document, "magnetostatic", CurrentDataVersion)
            .Deserialize(Context.MagnetostaticScene) ?? throw new InvalidDataException("Missing scene.");
        SceneValidation.Validate(scene);
        return ElectromagneticSceneNormalizer.Normalize(scene);
    }

    public static Task SaveAsync(ElectrostaticScene scene, Stream stream, CancellationToken cancellationToken = default)
    {
        SceneValidation.Validate(scene);
        return JsonSerializer.SerializeAsync(stream,
            new ElectrostaticDocument(CurrentDataVersion, "electrostatic", ElectromagneticSceneNormalizer.Normalize(scene)),
            Context.ElectrostaticDocument, cancellationToken);
    }

    public static Task SaveAsync(MagnetostaticScene scene, Stream stream, CancellationToken cancellationToken = default)
    {
        SceneValidation.Validate(scene);
        return JsonSerializer.SerializeAsync(stream,
            new MagnetostaticDocument(CurrentDataVersion, "magnetostatic", ElectromagneticSceneNormalizer.Normalize(scene)),
            Context.MagnetostaticDocument, cancellationToken);
    }
}

internal sealed record ElectrostaticDocument(int DataVersion, string SceneType, ElectrostaticScene Scene);
internal sealed record MagnetostaticDocument(int DataVersion, string SceneType, MagnetostaticScene Scene);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(ElectrostaticDocument))]
[JsonSerializable(typeof(MagnetostaticDocument))]
internal partial class ElectromagneticJsonContext : JsonSerializerContext;
