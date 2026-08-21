# 拉取最新代码并一键构建
# 用法：powershell -ExecutionPolicy Bypass -File pull.ps1 [-Arch x86|x64] [-Clean]
#
# 说明：
#   - 先 git pull 拉取最新代码（若本地有未提交改动则中止）
#   - 再调用 build.ps1 构建指定架构版本（默认 x86，可运行 Flash 游戏）
param(
    [ValidateSet('x86', 'x64')]
    [string]$Arch = 'x86',
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "==> 拉取最新代码 (git pull)" -ForegroundColor Cyan
$Status = git -C $Root status --porcelain
if ($Status) {
    throw "工作区存在未提交的改动，已中止。请先提交或清理：`n$Status"
}
git -C $Root pull
if ($LASTEXITCODE -ne 0) { throw "git pull 失败（退出码 $LASTEXITCODE）" }

Write-Host "==> 构建版本: $Arch" -ForegroundColor Cyan
& powershell -ExecutionPolicy Bypass -File (Join-Path $Root 'build.ps1') -Arch $Arch $(if ($Clean) { '-Clean' })
