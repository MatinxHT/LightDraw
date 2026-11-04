# 光绘课堂技术参考

[返回首页](../README.md) | [English](technical.en.md)

## 构建与发布

### 环境要求

- .NET SDK 10.0.400，或与 `global.json` 兼容的 .NET 10 SDK；
- 桌面版运行环境：Windows 10/11、macOS，或支持 X11/Wayland 的 Linux；
- 本地构建浏览器版时还需要 `wasm-tools` 工作负载。

### GitHub Release 自动发布

工作流 `.github/workflows/release.yml` 在推送到 `master` 时自动运行，从 `Directory.Build.props` 的 `<Version>` 读取 `主版本.次版本.修订版本`（例如 `0.7.3`）。该版本尚未发布时，自动构建并创建 Release，名称为 `0.7.3`，关联标签 `v0.7.3` 由工作流自动创建，无需手动打标签；应用和压缩包使用相同版本号。

以后发布新版本，只需修改 `<Version>`，提交并推送到 `master`。如果该版本已经公开发布，工作流会成功跳过构建和发布，不覆盖已有附件。也可在 Actions → Release desktop → Run workflow 中选择 `master` 手动运行，无需填写版本参数；首次手动运行前，工作流需已合入默认分支。

| 平台 | Release 附件 |
| --- | --- |
| Windows x64 / ARM64 | `LightDraw-<版本>-win-x64.zip` / `LightDraw-<版本>-win-arm64.zip` |
| macOS Apple Silicon | `LightDraw-<版本>-osx-arm64.tar.gz`（含 `LightDraw.app`） |
| Linux x64 / ARM64 | `LightDraw-<版本>-linux-x64.tar.gz` / `LightDraw-<版本>-linux-arm64.tar.gz` |

每个压缩包包含 .NET 运行时、许可证和说明文件，并附带独立的 `.sha256` 校验文件。Windows 解压后运行 `LightDraw.exe`，macOS 解压后打开 `LightDraw.app`，Linux 解压后运行 `./LightDraw`；Linux 仍需桌面环境及 Avalonia/Skia 的系统依赖。macOS 包使用临时签名，未经过 Apple 公证，首次打开可能需要在系统设置中允许运行。

构建产物在各 runner 的临时磁盘打包后直接上传到 Release 草稿，五个平台全部成功且附件齐全后才公开发布。失败时保留草稿，可在 Actions 中重新运行原工作流的失败任务；如果已有草稿或标签指向另一个提交，工作流会停止，避免混合不同提交的产物，此时请重跑原工作流或提升项目版本号。工作流不使用 Actions artifacts 或依赖缓存，不占用它们的存储额度；Actions 执行时间仍遵循仓库的 GitHub 计费规则。

### Microsoft Store 混合架构安装包

在安装了 Windows SDK 10.0.26100.0 打包工具的 Windows 上，从仓库根目录运行：

```powershell
.\scripts\Package-MsixBundle.ps1
```

脚本默认读取项目版本（`Directory.Build.props`），以 Release 配置分别发布包含 .NET 运行时的 x64 和 ARM64 程序，再合并为 `artifacts/msix/<版本>/MartinHungChiho.LightDraw-<版本>.msixbundle`。也可通过 `-Version 0.7.3` 显式指定版本；应用与安装包版本会同步设置。每次构建使用独立的 staging 目录，避免混入历史发布文件。

将生成的 `.msixbundle` 手动上传至 Partner Center 的程序包页面。包沿用本项目的商店标识和发布者，不包含本地测试签名，由 Microsoft Store 签名分发。

## 场景文件格式

场景使用 UTF-8 JSON，并通过 `dataVersion` 标记数据结构版本。当前版本为 `14`，并可继续读取版本 `1`～`13`。所有世界坐标、长度、通孔、半径和焦距均以毫米（`mm`）计：

```json
{
  "dataVersion": 14,
  "scene": {
    "name": "双镜面反射演示",
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

字段说明：

- `dataVersion`：场景文件格式版本，用于未来的数据迁移；
- `scene.name`：场景显示名称；
- `lightSources[].position`：光源在世界坐标系中的位置；
- `directionDegrees`：中心出射方向，单位为度；移动/编辑属性栏中，点光源及线状元件的第二原点表示距第一原点 100 mm 的白色旋转点，凹、凸球面镜的第二原点仍表示曲率圆心；
- `spreadDegrees`：扇形发射角度，单位为度；
- `wavelengthNanometers`：波长，单位为纳米；单色光源默认为 580 nm，用户修改后的真实值会保存并直接参与光栅方程计算。复色光源在场景中以 550 nm 作为参考值，实际分光计算使用不可修改的 450、550、650 nm 三个等强分量；
- `spectrum`：光谱类型，`monochromatic` 为单色光源，`composite` 为复色光源；旧场景未包含此字段时按单色光源读取；
- `kind`：光源类型，`point` 或 `parallelLine`；线光源还会保存 `end` 端点；
- `mirrors[].start/end`：有限线段镜面的两个端点。
- `concaveSphericalMirrors[].vertex`：凹球面镜的镜面中心点（第一原点）；`centerOfCurvature` 为球心（第二原点），两点距离为曲率半径；`arcAngleDegrees` 为镜面圆心角，焦距由 `f = R/2` 自动确定。
- `convexSphericalMirrors[]`：字段与凹球面镜一致，但有效反射面位于背向球心的一侧。
- `beamSplitters[].start/end`：平面分光镜的两个端点；透射和反射分支的光强各为入射光的 50%。
- `screens[].start/end`：有限线段光屏的两个端点；命中光屏后光线立即终止传播。
- `apertures[].start/end`：光阑外部线段的两个端点；`openingSize` 为中央通孔大小。
- `reflectionGratings[].start/end`：反射光栅的两个端点；`grooveDensityLinesPerMillimeter` 为刻线密度（线/mm）。
- `concaveGratings[]`：凹面光栅；`vertex`、`centerOfCurvature` 和 `arcAngleDegrees` 的几何定义与理想凹球面镜一致，另以 `grooveDensityLinesPerMillimeter` 设置刻线密度。
- `lenses[].start/end`：薄透镜的两个端点，另含 `kind` 与 `focalLength`；新建凸、凹透镜的默认基准焦距均为 300 mm，聚散性质由 `kind` 决定。`dispersionMode` 可取 `none`、`normal`、`anomalous`，分别表示理想无色散、正常色散和反常色散；`dispersionLevel` 范围为 0～10，默认 5，仅在色散模式下生效。旧场景缺少这两个字段时迁移为 `none` 和 `5`。
- 光学元件可保存自定义 `name` 和 `isTemporarilyHidden`；后者为 `true` 时仍绘制元件，但追迹时忽略它。场景还可包含 `groups`，记录组合成员及主元件；这些字段缺失的旧场景仍可读取。

色散透镜以 550 nm 绿光焦距为 `f₀`。令 `t = clamp((λ - 550) / 100, -1, 1)`、`s = dispersionLevel × 0.05`，正常色散使用 `f(λ) = f₀ × (1 + st)`，反常色散使用 `f(λ) = f₀ × (1 - st)`。因此正常色散下蓝光焦距较短、红光焦距较长，反常色散则相反。等级 5、基准焦距 300 mm 时，正常色散的 450/550/650 nm 焦距分别为 225/300/375 mm。复色光第一次命中色散透镜时拆分为三个等强分量，后续透镜按各分量波长继续计算，不会重复拆分。

反射光栅采用切向波矢形式的光栅方程。计算时先将波长从纳米换算为毫米：`λ(mm) = λ(nm) × 10⁻⁶`，再按 `sin βₘ = sin α + mλ/d` 求可传播级次，其中 `d` 为光栅常数。凹面光栅在每个命中点以球面法线及其垂直方向建立局部法线—切线坐标系，再使用同一方程，因此 0 级光按球面镜反射并具有 `f = R/2` 的几何聚焦性质。仅追踪 0、±1、±2、±3 级。单色光始终使用用户设置的真实波长计算衍射角；绘制颜色则与参考表做绝对差最小匹配：390 nm 紫色、450 nm 蓝色、550 nm 绿色、580 nm 黄色、650 nm 红色，因此默认 580 nm 显示为黄色。若目标波长恰好位于两个参考值的中点，则以黄色 580 nm 为系统重心，选择两个候选颜色中更靠近 580 nm 的一侧。复色光在分光前以黄色绘制，其无色散的 0 级也保留为一条黄色混合光；只有 ±1、±2、±3 级会将固定的 450、550、650 nm 分量分别以蓝、绿、红色绘制，并按各自波长计算色散角。三个分量的初始强度比为 1:1:1；0 级光强为入射混合光的 90%，+1 与 -1 级的各波长分量为对应入射分量的 50%，+2 与 -2 级各为 25%，+3 与 -3 级各为 10%。另设全场景线段数量上限以控制交互性能。

未来修改格式时应提升 `dataVersion` 并提供显式迁移器，不应静默改变已有字段的含义。

## 技术架构

```text
LightDraw
├─ assets
│  └─ app-store          受版本控制的应用商店海报
├─ docs                  使用说明与技术参考
├─ src/LightDraw.Core
│  ├─ Geometry           向量和几何基础类型
│  ├─ Scene              平台无关的场景模型
│  ├─ Simulation         光线生成、求交和反射
│  ├─ Electromagnetics   静电场与磁静态模型及模拟器
│  └─ Persistence        带版本号的 JSON 场景读写
├─ src/LightDraw.Rendering.Skia
│  ├─ Optics             光学画布、场景编辑和 Skia 绘制
│  ├─ Electrostatics     静电场交互画布
│  └─ Magnetostatics     磁静态交互画布
├─ src/LightDraw.Desktop
│  ├─ Views              Avalonia 桌面窗口和界面布局
│  ├─ ViewModels         两端共用的主界面状态与命令
│  ├─ Services           本地文件选择、场景存储与本地化服务
│  └─ Assets             应用内使用的品牌和图标资源
└─ src/LightDraw.Browser
   ├─ Views              Avalonia 浏览器界面与关于面板
   ├─ Services           浏览器文件选择、下载与场景存储
   └─ wwwroot            WebAssembly 入口、脚本与静态资源
```

依赖方向保持为：

```text
LightDraw.Core ← LightDraw.Rendering.Skia ← LightDraw.Desktop / LightDraw.Browser
```

`LightDraw.Core` 不引用 Avalonia、SkiaSharp、Windows API 或 macOS API，因此可以独立测试，并已复用于浏览器版。浏览器版还复用光学画布、主界面 ViewModel 和本地化服务；浏览器专用代码负责文件选择与下载。仓库根部的 `assets/app-store` 保存需要纳入版本控制的商店展示海报；构建、发布和打包产生的文件不纳入版本控制。

### 编辑器结构与测试

`SceneEditor.cs` 保留编辑状态和事件，操作按职责放入 `SceneEditor.Placement`、`Selection`、`Groups`、`Properties`、`Transforms`、`Drag`、`Deletion` 和 `Geometry` 分部文件。默认值处理独立到 Core 的场景归一化器。`SceneHistory<T>` 管理内容快照；三个窗口共用 `SceneDocumentViewModel<T>` 处理保存、打开、撤销与未保存提醒。

运行回归检查（控制台测试程序，失败时返回非零退出码）：

```bash
dotnet run --project tests/LightDraw.Tests
```

检查包括三类场景的文档流程、旧格式读取、编辑器操作、关键计算结果，以及 Avalonia Headless 无界面窗口集成测试。详见 [测试说明](../tests/README.md)。

### 模拟与绘制

`RayTracer` 将场景计算为与 UI 无关的光线线段集合。`OpticalCanvas` 在 Avalonia 的自定义绘制操作中获取当前 SkiaSharp 画布，将线段合并到路径后批量绘制。模拟层和显示层之间只传递数据，以便未来加入后台计算、取消令牌、空间索引和可插拔模拟引擎。

## 设计原则

- **教学优先**：交互和术语应便于课堂演示，而不是堆叠工程软件式参数；
- **计算与界面分离**：核心算法不依赖桌面框架；
- **跨平台一致**：避免无必要的平台专属 API；
- **性能可扩展**：大量光线优先批量计算和批量绘制；
- **格式可迁移**：所有持久化数据都有显式版本；
- **社区共建**：重要行为变更需要测试、文档和清晰的提交说明。
