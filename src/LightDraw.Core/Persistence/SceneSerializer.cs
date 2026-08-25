using System.Text.Json;
using System.Text.Json.Serialization;
using LightDraw.Core.Scene;

namespace LightDraw.Core.Persistence;

public static class SceneSerializer
{
    public const int CurrentDataVersion = 6;

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

        if (document.DataVersion is not (1 or 2 or 3 or 4 or 5 or CurrentDataVersion))
        {
            throw new InvalidDataException($"暂不支持场景数据版本 {document.DataVersion}。");
        }

        return document.Scene ?? throw new InvalidDataException("场景文件缺少 scene 节点。");
    }

    private sealed record SceneDocument(int DataVersion, OpticalScene? Scene);
}
