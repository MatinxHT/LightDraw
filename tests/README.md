# Regression checks

Run `dotnet run --project tests/LightDraw.Tests` from the repository root. This is an executable test runner: it prints each result and exits with a nonzero code on failure. It references the production Core, Rendering and Desktop assemblies; Avalonia.Headless creates test windows without launching a native application.

Coverage:

- Bounded history, isolated snapshots, no-op edits, redo branches and saved-state tracking.
- Save/open/reset/close for all three scene types, including picker cancellation, failed saves and edits made during an outstanding save.
- Optical placement, dragging, groups, names, visibility, transforms and deletion.
- Optical versions 1–14, electromagnetic round trips, malformed input and file size limits.
- Local atomic replacement and cancellation preserving the previous file.
- Reflection, screen interception, beam splitter intensity, lens focus, electric superposition and magnetic current reversal.
- Real window bindings, keyboard shortcuts, committing focused property fields before saving, confirmation cancellation and multi-window close review.
- Desktop startup notice and browser workspace bindings, scene switching, dirty-state aggregation and electric/magnetic download and reopen, using a stub for browser JavaScript interop.

Set `LIGHTDRAW_TEST_SCREENSHOT` to a PNG path to export the headless optical window for layout inspection. The output is optional and not committed.

Browser interop checks run with `node tests/fileInterop.test.mjs`. They cover page-leave warnings, cancelling the file picker, the size limit and a successful file read.

## Manual platform checks

Native file pickers, browser download completion and operating-system shutdown are platform services. Check these on the deployment platform:

1. Create and drag elements, edit a property, undo and redo; confirm each complete operation is one step.
2. Save each simulation, change it, then undo back to the saved state; the unsaved marker should disappear.
3. Cancel a save picker from the unsaved prompt; the pending open/reset/close must stop.
4. Close the main window with all three scenes dirty, then cancel the last prompt; all windows must remain open.
5. Open the saved electric/magnetic files in matching windows; opening the wrong type must report an error and retain the scene.
6. In the browser, edit all three scenes, switch between them, and verify edits and undo history persist. Save/download and reopen each scene, including files from desktop; confirm a wrong scene type is rejected. Verify canvas PNG export in each scene.
7. With any one scene dirty (including an inactive scene or an uncommitted property edit), reload/close and verify the browser's native leave warning. Confirm cancelling open/reset retains the scene and blocks switching during the prompt.
8. Test Cmd+Q on macOS and the native window close controls, including when a file picker is open.

With the .NET 11 SDK pinned in `global.json`, install `wasm-tools` for a runnable browser build. `-p:WasmBuildNative=false` checks managed code and XAML only; its output is not a runnable replacement because Skia/HarfBuzz native libraries are not linked.
