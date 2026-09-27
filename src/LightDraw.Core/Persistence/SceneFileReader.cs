using System.Text.Json;

namespace LightDraw.Core.Persistence;

internal static class SceneFileReader
{
    public const int MaximumBytes = 16 * 1024 * 1024;

    public static async Task<JsonDocument> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + count > MaximumBytes)
                throw new InvalidDataException("场景文件不能超过 16 MiB。 / Scene files must not exceed 16 MiB.");
            buffer.Write(chunk, 0, count);
        }
        buffer.Position = 0;
        return await JsonDocument.ParseAsync(buffer, cancellationToken: cancellationToken);
    }

    public static JsonElement Scene(JsonDocument document, string kind, int maximumVersion, bool allowLegacy = false)
    {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(root, "dataVersion", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) ||
            number < 1 || number > maximumVersion)
            throw new InvalidDataException("不支持的场景版本。 / Unsupported scene version.");
        if (TryGetProperty(root, "sceneType", out var type))
        {
            if (type.ValueKind != JsonValueKind.String || type.GetString() != kind)
                throw new InvalidDataException("请在对应的仿真窗口打开此文件。 / Open this file in the matching simulation window.");
        }
        else if (!allowLegacy)
            throw new InvalidDataException("场景文件缺少类型。 / Missing scene type.");
        if (!TryGetProperty(root, "scene", out var scene) || scene.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("场景文件缺少 scene 节点。 / Missing scene object.");
        return scene;
    }
    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            value = property.Value;
            return true;
        }
        value = default;
        return false;
    }

}
