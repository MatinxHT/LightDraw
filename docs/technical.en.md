# LightDraw Technical Reference

[Back to README](../README.en.md) | [中文](technical.md)

## Building and Releasing

### Requirements

- .NET SDK 10.0.400, or a compatible .NET 10 SDK as specified by `global.json`
- Desktop runtime: Windows 10/11, macOS, or Linux with X11/Wayland support
- The `wasm-tools` workload is also required to build the browser edition locally.

### Automated GitHub Releases

The `.github/workflows/release.yml` workflow runs on pushes to `master` and reads the `MAJOR.MINOR.PATCH` version from `<Version>` in `Directory.Build.props` (for example, `0.7.3`). If that version has not been published, it builds and creates a Release titled `0.7.3`, automatically creating the associated `v0.7.3` tag. No manual tagging is needed; the app and archives use the same version.

To publish subsequent versions, update `<Version>`, commit, and push to `master`. If the version is already public, the workflow successfully skips building and publishing without replacing assets. You can also select `master` under Actions → Release desktop → Run workflow, with no version input required; the workflow must be on the default branch before its first manual run.

| Platform | Release assets |
| --- | --- |
| Windows x64 / ARM64 | `LightDraw-<version>-win-x64.zip` / `LightDraw-<version>-win-arm64.zip` |
| macOS Apple Silicon | `LightDraw-<version>-osx-arm64.tar.gz` (contains `LightDraw.app`) |
| Linux x64 / ARM64 | `LightDraw-<version>-linux-x64.tar.gz` / `LightDraw-<version>-linux-arm64.tar.gz` |

Each archive includes the .NET runtime, license, and documentation, with a separate `.sha256` checksum file. Extract and run `LightDraw.exe` on Windows, open `LightDraw.app` on macOS, or run `./LightDraw` on Linux. Linux still requires a desktop environment and Avalonia/Skia system dependencies. The macOS app is ad-hoc signed and is not Apple-notarized; its first launch may require permission in System Settings.

Each runner packages files on its temporary disk and uploads directly to a draft Release. The release becomes public only after all five builds succeed and all assets are present. Failures leave the draft available for retrying failed jobs in the original workflow run. If an existing draft or tag targets another commit, the workflow stops to avoid mixing builds from different commits; rerun the original workflow or bump the project version. No Actions artifacts or dependency caches are used, so these storage quotas are not consumed. Actions execution time remains subject to the repository's GitHub billing rules.

### Microsoft Store bundle

On Windows with the Windows SDK 10.0.26100.0 packaging tools installed, run from the repository root:

```powershell
.\scripts\Package-MsixBundle.ps1
```

The script reads the project version from `Directory.Build.props`, publishes self-contained Release builds for x64 and ARM64, and combines them into `artifacts/msix/<version>/MartinHungChiho.LightDraw-<version>.msixbundle`. Use `-Version 0.7.3` to override both the application and package version. Each build uses a fresh staging directory to exclude obsolete published files.

Manually upload the `.msixbundle` on the Partner Center Packages page. The bundle retains the project's Store identity and publisher and has no local test signature; Microsoft Store signs it for distribution.

## Scene File Format

Scenes are UTF-8 JSON and use `dataVersion` for schema versioning. The current version is `14`, and versions `1` through `13` are still readable. All world coordinates, lengths, openings, radii, and focal lengths use millimeters (`mm`):

```json
{
  "dataVersion": 14,
  "scene": {
    "name": "Double Mirror Reflection Demo",
    "lightSources": [
      {
        "position": { "x": -300, "y": 20 },
        "directionDegrees": -8,
        "spreadDegrees": 38,
        "wavelengthNanometers": 580,
        "spectrum": "monochromatic"
      }
    ],
    "mirrors": [
      {
        "start": { "x": 40, "y": -170 },
        "end": { "x": 105, "y": 165 }
      }
    ],
    "concaveSphericalMirrors": [
      {
        "vertex": { "x": 0, "y": 0 },
        "centerOfCurvature": { "x": 100, "y": 0 },
        "arcAngleDegrees": 180
      }
    ],
    "convexSphericalMirrors": [
      {
        "vertex": { "x": 0, "y": 160 },
        "centerOfCurvature": { "x": 100, "y": 160 },
        "arcAngleDegrees": 120
      }
    ],
    "beamSplitters": [
      {
        "start": { "x": 120, "y": -100 },
        "end": { "x": 220, "y": 0 }
      }
    ],
    "screens": [
      {
        "start": { "x": 300, "y": -170 },
        "end": { "x": 300, "y": 165 }
      }
    ],
    "apertures": [
      {
        "start": { "x": 180, "y": -170 },
        "end": { "x": 180, "y": 165 },
        "openingSize": 60
      }
    ],
    "lenses": [
      {
        "start": { "x": 210, "y": -120 },
        "end": { "x": 210, "y": 120 },
        "kind": "convex",
        "focalLength": 300,
        "dispersionMode": "normal",
        "dispersionLevel": 5
      }
    ],
    "reflectionGratings": [
      {
        "start": { "x": 240, "y": -170 },
        "end": { "x": 240, "y": 165 },
        "grooveDensityLinesPerMillimeter": 600
      }
    ],
    "concaveGratings": [
      {
        "vertex": { "x": 360, "y": 0 },
        "centerOfCurvature": { "x": 460, "y": 0 },
        "arcAngleDegrees": 120,
        "grooveDensityLinesPerMillimeter": 600
      }
    ]
  }
}
```

Field notes:

- `dataVersion`: schema version for future data migration.
- `scene.name`: display name of the scene.
- `lightSources[].position`: light source position in world coordinates.
- `directionDegrees`: central emission direction in degrees. In move/edit mode, the second anchor of point sources and line elements is the white rotation point 100 mm from the first anchor; for concave/convex spherical mirrors, the second anchor remains the center of curvature.
- `spreadDegrees`: fan emission angle in degrees.
- `wavelengthNanometers`: wavelength in nanometers. Monochromatic sources default to 580 nm, and user-edited values are saved and directly used in grating equations. Composite sources use 550 nm as a reference in scene data, while actual dispersion uses fixed equal-strength 450/550/650 nm components.
- `spectrum`: spectrum type; `monochromatic` for monochromatic source, `composite` for composite source. Old scenes without this field are loaded as monochromatic.
- `kind`: source type, `point` or `parallelLine`; line sources also store an `end` point.
- `mirrors[].start/end`: two endpoints of a finite line-segment mirror.
- `concaveSphericalMirrors[].vertex`: mirror vertex (first anchor) of a concave spherical mirror; `centerOfCurvature` is the curvature center (second anchor), and the distance between them is the curvature radius; `arcAngleDegrees` is the central angle. Focal length is automatically derived by `f = R/2`.
- `convexSphericalMirrors[]`: same fields as concave spherical mirrors, but the effective reflective surface is on the side opposite the curvature center.
- `beamSplitters[].start/end`: two endpoints of a planar beam splitter; transmitted and reflected branches each keep 50% of incident intensity.
- `screens[].start/end`: two endpoints of a finite screen; rays terminate immediately on hit.
- `apertures[].start/end`: two endpoints of outer aperture segments; `openingSize` is the central opening size.
- `reflectionGratings[].start/end`: two endpoints of a reflection grating; `grooveDensityLinesPerMillimeter` is groove density (lines/mm).
- `concaveGratings[]`: concave gratings; geometric meaning of `vertex`, `centerOfCurvature`, and `arcAngleDegrees` matches ideal concave spherical mirrors, with `grooveDensityLinesPerMillimeter` for groove density.
- `lenses[].start/end`: two endpoints of thin lenses, plus `kind` and `focalLength`. Default reference focal length for new convex/concave lenses is 300 mm; converging/diverging behavior is determined by `kind`. `dispersionMode` can be `none`, `normal`, or `anomalous` (achromatic, normal dispersion, anomalous dispersion). `dispersionLevel` ranges 0–10 with default 5, active only in dispersion modes. Old scenes missing these fields migrate to `none` and `5`.
- Optical elements can store a custom `name` and `isTemporarilyHidden`. When the latter is `true`, the element is still drawn but ignored during ray tracing. A scene can also contain `groups` with member and primary element IDs. Older scenes without these fields remain readable.

For dispersive lenses, use 550 nm green-light focal length as `f₀`. Let `t = clamp((λ - 550) / 100, -1, 1)` and `s = dispersionLevel × 0.05`. Normal dispersion uses `f(λ) = f₀ × (1 + st)`, anomalous dispersion uses `f(λ) = f₀ × (1 - st)`. Therefore, with normal dispersion blue has shorter focal length and red has longer focal length (reversed for anomalous). At level 5 and base focal length 300 mm, focal lengths at 450/550/650 nm are 225/300/375 mm in normal dispersion. Composite light is split into three equal-strength components at its first hit on a dispersive lens; later lenses continue using each component wavelength without re-splitting.

Reflection gratings use the tangential wave-vector form of the grating equation. Convert wavelength from nm to mm first: `λ(mm) = λ(nm) × 10⁻⁶`, then solve propagating orders with `sin βₘ = sin α + mλ/d`, where `d` is grating spacing. For concave gratings, a local normal–tangent frame is built from spherical normal and its perpendicular direction at each hit point, then the same equation is applied. Thus, zeroth order reflects like a spherical mirror and keeps geometric focusing behavior `f = R/2`. Only orders 0, ±1, ±2, and ±3 are traced. Monochromatic light always uses user-set true wavelength for diffraction-angle calculations; render color is chosen by nearest reference wavelength: 390 nm purple, 450 nm blue, 550 nm green, 580 nm yellow, 650 nm red, so default 580 nm appears yellow. If target wavelength is exactly between two references, 580 nm yellow is treated as system center and the candidate closer to 580 nm is chosen. Composite light is rendered yellow before splitting; its non-dispersed zeroth order also remains one yellow mixed beam. Only ±1/±2/±3 orders split fixed 450/550/650 nm components and render them as blue/green/red while calculating angles by their own wavelengths. Initial component intensity ratio is 1:1:1; zeroth-order intensity is 90% of incident mixed beam, each ±1 component is 50% of its corresponding incident component, each ±2 is 25%, and each ±3 is 10%. A global line-segment cap is also applied to control interactive performance.

When evolving the file format, `dataVersion` should be incremented and an explicit migrator should be provided. Do not silently change meanings of existing fields.

## Technical Architecture

```text
LightDraw
├─ assets
│  └─ app-store          Version-controlled app store posters
├─ docs                  User guide and technical reference
├─ src/LightDraw.Core
│  ├─ Geometry           Vectors and foundational geometry types
│  ├─ Scene              Platform-independent scene models
│  ├─ Simulation         Ray generation, intersection, and reflection
│  ├─ Electromagnetics   Electrostatic/magnetostatic models and simulators
│  └─ Persistence        Versioned JSON scene read/write
├─ src/LightDraw.Rendering.Skia
│  ├─ Optics             Optical canvas, scene editing, and Skia rendering
│  ├─ Electrostatics     Interactive electrostatic canvas
│  └─ Magnetostatics     Interactive magnetostatic canvas
├─ src/LightDraw.Desktop
│  ├─ Views              Avalonia desktop windows and layouts
│  ├─ ViewModels         Main interface state and commands shared by both editions
│  ├─ Services           Local file selection, scene storage, and localization
│  └─ Assets             In-app branding and icon assets
└─ src/LightDraw.Browser
   ├─ Views              Avalonia browser interface and About panel
   ├─ Services           Browser file selection, downloads, and scene storage
   └─ wwwroot            WebAssembly entry point, scripts, and static assets
```

Dependency direction is:

```text
LightDraw.Core ← LightDraw.Rendering.Skia ← LightDraw.Desktop / LightDraw.Browser
```

`LightDraw.Core` does not reference Avalonia, SkiaSharp, Windows API, or macOS API, so it can be tested independently and is already reused by the browser edition. The browser edition also reuses the optical canvas, main interface ViewModel, and localization service; browser-specific code handles file selection and downloads. The root `assets/app-store` directory holds version-controlled store graphics. Build, publish, and package outputs are not version controlled.

### Editor and Tests

The optical editor is split into partial files for placement, selection, groups, properties, transforms, dragging, deletion and geometry. Normalization and history are independent Core components, and all three document workflows share `SceneDocumentViewModel<T>`.

Run the regression checks, including Avalonia Headless window integration tests:

```bash
dotnet run --project tests/LightDraw.Tests
```

See [tests/README.md](../tests/README.md) for coverage and manual checks.

### Simulation and Rendering

`RayTracer` computes scenes into UI-agnostic ray segment collections. `OpticalCanvas` obtains the current SkiaSharp canvas inside Avalonia custom draw operations, merges segments into paths, and draws in batches. Only data is passed between simulation and rendering layers, making it easier to add background computation, cancellation tokens, spatial indexing, and pluggable simulation engines in the future.

## Design Principles

- **Teaching first**: interactions and terminology should favor classroom demonstration over engineering-software-style parameter overload.
- **Compute/UI separation**: core algorithms should not depend on desktop frameworks.
- **Cross-platform consistency**: avoid unnecessary platform-specific APIs.
- **Scalable performance**: prioritize batched computation and batched rendering for large ray counts.
- **Migratable formats**: all persisted data has explicit versioning.
- **Community co-building**: major behavior changes require tests, docs, and clear commit messages.
