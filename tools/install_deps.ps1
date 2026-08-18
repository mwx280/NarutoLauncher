# =============================================================================
# 自动安装 CEF 87 构建所需依赖（适用于干净 Windows 11 / 云主机）
#
# 安装项：
#   1. Git for Windows
#   2. Python 3.x（真版，替换 Windows Store 假壳）
#   3. Visual Studio 2022 Build Tools（含 C++ x86/x64 组件，供 Chromium 编译）
#   4. depot_tools（由 build_cef_flash.ps1 自动拉取，本脚本仅确认/提示）
#
# 用法（以管理员身份运行 PowerShell）：
#   powershell -ExecutionPolicy Bypass -File tools/install_deps.ps1
#
# 注意：VS Build Tools 体积较大（约 3-6GB）；安装后需重新打开 PowerShell。
# =============================================================================

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Write-Step([string]$m) { Write-Host "`n==== $m ====" -ForegroundColor Cyan }
function Ok([string]$m)        { Write-Host "[OK] $m" -ForegroundColor Green }
function Warn([string]$m)      { Write-Host "[!] $m" -ForegroundColor Yellow }

# 刷新 PATH（从注册表读取系统 + 用户 PATH 并合并）
function Update-Path {
    $machine = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $user    = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$machine;$user"
}

# 安装一项 winget 包，若已存在则跳过
function Install-WingetPackage([string]$Id, [string]$DisplayName) {
    # 用 winget 查询是否已安装
    $q = & winget list --id $Id --accept-source-agreements 2>$null
    if ($LASTEXITCODE -eq 0 -and ($q | Select-String $Id)) {
        Ok "$DisplayName 已安装，跳过。"
        return $true
    }
    Write-Host "正在安装 $DisplayName ..."
    & winget install --id $Id -e --accept-source-agreements --accept-package-agreements --silent
    if ($LASTEXITCODE -ne 0) {
        Warn "$DisplayName 安装返回码 $LASTEXITCODE，可能需手动安装。"
        return $false
    }
    Ok "$DisplayName 安装完成。"
    return $true
}

# ---------------------------------------------------------------------------
# 0. 检查 winget
# ---------------------------------------------------------------------------
Write-Step '0/5 检查 winget'
$winget = Get-Command winget -ErrorAction SilentlyContinue
if (-not $winget) {
    throw '未找到 winget。请在 Windows 11 / 较新的 Windows 10 上运行，或先安装 App Installer。'
}
Ok "winget: $($winget.Source)"
Update-Path

# ---------------------------------------------------------------------------
# 1. 安装 Git
# ---------------------------------------------------------------------------
Write-Step '1/5 安装 Git'
$gitOk = Get-Command git -ErrorAction SilentlyContinue
if (-not $gitOk) {
    Install-WingetPackage 'Git.Git' 'Git for Windows' | Out-Null
    Update-Path
}
$gitOk = Get-Command git -ErrorAction SilentlyContinue
if ($gitOk) { Ok "git: $(git --version)" }
else { Warn 'git 安装后仍不可用，请重新打开 PowerShell 或手动安装。' }

# ---------------------------------------------------------------------------
# 2. 安装真版 Python（覆盖 Store 假壳）
# ---------------------------------------------------------------------------
Write-Step '2/5 安装 Python'
# 当前 python.exe 可能是 Windows Store 假壳（运行无输出）。先测真伪。
$pythonReal = $false
$py = Get-Command python -ErrorAction SilentlyContinue
if ($py) {
    $v = & python --version 2>&1
    if ($v -match 'Python 3') { $pythonReal = $true; Ok "Python 已可用: $v" }
    else { Warn '当前 python 是 Windows Store 假壳（无版本输出），需安装真版。' }
}
if (-not $pythonReal) {
    Install-WingetPackage 'Python.Python.3.12' 'Python 3.12' | Out-Null
    Update-Path
    $py = Get-Command python -ErrorAction SilentlyContinue
    if ($py) {
        $v = & python --version 2>&1
        if ($v -match 'Python 3') { $pythonReal = $true; Ok "Python 已可用: $v" }
        else { Warn 'python 命令仍指向假壳，请手动把真版 Python 加入 PATH（C:\Users\<user>\AppData\Local\Programs\Python\...）' }
    }
}
if (-not $pythonReal) { Warn '未能确认真版 Python，构建可能失败。' }

# ---------------------------------------------------------------------------
# 3. 安装 Visual Studio 2022 Build Tools + C++ 组件
# ---------------------------------------------------------------------------
Write-Step '3/5 安装 Visual Studio Build Tools（C++ 组件，体积较大）'
$vsWhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$vsFound = $false
if (Test-Path $vsWhere) {
    $vs = & $vsWhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 2>$null
    if ($vs) { $vsFound = $true; Ok "已找到 Visual Studio: $($vs.displayName)" }
}
if (-not $vsFound) {
    $btOk = Install-WingetPackage 'Microsoft.VisualStudio.2022.BuildTools' 'VS 2022 Build Tools'
    if ($btOk) {
        Write-Host '正在安装 C++ x86/x64 构建工具组件（workload 配置）...'
        # 定位 vs_installer 并添加 C++ 桌面组件
        $installer = Get-ChildItem 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\setup.exe' -ErrorAction SilentlyContinue
        if ($installer) {
            & $installer modify --installPath 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools' `
                --add Microsoft.VisualStudio.Workload.VCTools `
                --add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
                --add Microsoft.VisualStudio.Component.Windows10SDK.19041 `
                --quiet --norestart
            if ($LASTEXITCODE -ne 0) { Warn "VS 组件安装返回码 $LASTEXITCODE" }
            else { Ok 'VS C++ 组件安装完成。' }
        } else {
            Warn '未找到 VS setup.exe，请打开 Visual Studio Installer 手动添加 C++ 工作负载。'
        }
    } else {
        Warn 'VS Build Tools 安装未成功，请手动安装。'
    }
}

# 再次确认 VS
$vsFound = $false
if (Test-Path $vsWhere) {
    $vs = & $vsWhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 2>$null
    if ($vs) { $vsFound = $true; Ok "VS 确认可用: $($vs.displayName)" }
}
if (-not $vsFound) {
    Warn '未能确认 VS C++ 工具链。CEF 87 构建机用 VS2019(v142)；若用 VS2022(v143) 编译 Chromium 87 失败，'
    Warn '请用 Visual Studio Installer 添加"MSVC v142 生成工具"组件。'
}

# ---------------------------------------------------------------------------
# 4. 检查/提示 depot_tools
# ---------------------------------------------------------------------------
Write-Step '4/5 检查 depot_tools'
$depot = Join-Path $DownloadDir 'depot_tools\depot_tools.bat'
if (Test-Path $depot) { Ok "depot_tools 已就绪: $depot" }
else { Warn "depot_tools 将在此目录自动创建: $DownloadDir（由 build_cef_flash.ps1 自动拉取）" }

# ---------------------------------------------------------------------------
# 5. 汇总
# ---------------------------------------------------------------------------
Write-Step '5/5 汇总'
Update-Path
Write-Host "git:      $((Get-Command git -ErrorAction SilentlyContinue).Source)"
Write-Host "python:   $(if($pythonReal){ (Get-Command python -ErrorAction SilentlyContinue).Source } else { '未确认（假壳）' })"
Write-Host "winget:   $((Get-Command winget -ErrorAction SilentlyContinue).Source)"
Write-Host "depot:    $depot"
Write-Host ''
Write-Host '==== 完成 ====' -ForegroundColor Green
Write-Host '请【关闭并重新打开】PowerShell 使 PATH 生效，然后运行：'
Write-Host '  powershell -ExecutionPolicy Bypass -File tools/build_cef_flash.ps1'
Write-Host ''
Write-Host '注意：'
Write-Host ' 1. 若 VS2022(v143) 编译 Chromium 87 报错，请添加 MSVC v142 工具集组件后重试。'
Write-Host ' 2. 本机在中国大陆拉取 Chromium 源码（googlesource/bitbucket）可能很慢，请耐心等待。'
