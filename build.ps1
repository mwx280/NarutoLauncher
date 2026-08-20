# NarutoLauncher 一键构建脚本
# 用法：powershell -ExecutionPolicy Bypass -File build.ps1 [-Clean]
#
# 说明：
#   - 构建 CEF 游戏宿主（app/，x86，MSVC 交叉工具链 vcvarsall）
#   - 构建 WPF 启动器（NarutoLauncher/，x64）并把 GameHost 复制到输出目录的 GameHost/
#   - 首次构建前需运行 tools/download_deps.ps1 准备 third_party/ 依赖

param(
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Vcvars = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvarsall.bat'
$Ninja = 'C:\Users\xiaowu\Dev\Qt\Tools\Ninja\ninja.exe'
$Cmake = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
$Arch = 'x86'

if (-not (Test-Path $Vcvars)) { throw "未找到 vcvarsall.bat: $Vcvars" }
if (-not (Test-Path $Ninja)) { throw "未找到 ninja.exe: $Ninja" }
if (-not (Test-Path $Cmake)) { throw "未找到 cmake.exe: $Cmake" }

function Invoke-Step {
    param([string]$Name, [scriptblock]$Block)
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Block
    if ($LASTEXITCODE -ne 0) { throw "步骤失败: $Name (退出码 $LASTEXITCODE)" }
}

# ---- 1. 检查依赖是否已下载 ----
if (-not (Test-Path (Join-Path $Root 'third_party\cef_sdk\CEF\include\cef_version.h'))) {
    Write-Host "[!] 未找到 CEF SDK，先运行依赖下载脚本。" -ForegroundColor Yellow
    Invoke-Step '下载依赖 tools/download_deps.ps1' {
        & powershell -ExecutionPolicy Bypass -File (Join-Path $Root 'tools\download_deps.ps1') -RootDir $Root
    }
}

# ---- 2. 构建 CEF 游戏宿主（x86） ----
if ($Clean -and (Test-Path (Join-Path $Root 'build\app'))) {
    Write-Host "[!] -Clean 已指定，清理 build/app。" -ForegroundColor Yellow
    Remove-Item (Join-Path $Root 'build\app') -Recurse -Force
}
Invoke-Step '配置 CMake（app -> build/app，Ninja x86）' {
    & cmd /c "call `"$Vcvars`" $Arch && `"$Cmake`" -S `"$Root\app`" -B `"$Root\build\app`" -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_MAKE_PROGRAM=`"$Ninja`""
}
Invoke-Step '构建 GameHost（ninja build/app）' {
    & cmd /c "call `"$Vcvars`" $Arch && `"$Ninja`" -C `"$Root\build\app`""
}

# ---- 3. 构建 WPF 启动器（x64），并把 GameHost 输出复制到启动器输出目录的 GameHost/ ----
Invoke-Step '构建 WPF 启动器（dotnet build）' {
    & dotnet build (Join-Path $Root 'NarutoLauncher\NarutoLauncher.csproj') -c Release
}
$OutBase = Join-Path $Root 'NarutoLauncher\bin\Release\net10.0-windows\win-x64'
$GameHostSrc = Join-Path $Root 'build\app\huoyin_launcher.exe'
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

Write-Host "全部完成。" -ForegroundColor Green