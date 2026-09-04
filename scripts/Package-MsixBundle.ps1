[CmdletBinding()]
param(
    [string]$Version = "0.6.1",
    [string]$Publisher = "CN=B535E105-079B-46A7-8878-0A2D1347C541"
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must use major.minor.patch format, for example 0.6.1."
}

$packageVersion = "$Version.0"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "src\LightDraw.Desktop\LightDraw.Desktop.csproj"
$outputRoot = Join-Path $repositoryRoot "artifacts\msix\$Version"
$bundleInputRoot = Join-Path $outputRoot "bundle-input"
$makeAppx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"

if (-not (Test-Path -LiteralPath $makeAppx)) {
    throw "MakeAppx was not found at '$makeAppx'. Install the Windows 10/11 SDK packaging tools."
}

New-Item -ItemType Directory -Force -Path $outputRoot, $bundleInputRoot | Out-Null

$architectures = @(
    @{ RuntimeIdentifier = "win-x64"; ProcessorArchitecture = "x64" },
    @{ RuntimeIdentifier = "win-arm64"; ProcessorArchitecture = "arm64" }
)

foreach ($architecture in $architectures) {
    $runtimeIdentifier = $architecture.RuntimeIdentifier
    $layoutDirectory = Join-Path $outputRoot "$runtimeIdentifier\layout"
    $packageName = "MartinHungChiho.LightDraw-$Version-$runtimeIdentifier.msix"
    $packagePath = Join-Path $bundleInputRoot $packageName

    dotnet publish $projectPath --configuration Release --runtime $runtimeIdentifier --self-contained true --output $layoutDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed for $runtimeIdentifier."
    }

    $assetDirectory = Join-Path $layoutDirectory "Assets"
    New-Item -ItemType Directory -Force -Path $assetDirectory | Out-Null
    $iconSource = Join-Path $repositoryRoot "src\LightDraw.Desktop\Assets\Icons\LightDraw-256.png"
    Copy-Item $iconSource (Join-Path $assetDirectory "Square44x44Logo.png")
    Copy-Item $iconSource (Join-Path $assetDirectory "Square150x150Logo.png")
    Copy-Item $iconSource (Join-Path $assetDirectory "StoreLogo.png")

    $manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap uap10 rescap">
  <Identity Name="MartinHungChiho.LightDraw" Publisher="$Publisher" Version="$packageVersion" ProcessorArchitecture="$($architecture.ProcessorArchitecture)" />
  <Properties>
    <DisplayName>&#x5149;&#x7ED8;&#x8BFE;&#x5802;LightDraw</DisplayName>
    <PublisherDisplayName>Martin Hung Chiho</PublisherDisplayName>
    <Description>LightDraw classroom application for optics, electrostatics, and magnetostatics.</Description>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Resources>
    <Resource Language="zh-CN" />
    <Resource Language="en-US" />
  </Resources>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>
  <Applications>
    <Application Id="LightDraw" Executable="LightDraw.exe" uap10:RuntimeBehavior="packagedClassicApp" uap10:TrustLevel="mediumIL">
      <uap:VisualElements DisplayName="LightDraw" Description="LightDraw classroom application for optics, electrostatics, and magnetostatics." BackgroundColor="transparent" Square44x44Logo="Assets\Square44x44Logo.png" Square150x150Logo="Assets\Square150x150Logo.png" />
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@
    Set-Content -LiteralPath (Join-Path $layoutDirectory "AppxManifest.xml") -Value $manifest -Encoding UTF8

    & $makeAppx pack /d $layoutDirectory /p $packagePath /o
    if ($LASTEXITCODE -ne 0) {
        throw "MSIX packaging failed for $runtimeIdentifier."
    }
}

$bundlePath = Join-Path $outputRoot "MartinHungChiho.LightDraw-$Version.msixbundle"
& $makeAppx bundle /d $bundleInputRoot /p $bundlePath /bv $packageVersion /o
if ($LASTEXITCODE -ne 0) {
    throw "MSIXBundle creation failed."
}

Write-Host "Created $bundlePath"
