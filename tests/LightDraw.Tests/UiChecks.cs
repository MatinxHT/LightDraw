using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LightDraw.Core.Electromagnetics;
using LightDraw.Core.Scene;
using LightDraw.Desktop.Controls;
using LightDraw.Desktop.ViewModels;
using LightDraw.Desktop.Views;
using LightDraw.Rendering.Skia.Electrostatics;
using LightDraw.Rendering.Skia.Magnetostatics;
using LightDraw.Rendering.Skia.Optics;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<LightDraw.Desktop.App>()
        .UseSkia().WithInterFont().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

internal static class UiChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static Point At(Control control, Window window, double x, double y) => control.TranslatePoint(new Point(x, y), window)!.Value;
    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
    }
    private static void Shortcut(Window window, Key key, bool shift = false)
    {
        var modifiers = (RawInputModifiers)(Application.Current!.PlatformSettings!.HotkeyConfiguration.CommandModifiers);
        if (shift) modifiers |= RawInputModifiers.Shift;
        window.KeyPress(key, modifiers, PhysicalKey.None, null); window.KeyRelease(key, modifiers, PhysicalKey.None, null);
    }
    private static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(10);
        Check(condition(), "UI action did not complete");
    }

    public static async Task RunAsync()
    {
        var windows = new List<Window>();
        var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder), AvaloniaTestIsolationLevel.PerAssembly);
        try
        {
            await session.Dispatch(async () =>
            {
                var storage = new FakeStorage<OpticalScene> { SaveName = "test.lightdraw.json" };
                var vm = new MainWindowViewModel(storage);
                var window = new MainWindow(); windows.Add(window);
                window.AttachViewModel(vm); window.Show(); window.UpdateLayout();
                var canvas = window.FindControl<OpticalCanvas>("Canvas")!;
                Check(canvas.Bounds.Width > 200 && canvas.Bounds.Height > 150, "Canvas must remain usable with document toolbar");
                vm.ActiveTool = CanvasTool.PointLight;
                var location = At(canvas, window, 200, 150);
                Click(window, location);
                Check(vm.Document.Scene.LightSources.Length == 1 && vm.Document.HasUnsavedChanges, "Canvas edit must reach document");
                Shortcut(window, Key.Z);
                Check(canvas.Scene.LightSources.Length == 0 && !vm.Document.HasUnsavedChanges, "Undo must update canvas and saved marker");
                Shortcut(window, Key.Z, shift: true);
                Check(canvas.Scene.LightSources.Length == 1 && vm.Document.HasUnsavedChanges, "Redo must update canvas");
                await vm.Document.SaveSceneCommand.ExecuteAsync(null);
                vm.ActiveTool = CanvasTool.Move; Click(window, location);
                var originalName = canvas.Scene.LightSources[0].Name;
                var name = window.GetVisualDescendants().OfType<TextBox>().Single(box => box.Text == originalName);
                name.Focus(); name.Text = "Classroom source";
                await Until(() => vm.Document.HasUnsavedChanges);
                Check(canvas.Scene.LightSources[0].Name == originalName, "Text should commit as one edit on focus loss");
                await vm.Document.SaveSceneCommand.ExecuteAsync(null);
                Check(canvas.Scene.LightSources[0].Name == "Classroom source" && !vm.Document.HasUnsavedChanges,
                    "Saving must flush focused property text before taking its snapshot");
                Shortcut(window, Key.Z);
                Check(canvas.Scene.LightSources[0].Name == originalName && vm.Document.HasUnsavedChanges, "Rename is one undo step");
                Shortcut(window, Key.Z, shift: true);
                Check(!vm.Document.HasUnsavedChanges, "Redo to saved content must clear dirty state");
                vm.Document.Commit(vm.Document.Scene with { Name = "Unsaved" });
                var resetting = vm.Document.ResetSceneCommand.ExecuteAsync(null);
                var prompt = window.FindControl<UnsavedChangesPrompt>("UnsavedPrompt")!;
                await Until(() => prompt.IsVisible);
                Check(!canvas.IsEffectivelyEnabled, "Scene controls must be disabled while confirmation is open");
                prompt.FindControl<Button>("CancelButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await resetting;
                Check(canvas.Scene.LightSources.Length == 1 && vm.Document.HasUnsavedChanges, "Cancel must keep scene");
                // Render for layout verification without creating any native windows.
                var imagePath = Environment.GetEnvironmentVariable("LIGHTDRAW_TEST_SCREENSHOT");
                if (!string.IsNullOrEmpty(imagePath))
                {
                    using var bitmap = window.CaptureRenderedFrame(); bitmap?.Save(imagePath, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                }
                vm.Document.ConfirmUnsavedChangesAsync = () => Task.FromResult(UnsavedChangesChoice.Discard);
                vm.RayDensity = 400;
                await vm.Document.ResetSceneCommand.ExecuteAsync(null);
                Check(vm.RayDensity == 160 && canvas.Scene.LightSources.Length == 0, "Reset must preserve its default view settings");
                vm.Document.UndoCommand.Execute(null);
                Check(canvas.Scene.LightSources.Length == 1, "Reset must be undoable through the actual window");
                window.Close(); await Until(() => !window.IsVisible);
                return true;

            }, CancellationToken.None);

            await session.Dispatch(async () =>
            {
                var mainVm = new MainWindowViewModel(new FakeStorage<OpticalScene>());
                var main = new MainWindow(); windows.Add(main); main.AttachViewModel(mainVm); main.Show();
                var electric = new ElectrostaticWindow(); windows.Add(electric); electric.Show(main); electric.UpdateLayout();
                var magnetic = new MagnetostaticWindow(); windows.Add(magnetic); magnetic.Show(main); magnetic.UpdateLayout();
                var evm = (ElectrostaticWindowViewModel)electric.DataContext!;
                var mvm = (MagnetostaticWindowViewModel)magnetic.DataContext!;
                var ec = electric.FindControl<ElectrostaticCanvas>("Canvas")!;
                var mc = magnetic.FindControl<MagnetostaticCanvas>("Canvas")!;
                evm.ActiveTool = ElectrostaticTool.PointCharge;
                Click(electric, At(ec, electric, 200, 150));
                Check(evm.Document.Scene.Charges.Length == 1 && evm.Document.HasUnsavedChanges, "Electric edit reaches document");
                Shortcut(electric, Key.Z); Check(ec.Scene.Charges.Length == 0, "Electric undo reaches canvas");
                Shortcut(electric, Key.Z, true); Check(ec.Scene.Charges.Length == 1, "Electric redo reaches canvas");
                mvm.ActiveTool = MagnetostaticTool.VerticalInfiniteCurrentConductor;
                Click(magnetic, At(mc, magnetic, 200, 150));
                Check(mvm.Document.Scene.VerticalConductorElements.Length == 1 && mvm.Document.HasUnsavedChanges, "Magnetic edit reaches document");
                Shortcut(magnetic, Key.Z); Check(mc.Scene.VerticalConductorElements.Length == 0, "Magnetic undo reaches canvas");
                Shortcut(magnetic, Key.Z, true); Check(mc.Scene.VerticalConductorElements.Length == 1, "Magnetic redo reaches canvas");
                var prompts = new List<string>();
                evm.Document.ConfirmUnsavedChangesAsync = () => { prompts.Add("electric"); return Task.FromResult(UnsavedChangesChoice.Discard); };
                mvm.Document.ConfirmUnsavedChangesAsync = () => { prompts.Add("magnetic"); return Task.FromResult(UnsavedChangesChoice.Cancel); };
                main.Close(); await Until(() => prompts.Count == 2);
                Check(main.IsVisible && electric.IsVisible && magnetic.IsVisible, "Cancelling one close review must keep all windows open");
                Check(evm.Document.IsEditingEnabled && mvm.Document.IsEditingEnabled, "Cancelling close must unlock all windows");
                mvm.Document.ConfirmUnsavedChangesAsync = () => Task.FromResult(UnsavedChangesChoice.Discard);
                main.Close(); await Until(() => !main.IsVisible);
                Check(!electric.IsVisible && !magnetic.IsVisible, "Confirmed root close must close owned windows");
                return true;

            }, CancellationToken.None);
        }
        finally
        {
            try
            {
                await session.Dispatch(async () =>
                {
                    foreach (var item in windows)
                    {
                        SceneDocumentViewModel? document = item.DataContext switch
                        {
                            MainWindowViewModel model => model.Document,
                            ElectrostaticWindowViewModel model => model.Document,
                            MagnetostaticWindowViewModel model => model.Document,
                            _ => null
                        };
                        if (document is not null)
                            document.ConfirmUnsavedChangesAsync = () => Task.FromResult(UnsavedChangesChoice.Discard);
                        item.FindControl<UnsavedChangesPrompt>("UnsavedPrompt")?.Cancel();
                    }
                    await Task.Delay(20);
                    foreach (var item in windows.Where(item => item.IsVisible).ToArray()) item.Close();
                    await Until(() => windows.All(item => !item.IsVisible));
                    return true;
                }, CancellationToken.None);
            }
            finally
            {
                // Dispatch may complete inline on its worker; disposing there would wait on itself.
                await Task.Run(session.Dispose);
            }
        }
    }
}
