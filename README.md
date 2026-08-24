# 光绘课堂 (LightDraw)

**光绘课堂 (LightDraw)** 是一款面向中小学物理课堂、实验演示和几何光学学习的跨平台二维光学绘图与模拟工具。项目希望用直观、流畅的可视化方式帮助教师讲解光的传播与反射，也让学生能够自由搭建场景、观察光路并验证自己的猜想。

项目以公开源码、社区协作和长期维护为方向。欢迎教师、学生、开发者、设计师和光学爱好者提交问题、改进文档、补充测试或实现新功能。

> [!IMPORTANT]
> 本项目采用 [PolyForm Noncommercial License 1.0.0](LICENSE)，仅授权非商业用途。它是“源码可用（source-available）”项目，不是 OSI 定义下的开源软件。未经版权所有者另行书面许可，不得将本项目或其衍生版本用于商业产品、收费服务、商业交付或其他商业目的。

## 项目状态

光绘课堂目前处于早期开发阶段。仓库已经包含可运行的桌面基础版本，主要用于验证以下技术路线：

- 纯 .NET 几何光学计算核心；
- Avalonia 跨平台桌面界面；
- SkiaSharp 高性能批量绘图；
- Windows、macOS 和 Linux 的统一代码基础；
- 兼容应用商店沙盒的场景文件读写。

当前版本适合开发预览、课堂概念演示和技术验证，尚不建议用于精密工程计算或对数值误差敏感的科研工作。

## 当前功能

- 使用纯 C# 实现的平台无关光学核心，不依赖 UI 或操作系统 API；
- 可在画布上放置点光源与线平行光源，并统一设置每个光源的模拟光线数量；
- 通过两次点击绘制平面反光镜、凸透镜和凹透镜，移动鼠标时实时预览长度；
- 有限线段求交、多次镜面反射和理想薄透镜近轴折射；
- 光线辉光混合、镜面、光源、坐标轴和自适应网格绘制；
- 基于 SkiaSharp 路径的光线批量渲染，避免为每条光线创建 UI 控件；
- 鼠标或触控拖动画布，滚轮以指针所在位置为中心缩放；
- 添加平面镜、恢复演示场景和复位视图；
- 打开和保存带版本号的 `.lightdraw.json` 场景文件；
- 使用 Avalonia `StorageProvider` 流式读写文件，兼容 macOS App Sandbox。

## 快速开始

### 环境要求

- .NET SDK 10.0.400，或与 `global.json` 兼容的 .NET 10 SDK；
- Windows 10/11、macOS，或支持 X11/Wayland 的 Linux；
- 可选 IDE：JetBrains Rider、Visual Studio 或 Visual Studio Code。

先确认本机 SDK：

```powershell
dotnet --info
```

### 还原、构建与运行

在仓库根目录执行：

```powershell
dotnet restore LightDraw.slnx
dotnet build LightDraw.slnx -c Release
dotnet run --project src/LightDraw.Desktop/LightDraw.Desktop.csproj
```

首次还原需要连接 NuGet。仓库启用了可空引用类型、确定性构建和“警告视为错误”，提交代码前应确保 Release 构建无警告通过。

## 使用说明

| 操作 | 效果 |
| --- | --- |
| 选择平移工具并按住鼠标左键拖动 | 平移画布 |
| 移动或调整元件 | 拖动元件主体可整体平移；拖动线光源、镜面或透镜的端点可固定另一端并拉伸、旋转，光路在拖动时实时刷新 |
| 删除元件 | 单击光源、镜面或透镜即可删除，成功删除后自动返回平移工具 |
| 使用任意绘制工具时按住右键拖动 | 临时平移画布 |
| 滚动鼠标滚轮 | 以当前指针位置为中心缩放 |
| 点光源 | 单击放置，向 360° 均匀发光，随后自动返回平移工具 |
| 线平行光源 | 两次点击确定发射线段，光线沿线段法线方向平行发射 |
| 平面反光镜 / 凸透镜 / 凹透镜 | 第一次点击确定起点，第二次点击确定长度和朝向，随后自动返回平移工具 |
| Esc | 取消正在放置的物件 |
| 重置演示场景 | 恢复内置的三镜面反射示例 |
| 适合窗口 | 恢复默认视图范围 |
| 光线密度 | 实时调整每个光源生成的光线数量 |
| 打开场景 / 保存场景 | 读写 `.lightdraw.json` 场景文件 |

## 场景文件格式

场景使用 UTF-8 JSON，并通过 `dataVersion` 标记数据结构版本。当前版本为 `2`，并可继续读取版本 `1`：

```json
{
  "dataVersion": 2,
  "scene": {
    "name": "双镜面反射演示",
    "lightSources": [
      {
        "position": { "x": -300, "y": 20 },
        "directionDegrees": -8,
        "spreadDegrees": 38,
        "wavelengthNanometers": 589
      }
    ],
    "mirrors": [
      {
        "start": { "x": 40, "y": -170 },
        "end": { "x": 105, "y": 165 }
      }
    ]
  }
}
```

字段说明：

- `dataVersion`：场景文件格式版本，用于未来的数据迁移；
- `scene.name`：场景显示名称；
- `lightSources[].position`：光源在世界坐标系中的位置；
- `directionDegrees`：中心出射方向，单位为度；
- `spreadDegrees`：扇形发射角度，单位为度；
- `wavelengthNanometers`：波长，单位为纳米；
- `kind`：光源类型，`point` 或 `parallelLine`；线光源还会保存 `end` 端点；
- `mirrors[].start/end`：有限线段镜面的两个端点。
- `lenses[].start/end`：理想薄透镜的两个端点，另含 `kind` 与 `focalLength`。

未来修改格式时应提升 `dataVersion` 并提供显式迁移器，不应静默改变已有字段的含义。

## 技术架构

```text
LightDraw
├─ src/LightDraw.Core
│  ├─ Geometry           向量和几何基础类型
│  ├─ Scene              平台无关的场景模型
│  ├─ Simulation         光线生成、求交和反射
│  └─ Persistence        带版本号的 JSON 场景读写
├─ src/LightDraw.Rendering.Skia
│  └─ OpticalCanvas      Avalonia + SkiaSharp 高性能画布
└─ src/LightDraw.Desktop
   ├─ App                Avalonia 应用入口和主题
   └─ MainWindow         简体中文桌面界面
```

依赖方向保持为：

```text
LightDraw.Core ← LightDraw.Rendering.Skia ← LightDraw.Desktop
```

`LightDraw.Core` 不引用 Avalonia、SkiaSharp、Windows API 或 macOS API，因此可以独立测试，也便于未来复用于命令行工具、WebAssembly 或其他前端。

### 模拟与绘制

`RayTracer` 将场景计算为与 UI 无关的光线线段集合。`OpticalCanvas` 在 Avalonia 的自定义绘制操作中获取当前 SkiaSharp 画布，将线段合并到路径后批量绘制。模拟层和显示层之间只传递数据，以便未来加入后台计算、取消令牌、空间索引和可插拔模拟引擎。

## 设计原则

- **教学优先**：交互和术语应便于课堂演示，而不是堆叠工程软件式参数；
- **计算与界面分离**：核心算法不依赖桌面框架；
- **跨平台一致**：避免无必要的平台专属 API；
- **性能可扩展**：大量光线优先批量计算和批量绘制；
- **格式可迁移**：所有持久化数据都有显式版本；
- **社区共建**：重要行为变更需要测试、文档和清晰的提交说明。

## 路线图

- 增加 xUnit 数值测试、边界用例和可复现的黄金场景；
- 实现对象选择、可拖动控制点、撤销与重做；
- 加入理想透镜、玻璃折射、圆弧镜、棱镜、色散和光栅；
- 引入空间索引（如 BVH）和统一的 `ISimulationEngine`；
- 支持 PNG、SVG、CSV 等导出格式；
- 改进键盘操作、屏幕阅读器信息和高对比度主题；
- 建立 Windows、macOS 和 Linux 的持续集成与发布流程；
- 完善 Microsoft Store 和 Mac App Store 所需的签名、沙盒与资源文件。

路线图不是固定承诺，实际优先级由教学价值、社区反馈和维护成本共同决定。

## 参与贡献

欢迎报告缺陷、提出课堂需求、完善中英文文档、补充测试与无障碍支持、优化性能或实现新的光学元件。开始编码前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。较大的功能建议先创建 Issue，说明使用场景、交互方案和算法依据。

提交贡献即表示你有权提供相关内容，并同意将该贡献按照本仓库当前的 PolyForm Noncommercial License 1.0.0 提供。请勿直接复制许可证不兼容或来源不明的代码、图片、字体、题目和教学材料。

## 许可证与使用边界

项目代码采用 **PolyForm Noncommercial License 1.0.0**，完整条款见 [LICENSE](LICENSE)。简要理解如下：

- 允许个人学习、研究、实验、教学和非商业爱好项目使用；
- 允许教育机构、慈善机构、公共研究机构和政府机构等按许可证规定使用；
- 允许在非商业目的范围内修改和分发，并须同时提供许可证及必要声明；
- 不授权将项目或衍生版本用于商业产品、收费服务、商业交付或预期的商业应用；
- 本摘要仅用于帮助理解，若与 `LICENSE` 正文冲突，以英文许可证正文为准；
- 商业授权或对具体使用方式有疑问时，应先联系版权所有者并取得书面许可。

由于禁止特定商业用途，本项目不符合 [Open Source Initiative 对开源软件的定义](https://opensource.org/osd)，请使用“源码可用”“公开源码”或“社区协作项目”描述本项目，避免标注为 OSI-approved open source。

本项目依赖的第三方组件仍分别遵循其原有许可证，本许可证不会改变第三方组件的授权条款。若未来参考或移植其他项目（包括 `ricktu288/ray-optics`）的代码，必须先完成许可证兼容性检查、保留所需声明并明确记录来源；许可证不兼容的代码不得直接合入。

## 致谢与灵感来源

光绘课堂的创作灵感来自 [Ray Optics Simulation](https://github.com/ricktu288/ray-optics)。该项目提供了功能丰富的二维几何光学场景编辑、模拟与交互式演示，让我们看到了将抽象光学知识转化为直观可视化工具的可能性。

在此特别感谢 `ricktu288/ray-optics` 的作者和所有贡献者长期以来的设计、开发与社区维护工作。

光绘课堂是使用 .NET、Avalonia 和 SkiaSharp 探索跨平台课堂教学体验的独立项目，与 Ray Optics Simulation 不存在官方隶属、合作或背书关系。“受到启发”不代表直接复制其代码；如果未来实际引用、翻译或移植该项目的任何代码或资源，将按其 Apache License 2.0 保留版权、许可证及其他必要声明，并在仓库中明确记录来源和修改内容。

## 免责声明

光绘课堂按许可证规定“按原样”提供，不附带任何明示或默示保证。模拟结果主要用于教学和演示，不构成工程设计、实验安全或专业决策依据。
