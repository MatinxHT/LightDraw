# 本地调试

在 VS Code 的“运行和调试”中选择 `Debug LightDraw Browser`，再按 F5。
后台任务先构建并启动 `http://127.0.0.1:8765/`，服务就绪后启动 Chrome；停止调试时关闭该项目的本地服务。

浏览器配置使用 `blazorwasm` 的 `attach`，因为服务已由 `preLaunchTask` 启动；改成 `launch` 会让 C# 扩展再次启动服务。
`browserConfig.runtimeArgs` 中也传入页面地址，使 Chrome 启动时直接打开应用，不依赖 WebAssembly 调试桥接完成后才从 `about:blank` 导航。修改端口时，需同步修改 `url`、`runtimeArgs`、`tasks.json` 的地址及就绪匹配，以及 `stop-browser.sh`。

`dotnet-browser.sh` 优先使用 `~/.local/share/lightdraw-dotnet/dotnet`，否则使用 PATH 中的 .NET；所用 SDK 需安装 `wasm-tools`。C# 断点仍依赖 VS Code C# 扩展的 WebAssembly 调试支持。

浏览器工具图标优先使用 `PathIcon` 矢量路径，不依赖系统字体回退。中文字体已随应用打包，但不包含所有 Unicode 符号。

Chrome 使用项目独立的 `.vscode/.browser-profile`（已忽略），停止调试时关闭整个调试浏览器并定向清理残留。若扩展启动失败而未执行停止钩子，可运行 VS Code 任务 `stop LightDraw Browser`。停止前请保存课堂编辑成果。迁移前使用 VS Code 默认配置目录启动的旧调试窗口，请保存后手动退出一次；清理脚本不会关闭默认目录中的浏览器。
