# 下载第三方依赖（CEF 87 运行时 + CEF 87 头文件/包装器源码）
# 用法：powershell -ExecutionPolicy Bypass -File tools/download_deps.ps1
#
# 背景：CEF 官方下载站已下线 87.1.13 的旧版本 tar.bz2，因此：
#   - CEF 87 运行时（libcef.dll 等）取自 CefSharp 发布的 cef.redist.x86/87.1.13 NuGet 包
#     （官方分发渠道，与 CEF 87.1.13 完全一致，含 locales、swiftshader、ICUDT 等完整运行时）
#   - CEF 87 头文件与 libcef_dll_wrapper 源码取自 chromiumembedded/cef 官方仓库对应提交
#     （commit 481a82af "Update to Chromium version 87.0.4280.141"，与运行时版本精确匹配）

param(
    [string]$RootDir = (Resolve-Path (Join-Path $PSScriptRoot '..'))
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$ThirdParty = Join-Path $RootDir 'third_party'
$Tmp = Join-Path $env:TEMP ('cef_deps_' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $ThirdParty, $Tmp | Out-Null

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

# ---- 1. CEF 87 运行时（x86）----
Write-Step '下载 CEF 87.1.13 运行时 (cef.redist.x86 nuget)'
$nupkg = Join-Path $Tmp 'cef.redist.x86.87.1.13.nupkg'
Invoke-WebRequest -Uri 'https://api.nuget.org/v3-flatcontainer/cef.redist.x86/87.1.13/cef.redist.x86.87.1.13.nupkg' -OutFile $nupkg -UseBasicParsing

$runtimeDir = Join-Path $ThirdParty 'cef_runtime'
if (Test-Path $runtimeDir) { Remove-Item $runtimeDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $runtimeDir | Out-Null
$nupkgZip = [IO.Path]::ChangeExtension($nupkg, '.zip')
Move-Item -LiteralPath $nupkg -Destination $nupkgZip -Force
Expand-Archive -LiteralPath $nupkgZip -DestinationPath (Join-Path $Tmp 'runtime_pkg') -Force
Copy-Item -Path (Join-Path $Tmp 'runtime_pkg\CEF\*') -Destination $runtimeDir -Recurse -Force
Write-Host "  运行时已解压到: $runtimeDir"

# ---- 2. CEF 87 SDK（头文件 + libcef_dll 包装器 + CMake + tools）----
# 取自 CefSharp 分发的 cef.sdk/87.1.13 NuGet（官方 CEF 仓库对应提交的 SDK 产物，含 include、
# libcef_dll、cmake、tools 等，与运行时版本精确匹配；走 NuGet 避免 GitHub 下载限流）。
Write-Step '下载 CEF 87.1.13 SDK (cef.sdk nuget)'
$sdkNupkg = Join-Path $Tmp 'cef.sdk.87.1.13.nupkg'
Invoke-WebRequest -Uri 'https://api.nuget.org/v3-flatcontainer/cef.sdk/87.1.13/cef.sdk.87.1.13.nupkg' -OutFile $sdkNupkg -UseBasicParsing
$sdkNupkgZip = [IO.Path]::ChangeExtension($sdkNupkg, '.zip')
Move-Item -LiteralPath $sdkNupkg -Destination $sdkNupkgZip -Force
Expand-Archive -LiteralPath $sdkNupkgZip -DestinationPath (Join-Path $Tmp 'sdk_pkg') -Force

$sdkDir = Join-Path $ThirdParty 'cef_sdk'
if (Test-Path $sdkDir) { Remove-Item $sdkDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $sdkDir | Out-Null
Copy-Item -Path (Join-Path $Tmp 'sdk_pkg\*') -Destination $sdkDir -Recurse -Force
Write-Host "  SDK 已就位: $sdkDir"

Remove-Item $Tmp -Recurse -Force
Write-Step '完成'
Write-Host "third_party 内容:"
Get-ChildItem $ThirdParty | Select-Object Name
