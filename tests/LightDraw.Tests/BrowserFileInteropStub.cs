namespace LightDraw.Browser.Services;

// The browser UI tests replace only the JavaScript boundary; the views and storage are production code.
public static class BrowserFileInterop
{
    public static bool HasUnsavedChanges { get; private set; }
    public static (string FileName, byte[] Bytes)? File { get; set; }
    public static Task<(string FileName, byte[] Bytes)?> PickSceneAsync() => Task.FromResult(File);
    public static void Download(string fileName, string mimeType, byte[] bytes) => File = (fileName, bytes);
    public static void SetUnsavedChanges(bool hasChanges) => HasUnsavedChanges = hasChanges;
}
