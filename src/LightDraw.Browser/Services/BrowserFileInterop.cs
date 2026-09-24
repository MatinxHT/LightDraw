using System.Runtime.InteropServices.JavaScript;

namespace LightDraw.Browser.Services;

public static partial class BrowserFileInterop
{
    public static async Task InitializeAsync() =>
        await JSHost.ImportAsync("LightDrawFileInterop", "../js/fileInterop.js");

    public static async Task<(string FileName, byte[] Bytes)?> PickSceneAsync()
    {
        var encoded = await PickSceneFile();
        if (encoded is null) return null;
        var separator = encoded.IndexOf(':');
        if (separator < 0) throw new InvalidDataException("Invalid browser file response.");
        return (encoded[..separator], Convert.FromBase64String(encoded[(separator + 1)..]));
    }

    public static void Download(string fileName, string mimeType, byte[] bytes) =>
        DownloadFile(fileName, mimeType, Convert.ToBase64String(bytes));

    [JSImport("pickSceneFile", "LightDrawFileInterop")]
    private static partial Task<string?> PickSceneFile();

    [JSImport("downloadFile", "LightDrawFileInterop")]
    private static partial void DownloadFile(string fileName, string mimeType, string base64);
}
