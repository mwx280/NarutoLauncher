# NarutoLauncher 一键构建脚本
# 用法：powershell -ExecutionPolicy Bypass -File build.ps1 [-Arch x86|x64] [-Clean]
#
# 说明：
#   - 构建 CEF 游戏宿主（app/，按 -Arch 选择 x86/x64，MSVC 交叉工具链 vcvarsall）
#   - 构建 WPF 启动器（NarutoLauncher/，架构与 -Arch 一致，默认 win-x64）
#   - 把 GameHost 输出复制到启动器输出目录的 GameHost/
#   - 首次构建前需运行 tools/download_deps.ps1 -Arch x86/x64 准备 third_party/ 依赖
#
# 注意：PPAPI Flash 插件（pepflashplayer.dll）只有 32 位版本，x64 构建无法运行 Flash 游戏。

param(
    [ValidateSet('x86', 'x64')]
    [string]$Arch = 'x86',
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Vcvars = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvarsall.bat'
$Ninja = 'C:\Users\xiaowu\Dev\Qt\Tools\Ninja\ninja.exe'
$Cmake = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'

# 架构映射：CMake/vcvarsall 用 x86|x64；WPF 用 win-x86|win-x64 与 PlatformTarget
if ($Arch -eq 'x86') {
    $VcArch = 'x86'
    $Rid = 'win-x86'
    $PlatformTarget = 'x86'
    $BuildDir = 'build\app'
} else {
    $VcArch = 'x64'
    $Rid = 'win-x64'
    $PlatformTarget = 'x64'
    $BuildDir = 'build\app_x64'
}

if (-not (Test-Path $Vcvars)) { throw "未找到 vcvarsall.bat: $Vcvars" }
if (-not (Test-Path $Ninja)) { throw "未找到 ninja.exe: $Ninja" }
if (-not (Test-Path $Cmake)) { throw "未找到 cmake.exe: $Cmake" }

function Invoke-Step {
    param([string]$Name, [scriptblock]$Block)
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Block
    if ($LASTEXITCODE -ne 0) { throw "步骤失败: $Name (退出码 $LASTEXITCODE)" }
}

Write-Host "==> 目标架构: $Arch (vcvarsall=$VcArch, WPF RID=$Rid, 构建目录=$BuildDir)" -ForegroundColor Yellow

# ---- 1. 检查依赖是否已下载 ----
if (-not (Test-Path (Join-Path $Root 'third_party\cef_sdk\CEF\include\cef_version.h'))) {
    Write-Host "[!] 未找到 CEF SDK，先运行依赖下载脚本。" -ForegroundColor Yellow
    Invoke-Step '下载依赖 tools/download_deps.ps1' {
        & powershell -ExecutionPolicy Bypass -File (Join-Path $Root 'tools\download_deps.ps1') -RootDir $Root -Arch $Arch
    }
}

# ---- 2. 构建 CEF 游戏宿主 ----
if ($Clean -and (Test-Path (Join-Path $Root $BuildDir))) {
    Write-Host "[!] -Clean 已指定，清理 $BuildDir。" -ForegroundColor Yellow
    Remove-Item (Join-Path $Root $BuildDir) -Recurse -Force
}
Invoke-Step "配置 CMake（app -> $BuildDir，Ninja $Arch）" {
    & cmd /c "call `"$Vcvars`" $VcArch && `"$Cmake`" -S `"$Root\app`" -B `"$Root\$BuildDir`" -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_MAKE_PROGRAM=`"$Ninja`""
}
Invoke-Step "构建 GameHost（ninja $BuildDir）" {
    & cmd /c "call `"$Vcvars`" $VcArch && `"$Ninja`" -C `"$Root\$BuildDir`""
}

# ---- 3. 构建 WPF 启动器（架构与 GameHost 一致），并把 GameHost 输出复制到启动器输出目录的 GameHost/ ----
Invoke-Step "构建 WPF 启动器（dotnet build，RID=$Rid）" {
    & dotnet build (Join-Path $Root 'NarutoLauncher\NarutoLauncher.csproj') -c Release -p:RuntimeIdentifier=$Rid -p:PlatformTarget=$PlatformTarget
}
$OutBase = Join-Path $Root "NarutoLauncher\bin\Release\net10.0-windows\$Rid"
$GameHostSrc = Join-Path $Root "$BuildDir\huoyin_launcher.exe"
$GameHostDir = Join-Path $OutBase 'GameHost'
if (-not (Test-Path $GameHostSrc)) {
    throw "未找到 GameHost 输出: $GameHostSrc"
}
Invoke-Step '复制 GameHost 到启动器输出目录 GameHost/' {
    New-Item -ItemType Directory -Force -Path $GameHostDir | Out-Null
    $GameHostSrcDir = Split-Path $GameHostSrc
    $Skip = @('CMakeFiles', 'cmake_install.cmake', 'CMakeCache.txt',
              'build.ninja', '.ninja_deps', '.ninja_log', 'debug.log')
    Get-ChildItem -LiteralPath $GameHostSrcDir -Force | Where-Object {
        $_.Name -notin $Skip
    } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $GameHostDir -Recurse -Force
    }
}
Write-Host "构建完成：$GameHostDir" -ForegroundColor Green

if ($Arch -eq 'x64') {
    Write-Host "[!] 注意：x64 构建的 GameHost 无法加载 Flash 插件（pepflashplayer 仅有 32 位），游戏不可运行。" -ForegroundColor Red
}

Write-Host "全部完成。" -ForegroundColor Green
