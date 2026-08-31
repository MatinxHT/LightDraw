using System.Text.Json;
using System.Text.Json.Serialization;
using LightDraw.Core.Scene;

namespace LightDraw.Core.Persistence;

public static class SceneSerializer
{
    public const int CurrentDataVersion = 11;

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
                WavelengthNanometers = source.Spectrum == LightSpectrumKind.Composite
                    ? LightSource.CompositeGreenWavelengthNanometers
                    : NormalizeMonochromaticWavelength(source.WavelengthNanometers)
            })
            .ToArray(),
        Lenses = scene.LensElements
            .Select(lens => lens with
            {
                DispersionMode = Enum.IsDefined(lens.DispersionMode)
                    ? lens.DispersionMode
                    : LensDispersionMode.None,
                DispersionLevel = Math.Clamp(lens.DispersionLevel, 0, 10)
            })
            .ToArray()
    };

    private static double NormalizeMonochromaticWavelength(double wavelengthNanometers) =>
        double.IsFinite(wavelengthNanometers) && wavelengthNanometers > 0
            ? wavelengthNanometers
            : LightSource.MonochromaticWavelengthNanometers;

    private sealed record SceneDocument(int DataVersion, OpticalScene? Scene);
}
