# LightDraw User Guide

[Back to README](../README.en.md) | [中文](guide.md)

## Choose an edition

The browser edition provides the geometric optics canvas. A desktop browser with a viewport of at least 920 × 600 px is recommended. Open a compatible `.lightdraw.json` file to load a scene; saving a scene or exporting the canvas downloads a scene file or a PNG image.

The desktop edition also provides electrostatics and magnetostatics. After extracting a portable package from GitHub Releases, run `LightDraw.exe` on Windows, open `LightDraw.app` on macOS, or run `./LightDraw` on Linux. The macOS package is not notarized by Apple, so you may need to allow it in system settings on first launch. Linux requires a desktop environment and the Avalonia/Skia system dependencies.

## Usage

| Action | Effect |
| --- | --- |
| Theme dropdown to the left of the top language selector | Instantly switch between dark/light themes across all windows; default is light (white canvas + pale blue UI); dark uses warm graphite panels, charcoal canvas, and champagne-colored selection accents |
| Language dropdown at the top | Switch interface text between Simplified Chinese and English |
| About | View the version, simulation methods, project repository, license, and acknowledgements; shown in a window on desktop and an in-page panel in the browser |
| Select the pan tool and drag with left mouse button | Pan the canvas |
| Move or adjust components | Dragging on blank space pans; click any component to select it, then drag the first anchor to translate; drag the white point located 100 mm along the orthogonal direction to rotate around a fixed origin; all lengths are edited in the property panel, and ray paths refresh in real time while dragging |
| Properties of a selected element | Edit its name, coordinates, and applicable optical parameters; group or ungroup elements and set the primary element of a group |
| Temporarily hide an element | Select an optical element and check **Temporarily hide**. The element remains visible on the canvas but is excluded from ray tracing, so rays pass through it |
| Delete a component | Click a light source, mirror, beam splitter, screen, aperture, grating, or lens to delete; after deletion, the tool automatically returns to pan mode |
| Hold right mouse button while using any drawing tool and drag | Temporarily pan the canvas |
| Scroll mouse wheel | Zoom centered at the current cursor position |
| Monochromatic point source | Single click to place; default wavelength is 580 nm with uniform 360° emission; after selection you can edit wavelength and emission spread, and rotate beam direction with the white rotation handle |
| Monochromatic parallel source | Click twice to define the emission segment; default wavelength is 580 nm and rays emit parallel to the segment normal; wavelength is editable after selection |
| Composite point source | Single click to place; composed of equal-strength 450/550/650 nm components, rendered yellow before dispersion; geometric editing is the same as monochromatic point source |
| Composite parallel source | Click twice to define the emission segment; composed of equal-strength 450/550/650 nm components, rendered yellow before dispersion; geometric editing is the same as monochromatic parallel source |
| Plane mirror / plane beam splitter / screen / aperture / reflection grating / convex lens / concave lens | First click sets start point, second click sets length and orientation, then the tool returns to pan mode; beam splitter preserves 50% intensity in both transmitted and reflected branches; rays stop when they hit a screen; reflection grating generates propagating diffraction orders by wavelength and groove density |
| Ideal concave spherical mirror / ideal convex spherical mirror | First click sets mirror vertex (first anchor), second click sets center of curvature (second anchor), direction, and initial radius; default central angle is 180°. During editing, the second anchor on canvas changes direction only and does not stretch radius; the property panel directly edits two anchor coordinates, central angle, radius, and focal length. When radius or focal length changes, the first anchor stays fixed, second anchor moves along current axis, and `f = R/2` is always enforced; concave/convex mirrors only reflect on the side facing/taking the opposite of the curvature center respectively |
| Concave grating | Same interaction as ideal concave spherical mirror: first click sets grating vertex, second click sets curvature center, direction, and radius; defaults: 180° central angle and 600 lines/mm. It only accepts rays incident from the concave side and generates diffraction orders on the local tangent plane using the reflection grating equation |
| Esc | Cancel placement of the current object |
| Reset scene | Clear all light sources and optical components, restoring a blank scene |
| Fit to window | Restore default viewport range |
| Ray density | Adjust the number of generated rays per source in real time |
| Open scene / Save scene | Read/write `.lightdraw.json` scene files |
| Export canvas | Export the current optical canvas as a PNG; choose a save location on desktop or download it directly in the browser |

## Edit protection and scene documents

Optics (desktop and browser), electrostatics and magnetostatics share a document toolbar:

- Undo/redo retains the last 100 completed edits. A drag is one edit; text properties commit on Enter or focus loss. Creating, deleting, grouping, renaming and hiding elements can all be undone. A new edit clears the redo branch.
- The unsaved marker compares scene content with the last successful save. Returning to saved content clears it. Pan, zoom and display density are not document edits.
- Opening, resetting and closing offer Save, Don’t save and Cancel when needed. Cancelled or failed saves preserve the current scene. Reset is undoable; opening another scene starts fresh history.
- Closing the main window or quitting reviews every simulation window before any closes. Cancelling any review keeps all windows open.
- Use Ctrl+Z, Ctrl+Shift+Z / Ctrl+Y, Ctrl+S and Ctrl+O; use ⌘ on macOS. Text fields retain their own text undo/redo.
- Local files are fully serialized and written to a temporary file before replacement. Other storage providers may not support atomic replacement.

The browser registers a native page-leave warning while dirty. The browser controls when it appears. Saving initiates a download; the application cannot confirm that it ultimately reaches disk. History lasts for the current session; automatic saving and crash recovery are not included.

Electrostatic and magnetostatic `.lightdraw.json` files use `sceneType` values `electrostatic` and `magnetostatic`, respectively, with independent `dataVersion: 1` formats. Open each in its corresponding window. All source geometry, physical parameters and names are preserved. Optical files retain version 14 and support valid versions 1–13.

Imports are limited to 16 MiB and 10,000 elements per collection. Invalid entries, duplicate optical IDs, non-finite numbers, numbers with magnitude above 10⁹ and zero-length elements are rejected before replacing a scene.
