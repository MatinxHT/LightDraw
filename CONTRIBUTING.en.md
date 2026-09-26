# Contributing to LightDraw (光绘课堂)

[中文](CONTRIBUTING.md) | English

Thank you for helping improve LightDraw. Contributions of code, tests, documentation, design, translations, teaching examples, and issue reports are welcome.

## Before You Start

1. Read the repository's [README](README.en.md) and make sure your proposal fits its teaching-first, cross-platform approach and separation of computation from the interface.
2. Search existing Issues to avoid duplicate reports or implementations.
3. You can submit a Pull Request directly for a small bug fix or documentation change. For a new optical element, a scene format change, or a substantial refactor, please open an Issue for discussion first.
4. Do not submit secrets, account details, students' personal information, teaching materials you are not authorized to use, or assets with unclear origins.

## Features Outside the Project's Scope

1. Force analysis and motion simulation for charged particles. The curriculum expects students to reason through these topics themselves; watching a simulation alone may not build that understanding.
2. Research-level questions such as the effects of different mirror coatings. Please use specialized research software, such as Ansys, for these tasks.

## Development Workflow

1. Fork the repository and create a clearly named branch for one focused change from the default branch.
2. Keep the change focused. Avoid unrelated formatting or refactoring in the same commit.
3. Add tests for changes to algorithms or behavior. If the relevant module has no test project yet, describe how you verified the change and suggest follow-up tests in the Pull Request.
4. Update affected README content, scene format documentation, or interface text.
5. Before submitting, run:

   ```shell
   dotnet restore LightDraw.slnx
   dotnet build LightDraw.slnx -c Release
   ```

6. If you tested manually on Windows, macOS, or Linux, state the platform and steps in the Pull Request.

## Code Conventions

- Follow the repository's existing C# style, keep nullable reference types enabled, and resolve all compiler warnings.
- Put core geometry and simulation logic in `LightDraw.Core`, without dependencies on Avalonia, SkiaSharp, or platform APIs.
- Put rendering logic in `LightDraw.Rendering.Skia`; put desktop windows and user flows in `LightDraw.Desktop`, and browser-specific views and file interactions in `LightDraw.Browser`.
- Add concise comments for public APIs, complex formulas, and non-obvious edge cases.
- Account for tolerance, degenerate segments, parallel or collinear lines, and extreme zoom when comparing floating-point values.
- Consider compatibility with old scene files when adding persisted fields. Breaking changes require a higher `dataVersion` and a migration plan.
- Prefer clear, accurate Simplified Chinese for user-visible text, and avoid unnecessary technical jargon.

## Bug Reports

Where possible, include your operating system, architecture, and .NET SDK version; the affected commit or app version; minimal reproduction steps; actual and expected results; and a scene file, screenshot, or error log that can be shared publicly. For an optical calculation error, include the geometry, theoretical result, or a textbook or paper that readers can check.

## Pull Request Description

Describe the problem and its teaching value, the implementation and important trade-offs, automated tests and manual verification, effects on the UI, file format, performance, and compatibility, and any third-party material and its license.

## License and Originality

This project uses the PolyForm Noncommercial License 1.0.0. By submitting an Issue, Pull Request, or other contribution, you confirm that:

- You created the contribution or have sufficient rights to provide it under this project's license.
- You agree to make your contribution available to the public under the repository's current license.
- You understand that the license permits non-commercial use only and that this is a source-available project, not OSI-approved open-source software.
- You have disclosed the origins and licenses of all third-party code, data, images, fonts, or text.

Do not copy code directly from a project with an incompatible license. Attribution alone does not resolve a license conflict. If you are unsure, open an Issue for discussion first.

## Code of Conduct

Respect people with different backgrounds and viewpoints, and keep discussion focused on the issue. Personal attacks, harassment, discrimination, disclosure of others' private information, and behavior that undermines community safety are prohibited. Maintainers may edit, hide, or reject content that violates these principles and restrict repeat offenders from participating.
