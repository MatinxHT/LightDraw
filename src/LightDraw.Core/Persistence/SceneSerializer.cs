using System.Text.Json;
using System.Text.Json.Serialization;
using LightDraw.Core.Scene;

namespace LightDraw.Core.Persistence;

public static class SceneSerializer
{
    public const int CurrentDataVersion = 14;

    private static readonly SceneJsonContext Context = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter<LightSourceKind>(JsonNamingPolicy.CamelCase),
            new JsonStringEnumConverter<LightSpectrumKind>(JsonNamingPolicy.CamelCase),
            new JsonStringEnumConverter<LensKind>(JsonNamingPolicy.CamelCase),
            new JsonStringEnumConverter<LensDispersionMode>(JsonNamingPolicy.CamelCase)
        }
    });

    public static async Task SaveAsync(OpticalScene scene, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(stream);
        SceneValidation.Validate(scene);
        scene = OpticalSceneNormalizer.Normalize(scene);
        await JsonSerializer.SerializeAsync(
            stream,
            new SceneDocument(CurrentDataVersion, scene),
            Context.SceneDocument,
            cancellationToken);
    }

    public static async Task<OpticalScene> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var document = await SceneFileReader.ReadAsync(stream, cancellationToken);
        var element = SceneFileReader.Scene(document, "optical", CurrentDataVersion, allowLegacy: true);
        var scene = element.Deserialize(Context.OpticalScene)
            ?? throw new InvalidDataException("场景文件为空或格式无效。");
        SceneValidation.Validate(scene);
        return OpticalSceneNormalizer.Normalize(scene);
    }
}

internal sealed record SceneDocument(int DataVersion, OpticalScene? Scene);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true)]
[JsonSerializable(typeof(SceneDocument))]
[JsonSerializable(typeof(OpticalScene))]
internal partial class SceneJsonContext : JsonSerializerContext;
