$ErrorActionPreference = 'Stop'
$projectDirectory = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$runtimeConfig = Join-Path $projectDirectory 'src\LightDraw.Browser\bin\Debug\net11.0-browser\LightDraw.Browser.runtimeconfig.json'
$profileDirectory = Join-Path $PSScriptRoot '.browser-profile'

# Only stop the LightDraw host listening on this workspace's debugging port.
$listeners = @(Get-NetTCPConnection -LocalPort 8765 -State Listen -ErrorAction SilentlyContinue)
foreach ($listener in $listeners) {
    $process = Get-CimInstance Win32_Process -Filter "ProcessId = $($listener.OwningProcess)" -ErrorAction SilentlyContinue
    if ($process -and $process.CommandLine -and
        $process.CommandLine.IndexOf('WasmAppHost.dll', [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -and
        $process.CommandLine.IndexOf($runtimeConfig, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Stop-Process -Id $process.ProcessId -ErrorAction SilentlyContinue
    }
}

# The JavaScript debugger normally closes its browser; clean up only this workspace's dedicated profile.
$profilePattern = '(?:^|\s)--user-data-dir=(?:"' + [regex]::Escape($profileDirectory) + '"|' + [regex]::Escape($profileDirectory) + ')(?=\s|$)'
Get-CimInstance Win32_Process -Filter "Name = 'chrome.exe'" | ForEach-Object {
    $commandLine = $_.CommandLine
    if ($commandLine -and $commandLine -match $profilePattern) {
        Stop-Process -Id $_.ProcessId -ErrorAction SilentlyContinue
    }
}
