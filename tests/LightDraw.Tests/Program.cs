using System.Text;
using LightDraw.Core.Editing;
using LightDraw.Core.Electromagnetics;
using LightDraw.Core.Geometry;
using LightDraw.Core.Persistence;
using LightDraw.Core.Scene;
using LightDraw.Core.Simulation;
using LightDraw.Desktop.Services;
using LightDraw.Desktop.ViewModels;
using LightDraw.Rendering.Skia.Optics;

var tests = new List<(string Name, Func<Task> Run)>();
void Test(string name, Action action) => tests.Add((name, () => { action(); return Task.CompletedTask; }));
void AsyncTest(string name, Func<Task> action) => tests.Add((name, action));
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Near(double actual, double expected, double tolerance = 1e-8) => Check(Math.Abs(actual - expected) <= tolerance, $"Expected {expected}, got {actual}");
async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); } catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}");
}
OpticalScene Optical(double x = 0) => OpticalSceneCodec.Instance.Normalize(new("Test", [new(new(x, 0), 0)], []));
MemoryStream Json(string text) => new(Encoding.UTF8.GetBytes(text));

Test("History: saved state, undo/redo, branches, no-op edits", () =>
{
    var codec = OpticalSceneCodec.Instance;
    var a = Optical(); var b = a with { Name = "B" }; var c = a with { Name = "C" };
    var history = new SceneHistory<OpticalScene>(a, codec.Snapshot, codec.ContentEquals);
    Check(!history.CanUndo && !history.IsModified(a));
    history.Commit(b); history.MarkSaved(b); history.Commit(c);
    Check(history.IsModified(c)); Check(!history.IsModified(history.Undo()));
    Check(history.IsModified(history.Undo())); Check(!history.CanUndo);
    Check(!history.IsModified(history.Redo()));
    history.Commit(codec.Snapshot(b)); Check(history.CanRedo, "A no-op must preserve redo");
    history.Commit(a with { Name = "New branch" }); Check(!history.CanRedo);
});

Test("History: bounds and nested array snapshots", () =>
{
    var codec = OpticalSceneCodec.Instance;
    var a = Optical() with { Groups = [new(Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()], Guid.Empty)] };
    var originalMember = a.Groups[0].MemberIds[0];
    var history = new SceneHistory<OpticalScene>(a, codec.Snapshot, codec.ContentEquals, capacity: 2);
    a.Groups[0].MemberIds[0] = Guid.NewGuid();
    Check(history.Undo().Groups![0].MemberIds[0] == originalMember);
    history.Commit(a with { Name = "1" }); history.Commit(a with { Name = "2" }); history.Commit(a with { Name = "3" });
    Check(history.Undo().Name == "2"); Check(history.Undo().Name == "1"); Check(!history.CanUndo);
});

async Task DocumentWorkflow<T>(ISceneCodec<T> codec, T edited) where T : class
{
    var storage = new FakeStorage<T>();
    var doc = new SceneDocumentViewModel<T>(storage, codec);
    var baseline = codec.Snapshot(doc.Scene);
    Check(!doc.HasUnsavedChanges);
    doc.Scene = edited; // Preview updates do not create history entries.
    Check(!doc.UndoCommand.CanExecute(null));
    doc.Commit(edited); Check(doc.UndoCommand.CanExecute(null) && doc.HasUnsavedChanges);
    storage.SaveName = "saved.lightdraw.json";
    await doc.SaveSceneCommand.ExecuteAsync(null); Check(!doc.HasUnsavedChanges);
    doc.UndoCommand.Execute(null); Check(codec.ContentEquals(doc.Scene, baseline) && doc.HasUnsavedChanges);
    doc.RedoCommand.Execute(null); Check(!doc.HasUnsavedChanges);
    doc.ConfirmUnsavedChangesAsync = () => Task.FromResult(UnsavedChangesChoice.Discard);
    await doc.ResetSceneCommand.ExecuteAsync(null); Check(doc.HasUnsavedChanges);
    doc.UndoCommand.Execute(null); Check(codec.ContentEquals(doc.Scene, edited) && !doc.HasUnsavedChanges);
    doc.Commit(baseline);
    doc.ConfirmUnsavedChangesAsync = () => Task.FromResult(UnsavedChangesChoice.Cancel);
    Check(!await doc.RequestCloseAsync());
    await doc.ResetSceneCommand.ExecuteAsync(null); Check(codec.ContentEquals(doc.Scene, baseline));
    storage.Opened = new(edited, "open.lightdraw.json");
    await doc.OpenSceneCommand.ExecuteAsync(null); Check(codec.ContentEquals(doc.Scene, baseline));
    doc.ConfirmUnsavedChangesAsync = () => Task.FromResult(UnsavedChangesChoice.Save);
    storage.SaveName = null;
    Check(!await doc.RequestCloseAsync(), "Cancelling Save must keep the window open");
    await doc.OpenSceneCommand.ExecuteAsync(null); Check(codec.ContentEquals(doc.Scene, baseline));
    storage.SaveError = new IOException("Disk full");
    string? status = null; doc.StatusChanged += value => status = value;
    Check(!await doc.RequestCloseAsync()); Check(status?.Contains("Disk full") == true);
    Check(doc.HasUnsavedChanges && doc.IsEditingEnabled);
    storage.SaveError = null; storage.SaveName = "saved.lightdraw.json";
    await doc.OpenSceneCommand.ExecuteAsync(null);
    Check(codec.ContentEquals(doc.Scene, edited) && !doc.HasUnsavedChanges);
    Check(!doc.UndoCommand.CanExecute(null) && !doc.RedoCommand.CanExecute(null));
    Check(await doc.RequestCloseAsync());
}

AsyncTest("Optical document: full save/open/reset/close workflow", () => DocumentWorkflow(OpticalSceneCodec.Instance, Optical()));
AsyncTest("Electrostatic document: full save/open/reset/close workflow", () => DocumentWorkflow(ElectrostaticSceneCodec.Instance,
    ElectrostaticSceneCodec.Instance.Normalize(new("Electric", [new(new(10, 20), -2)], [new(new(0, 0), new(100, 0), 200)]))));
AsyncTest("Magnetostatic document: full save/open/reset/close workflow", () => DocumentWorkflow(MagnetostaticSceneCodec.Instance,
    MagnetostaticSceneCodec.Instance.Normalize(new("Magnetic", [new(new(0, 0), new(100, 0), -3)], [new(new(30, 30), 4)]))));

AsyncTest("Save snapshot: edits made during a save remain dirty; commands serialize", async () =>
{
    var pending = new TaskCompletionSource<string?>();
    var storage = new FakeStorage<OpticalScene> { SaveHandler = _ => pending.Task };
    var doc = new SceneDocumentViewModel<OpticalScene>(storage, OpticalSceneCodec.Instance);
    var a = Optical(); doc.Commit(a);
    var saving = doc.SaveSceneCommand.ExecuteAsync(null);
    Check(doc.IsBusy && !doc.IsEditingEnabled && !doc.OpenSceneCommand.CanExecute(null));
    Check(!await doc.RequestCloseAsync());
    doc.Commit(a with { Name = "Edited during save" });
    pending.SetResult("saved.json"); await saving;
    Check(doc.HasUnsavedChanges && doc.IsEditingEnabled);
    doc.UndoCommand.Execute(null); Check(!doc.HasUnsavedChanges);
});

AsyncTest("Failed/cancelled open preserves scene and history", async () =>
{
    var storage = new FakeStorage<OpticalScene>();
    var doc = new SceneDocumentViewModel<OpticalScene>(storage, OpticalSceneCodec.Instance);
    var scene = Optical(); doc.Commit(scene);
    var prompts = 0;
    doc.ConfirmUnsavedChangesAsync = () => { prompts++; return Task.FromResult(UnsavedChangesChoice.Discard); };
    await doc.OpenSceneCommand.ExecuteAsync(null);
    storage.OpenError = new InvalidDataException("Invalid file");
    await doc.OpenSceneCommand.ExecuteAsync(null);
    Check(prompts == 2 && doc.HasUnsavedChanges && doc.UndoCommand.CanExecute(null));
    Check(OpticalSceneCodec.Instance.ContentEquals(scene, doc.Scene));
});

Test("Editor: one drag is one committed edit; undo restores all coordinates", () =>
{
    var editor = new SceneEditor();
    var doc = new SceneDocumentViewModel<OpticalScene>(new FakeStorage<OpticalScene>(), OpticalSceneCodec.Instance);
    editor.SetScene(doc.Scene);
    editor.SceneUpdated += (_, _) => doc.Scene = editor.Scene;
    editor.SceneCommitted += (_, _) => doc.Commit(editor.Scene);
    doc.SceneReplaced += (_, _) => editor.SetScene(doc.Scene);
    editor.AddElement(CanvasTool.Mirror, new(0, 0), new(100, 0));
    editor.SelectInRectangle(new(-1, -1), new(101, 1));
    var before = OpticalSceneCodec.Instance.Snapshot(editor.Scene);
    Check(editor.TryBeginMove(new(50, 0)));
    for (var i = 1; i <= 20; i++) editor.MoveSelectedItem(new(50 + i, i));
    Check(editor.EndMove());
    doc.UndoCommand.Execute(null);
    Check(OpticalSceneCodec.Instance.ContentEquals(before, editor.Scene));
    doc.UndoCommand.Execute(null); Check(editor.Scene.Mirrors.Length == 0);
    doc.RedoCommand.Execute(null); doc.RedoCommand.Execute(null);
    Near(editor.Scene.Mirrors[0].Start.X, 20); Near(editor.Scene.Mirrors[0].Start.Y, 20);
});

Test("Editor: placement, grouping, rename, hiding, transforms, deletion and restoration", () =>
{
    var editor = new SceneEditor();
    var doc = new SceneDocumentViewModel<OpticalScene>(new FakeStorage<OpticalScene>(), OpticalSceneCodec.Instance);
    editor.SetScene(doc.Scene);
    editor.SceneUpdated += (_, _) => doc.Scene = editor.Scene;
    editor.SceneCommitted += (_, _) => doc.Commit(editor.Scene);
    doc.SceneReplaced += (_, _) => editor.SetScene(doc.Scene);
    editor.AddElement(CanvasTool.Mirror, new(0, 0), new(100, 0));
    editor.AddElement(CanvasTool.ConvexLens, new(0, 200), new(100, 200));
    editor.SelectInRectangle(new(-1, -1), new(101, 201)); Check(editor.GroupSelection());
    editor.SetSelectedName("Teaching group"); editor.SetSelectedTemporarilyHidden(true);
    Check(editor.Scene.Mirrors[0].IsTemporarilyHidden && editor.Scene.LensElements[0].IsTemporarilyHidden);
    editor.SetSelectedOrigin(50, 50); editor.RotateSelectedBy(90);
    var grouped = OpticalSceneCodec.Instance.Snapshot(editor.Scene);
    Check(editor.UngroupSelection());
    doc.UndoCommand.Execute(null); Check(OpticalSceneCodec.Instance.ContentEquals(editor.Scene, grouped));
    editor.DeleteItemAt(editor.Scene.Mirrors[0].Start);
    Check(editor.Scene.Mirrors.Length == 0 && editor.Scene.LensElements.Length == 0 && editor.Scene.ElementGroups.Length == 0);
    doc.UndoCommand.Execute(null); Check(OpticalSceneCodec.Instance.ContentEquals(editor.Scene, grouped));
});

Test("Editor: every optical element survives an undo/redo round trip", () =>
{
    foreach (var tool in Enum.GetValues<CanvasTool>().Where(tool => tool is not (CanvasTool.Pan or CanvasTool.Move or CanvasTool.Delete)))
    {
        var editor = new SceneEditor();
        var doc = new SceneDocumentViewModel<OpticalScene>(new FakeStorage<OpticalScene>(), OpticalSceneCodec.Instance);
        editor.SetScene(doc.Scene);
        editor.SceneUpdated += (_, _) => doc.Scene = editor.Scene;
        editor.SceneCommitted += (_, _) => doc.Commit(editor.Scene);
        doc.SceneReplaced += (_, _) => editor.SetScene(doc.Scene);
        if (tool is CanvasTool.PointLight or CanvasTool.CompositePointLight)
            editor.AddPointLight(new(0, 0), tool == CanvasTool.PointLight ? LightSpectrumKind.Monochromatic : LightSpectrumKind.Composite);
        else
            Check(editor.AddElement(tool, new(0, 0), new(100, 0)), tool.ToString());
        var placed = OpticalSceneCodec.Instance.Snapshot(editor.Scene);
        editor.SelectInRectangle(new(-101, -101), new(201, 101));
        editor.SetSelectedOrigin(30, 20);
        var moved = OpticalSceneCodec.Instance.Snapshot(editor.Scene);
        doc.UndoCommand.Execute(null);
        Check(OpticalSceneCodec.Instance.ContentEquals(editor.Scene, placed), tool + " undo");
        doc.RedoCommand.Execute(null);
        Check(OpticalSceneCodec.Instance.ContentEquals(editor.Scene, moved), tool + " redo");
    }
});

async Task RoundTrip<T>(ISceneCodec<T> codec, T scene) where T : class
{
    var normalized = codec.Normalize(scene);
    using var buffer = new MemoryStream(); await codec.SaveAsync(normalized, buffer); buffer.Position = 0;
    Check(codec.ContentEquals(normalized, await codec.LoadAsync(buffer)));
}
AsyncTest("Electromagnetic files: all source types and names round trip", async () =>
{
    await RoundTrip(ElectrostaticSceneCodec.Instance, new("电场", [new(new(10, 20), -3, "负电荷")], [new(new(0, 0), new(200, 0), 120, "极板")]));
    await RoundTrip(MagnetostaticSceneCodec.Instance, new("磁场", [new(new(0, 0), new(100, 0), -2, "导线")],
        [new(new(20, 30), 3, "垂直导线")], [new(new(30, 40), 80, 2, "线圈")], [new(new(60, 70), 100, 45, -3, "垂直线圈")]));
});
AsyncTest("Optical files: all elements, groups and hidden state round trip", async () =>
{
    var editor = new SceneEditor(); editor.SetScene(OpticalScene.CreateEmpty());
    foreach (var tool in Enum.GetValues<CanvasTool>().Where(tool => tool is not (CanvasTool.Pan or CanvasTool.Move or CanvasTool.Delete or CanvasTool.PointLight or CanvasTool.CompositePointLight)))
        editor.AddElement(tool, new(0, 0), new(100, 0));
    editor.AddPointLight(new(-20, 0), LightSpectrumKind.Composite);
    editor.SelectInRectangle(new(-101, -101), new(201, 101)); editor.GroupSelection(); editor.SetSelectedTemporarilyHidden(true);
    await RoundTrip(OpticalSceneCodec.Instance, editor.Scene);
});
AsyncTest("Optical legacy versions 1–14 load with defaults", async () =>
{
    for (var version = 1; version <= 14; version++)
    {
        using var stream = Json(($$$"""{"dataVersion":{{{version}}},"scene":{"name":"Old","lightSources":[{"position":{"x":0,"y":0},"directionDegrees":0}],"mirrors":[]}}"""));
        var scene = await SceneSerializer.LoadAsync(stream);
        Check(scene.LightSources[0].Id != Guid.Empty && scene.LightSources[0].Name is not null);
        Near(scene.LightSources[0].WavelengthNanometers, 580);
    }
});
AsyncTest("File validation: wrong module/version, null arrays/elements, malformed geometry", async () =>
{
    foreach (var json in new[]
    {
        """{"dataVersion":15,"scene":{"lightSources":[],"mirrors":[]}}""",
        """{"dataVersion":1,"sceneType":"electrostatic","scene":{"lightSources":[],"mirrors":[]}}""",
        """{"dataVersion":14,"scene":{"lightSources":null,"mirrors":[]}}""",
        """{"dataVersion":14,"scene":{"lightSources":[null],"mirrors":[]}}""",
        """{"dataVersion":14,"scene":{"lightSources":[],"mirrors":[{"start":{"x":1,"y":2},"end":{"x":1,"y":2}}]}}"""
    })
        await ThrowsAsync<InvalidDataException>(async () => { using var stream = Json(json); await SceneSerializer.LoadAsync(stream); });
    using var optical = new MemoryStream(); await SceneSerializer.SaveAsync(Optical(), optical); optical.Position = 0;
    await ThrowsAsync<InvalidDataException>(() => ElectromagneticSceneSerializer.LoadElectrostaticAsync(optical));
});
AsyncTest("File size limit", async () =>
{
    using var stream = new MemoryStream(new byte[16 * 1024 * 1024 + 1]);
    await ThrowsAsync<InvalidDataException>(() => SceneSerializer.LoadAsync(stream));
});
AsyncTest("Atomic save: replacement and cancelled write preserve previous file", async () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "lightdraw-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        var path = Path.Combine(directory, "scene.json"); await File.WriteAllTextAsync(path, "previous");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await ThrowsAsync<OperationCanceledException>(() => AtomicSceneFile.WriteAsync(path, Encoding.UTF8.GetBytes("new"), cancellation.Token));
        Check(await File.ReadAllTextAsync(path) == "previous"); Check(Directory.GetFiles(directory).Length == 1);
        await AtomicSceneFile.WriteAsync(path, Encoding.UTF8.GetBytes("complete"));
        Check(await File.ReadAllTextAsync(path) == "complete"); Check(Directory.GetFiles(directory).Length == 1);
    }
    finally { Directory.Delete(directory, recursive: true); }
});

Test("Ray tracing: plane mirror and screen", () =>
{
    var source = new LightSource(new(-100, 0), 0, Kind: LightSourceKind.ParallelLine);
    var scene = new OpticalScene("Reflection", [source], [new(new(0, -100), new(0, 100))]);
    var result = new RayTracer().Trace(scene, new(RaysPerSource: 1));
    Check(result.Segments.Count == 2); Near(result.Segments[0].End.X, 0);
    Check(result.Segments[1].End.X < result.Segments[1].Start.X);
    result = new RayTracer().Trace(scene with { Mirrors = [], Screens = [new(new(0, -100), new(0, 100))] }, new(RaysPerSource: 1));
    Check(result.Segments.Count == 1); Near(result.Segments[0].End.X, 0);
});
Test("Ray tracing: splitter conserves intensity, lens converges at focus", () =>
{
    var source = new LightSource(new(-100, 20), 0, Kind: LightSourceKind.ParallelLine);
    var scene = new OpticalScene("Split", [source], [], BeamSplitters: [new(new(0, -100), new(0, 100))]);
    var result = new RayTracer().Trace(scene, new(RaysPerSource: 1));
    Check(result.Segments.Count == 3); Near(result.Segments.Skip(1).Sum(segment => segment.Intensity), 1);
    scene = scene with { BeamSplitters = [], Lenses = [new(new(0, -100), new(0, 100), LensKind.Convex, 300)] };
    var refracted = new RayTracer().Trace(scene, new(RaysPerSource: 1)).Segments[1];
    var direction = (refracted.End - refracted.Start).Normalized();
    Near(refracted.Start.Y + (300 - refracted.Start.X) * direction.Y / direction.X, 0);
});
Test("Fields: charge superposition and current reversal", () =>
{
    var electro = new ElectrostaticSimulator();
    var field = electro.ElectricFieldAt(new(0, 0), [new(new(-100, 0)), new(new(100, 0))]);
    Near(field.Length, 0);
    Near(electro.ElectricPotentialAt(new(1000, 0), [new(new(0, 0), 1)]), 8.9875517923, 1e-6);
    var magnetic = new MagnetostaticSimulator();
    var positive = magnetic.MagneticFieldInPlaneAt(new(1000, 0), [new(new(0, 0), 1)]);
    var negative = magnetic.MagneticFieldInPlaneAt(new(1000, 0), [new(new(0, 0), -1)]);
    Near(positive.Length, 2e-7, 1e-14); Near((positive + negative).Length, 0, 1e-14);
});

AsyncTest("Headless UI: real bindings, shortcuts, property commits, prompts and multi-window close", UiChecks.RunAsync);

var failures = 0;
foreach (var (name, run) in tests)
{
    try { await run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception exception) { failures++; Console.Error.WriteLine($"FAIL {name}\n{exception}"); }
}
Console.WriteLine($"{tests.Count - failures}/{tests.Count} checks passed.");
return failures == 0 ? 0 : 1;

sealed class FakeStorage<T> : ISceneStorageService<T> where T : class
{
    public OpenedScene<T>? Opened { get; set; }
    public string? SaveName { get; set; }
    public Exception? SaveError { get; set; }
    public Exception? OpenError { get; set; }
    public Func<T, Task<string?>>? SaveHandler { get; set; }
    public Task<OpenedScene<T>?> OpenAsync(CancellationToken cancellationToken = default) =>
        OpenError is { } error ? Task.FromException<OpenedScene<T>?>(error) : Task.FromResult(Opened);
    public Task<string?> SaveAsync(T scene, CancellationToken cancellationToken = default) =>
        SaveError is { } error ? Task.FromException<string?>(error) : SaveHandler?.Invoke(scene) ?? Task.FromResult(SaveName);
}
