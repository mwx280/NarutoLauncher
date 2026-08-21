# 构建全 x86 版本（WPF x86 + GameHost x86，可运行 Flash 游戏）
# 用法：powershell -ExecutionPolicy Bypass -File tools/build-x86.ps1 [-Clean]
param([switch]$Clean)
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
& powershell -ExecutionPolicy Bypass -File (Join-Path $Root 'build.ps1') -Arch x86 $(if ($Clean) { '-Clean' })
