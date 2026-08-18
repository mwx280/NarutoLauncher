# =============================================================================
# 自动安装 CEF 87 构建所需依赖（适用于无 winget 的干净 Windows）
#
# 安装项（全部用官方直链下载安装器，不依赖 winget）：
#   1. Git for Windows        https://github.com/git-for-windows/git/releases
#   2. Python 3.12.10          https://www.python.org/ftp/python/
#   3. VS 2022 Build Tools     https://aka.ms/vs/17/release/vs_BuildTools.exe
#      （含 C++ x86/x64 组件 + Windows SDK）
#   4. depot_tools            由 build_cef_flash.ps1 自动拉取
#
# 用法（管理员 PowerShell）：
#   powershell -ExecutionPolicy Bypass -File tools/install_deps.ps1
#
# 说明：
#   - 下载安装器会存到本目录 ./deps/ 方便复用
#   - VS Build Tools 体积大（约 3-6GB），安装耗时较长
#   - 安装完成后需重新打开 PowerShell
# =============================================================================

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# 脚本所在目录
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$DepsDir   = Join-Path $ScriptDir 'deps'
New-Item -ItemType Directory -Force -Path $DepsDir | Out-Null

function Write-Step([string]$m) { Write-Host "`n==== $m ====" -ForegroundColor Cyan }
function Ok([string]$m)         { Write-Host "[OK] $m" -ForegroundColor Green }
function Warn([string]$m)       { Write-Host "[!] $m" -ForegroundColor Yellow }

function Update-Path {
    $machine = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $user    = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$machine;$user"
}

# 下载文件（带重试）
function Download-File([string]$Url, [string]$OutFile) {
    if ((Test-Path -LiteralPath $OutFile) -and ((Get-Item -LiteralPath $OutFile).Length -gt 1MB)) {
        Ok "已存在: $OutFile"
        return
    }
    Write-Host "下载: $Url"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    for ($i = 1; $i -le 3; $i++) {
        try {
            Invoke-WebRequest -Uri $Url -OutFile $OutFile -UseBasicParsing -TimeoutSec 300
            Ok "下载完成: $OutFile"
            return
        } catch {
            Warn "第 $i 次下载失败: $($_.Exception.Message)"
            if ($i -eq 3) { throw "下载失败: $Url" }
            Start-Sleep -Seconds 5
        }
    }
}

# ---------------------------------------------------------------------------
# 0. 说明
# ---------------------------------------------------------------------------
Write-Step '0/5 开始安装依赖'
Write-Host "下载目录: $DepsDir"

# ---------------------------------------------------------------------------
# 1. Git
# ---------------------------------------------------------------------------
Write-Step '1/5 Git'
Update-Path
$gitOk = Get-Command git -ErrorAction SilentlyContinue
if ($gitOk) { Ok "git 已存在: $(git --version)" }
else {
    $gitUrl = 'https://github.com/git-for-windows/git/releases/download/v2.55.0.windows.4/Git-2.55.0.4-64-bit.exe'
    $gitExe = Join-Path $DepsDir 'Git-64-bit.exe'
    Download-File $gitUrl $gitExe
    Write-Host '安装 Git（静默）...'
    & $gitExe /VERYSILENT /NORESTART /SP- /NOCANCEL
    Update-Path
    $gitOk = Get-Command git -ErrorAction SilentlyContinue
    if ($gitOk) { Ok "git: $(git --version)" }
    else { Warn 'git 安装后未立即可用，请重开 PowerShell 或手动装。' }
}

# ---------------------------------------------------------------------------
# 2. Python
# ---------------------------------------------------------------------------
Write-Step '2/5 Python'
Update-Path
$pythonReal = $false
$py = Get-Command python -ErrorAction SilentlyContinue
if ($py) {
    $v = & python --version 2>&1
    if ($v -match 'Python 3') { $pythonReal = $true; Ok "Python 已可用: $v" }
    else { Warn '当前 python 是 Windows Store 假壳（无版本输出）。' }
}
if (-not $pythonReal) {
    $pyUrl = 'https://www.python.org/ftp/python/3.12.10/python-3.12.10-amd64.exe'
    $pyExe = Join-Path $DepsDir 'python-3.12.10-amd64.exe'
    Download-File $pyUrl $pyExe
    Write-Host '安装 Python 3.12.10（静默，加入 PATH）...'
    # InstallAllUsers=1 需要管理员；InstallAllUsers=0 装到当前用户
    & $pyExe /quiet InstallAllUsers=1 PrependPath=1 Include_test=0
    Update-Path
    $py = Get-Command python -ErrorAction SilentlyContinue
    if ($py) {
        $v = & python --version 2>&1
        if ($v -match 'Python 3') { $pythonReal = $true; Ok "Python 已可用: $v" }
        else {
            Warn 'python 仍指向假壳，尝试当前用户安装路径...'
            $cand = Join-Path $env:LOCALAPPDATA 'Programs\Python\Python312\python.exe'
            if (Test-Path $cand) { Ok "手动发现 Python: $cand"; $pythonReal = $true; & $cand --version }
        }
    }
}
if (-not $pythonReal) { Warn '未能确认真版 Python，构建可能失败。' }

# ---------------------------------------------------------------------------
# 3. Visual Studio 2022 Build Tools + C++ 组件
# ---------------------------------------------------------------------------
Write-Step '3/5 Visual Studio Build Tools（体积大，较久）'
$vsWhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$vsFound = $false
if (Test-Path $vsWhere) {
    $vs = & $vsWhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 2>$null
    if ($vs) { $vsFound = $true; Ok "已找到 Visual Studio: $($vs.displayName)" }
}
if (-not $vsFound) {
    $btUrl = 'https://aka.ms/vs/17/release/vs_BuildTools.exe'
    $btExe = Join-Path $DepsDir 'vs_BuildTools.exe'
    Download-File $btUrl $btExe
    Write-Host '安装 VS Build Tools + C++ 组件（静默，含 v142 以兼容 CEF 87）...'
    # 同时添加 v142（CEF 87 官方用 v142）+ v143 + Windows SDK
    $args = @(
        '--quiet','--norestart','--wait',
        '--add','Microsoft.VisualStudio.Workload.VCTools',
        '--add','Microsoft.VisualStudio.Component.VC.Tools.x86.x64',   # v143
        '--add','Microsoft.VisualStudio.Component.VC.v142.x86.x64',    # v142 (CEF 87 兼容)
        '--add','Microsoft.VisualStudio.Component.Windows10SDK.19041'
    )
    $p = Start-Process -FilePath $btExe -ArgumentList $args -Wait -PassThru
    if ($p.ExitCode -eq 0) { Ok 'VS Build Tools 安装完成。' }
    else { Warn "VS 安装退出码 $($p.ExitCode)（0 或 3010 表示成功，其余请查看日志）" }
}

$vsFound = $false
if (Test-Path $vsWhere) {
    $vs = & $vsWhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 2>$null
    if ($vs) { $vsFound = $true; Ok "VS 确认可用: $($vs.displayName)" }
}
if (-not $vsFound) {
    Warn '未能确认 VS C++ 工具链。若 v143 编译 Chromium 87 失败，请用 VS Installer 确认已含 v142 组件。'
}

# ---------------------------------------------------------------------------
# 4. depot_tools 提示
# ---------------------------------------------------------------------------
Write-Step '4/5 depot_tools'
$downloadDir = 'C:\cef-src'
$depot = Join-Path $downloadDir 'depot_tools\depot_tools.bat'
if (Test-Path $depot) { Ok "depot_tools 已就绪: $depot" }
else { Warn "depot_tools 将由 build_cef_flash.ps1 自动创建于 $downloadDir" }

# ---------------------------------------------------------------------------
# 5. 汇总
# ---------------------------------------------------------------------------
Write-Step '5/5 汇总'
Update-Path
$gitPath = (Get-Command git -ErrorAction SilentlyContinue).Source
$pyPath  = (Get-Command python -ErrorAction SilentlyContinue).Source
Write-Host "git:    $gitPath"
Write-Host "python: $pyPath  (真实可用: $pythonReal)"
Write-Host ''
Write-Host '==== 完成 ====' -ForegroundColor Green
Write-Host '请【关闭并重新打开】PowerShell 使 PATH 生效，然后运行：'
Write-Host '  powershell -ExecutionPolicy Bypass -File tools/build_cef_flash.ps1'
Write-Host ''
Write-Host '注意：'
Write-Host ' 1. 本机在中国大陆，从 googlesource/bitbucket 拉取 Chromium（约 25GB）可能很慢。'
Write-Host '    若卡住/失败，请把输出发回，我们有镜像加速方案备用。'
Write-Host ' 2. VS 组件已含 v142（兼容 CEF 87）。若仍编译报错请反馈。'
