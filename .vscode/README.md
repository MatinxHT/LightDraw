# 本地调试

在 VS Code 的“运行和调试”中选择 `Debug LightDraw Browser`，再按 F5。
预启动任务先编译浏览器项目。编译成功后，用 `--no-build` 和 `LightDraw.Browser` 启动配置运行本地服务。服务输出调试地址后，VS Code 才启动 Chrome。调试结束后，停止任务会关闭本地服务。

浏览器配置使用 `blazorwasm` 的 `attach`，由 C# 扩展通过 .NET SDK 的 WebAssembly 调试代理连接。Debug 编译禁用构建时资源压缩，避免旧的 gzip 启动脚本引用已不存在的 WASM 文件。修改端口时，需同步修改浏览器项目的 `Properties/launchSettings.json`、`launch.json`、`tasks.json` 的就绪匹配和停止脚本。

Debug 构建生成独立的可移植 PDB。Mono WebAssembly 通过这些文件启用 C# 断点；Release 仍使用嵌入式调试符号。

Windows 上的任务使用 PATH 中的 `dotnet`。macOS/Linux 上的 `dotnet-browser.sh` 优先使用 `~/.local/share/lightdraw-dotnet/dotnet`，其次使用 PATH 中的 `dotnet`。为实际调用的 `dotnet` 安装 `global.json` 指定的 .NET SDK 11 和 `wasm-tools` 工作负载。C# 断点依赖 VS Code C# 扩展的 WebAssembly 调试支持。

浏览器工具图标优先使用 `PathIcon` 矢量路径，不依赖系统字体回退。中文字体已随应用打包，但不包含所有 Unicode 符号。

Chrome 使用项目独立的 `.vscode/.browser-profile`（已忽略）。如果上一次调试留下了本地服务，可运行 `stop LightDraw Browser` 手动关闭。Windows 使用 `stop-browser.ps1`，其他平台使用 `stop-browser.sh`。
