# 构建全 x64 版本（WPF x64 + GameHost x64）
# 用法：powershell -ExecutionPolicy Bypass -File tools/build-x64.ps1 [-Clean]
param([switch]$Clean)
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
& powershell -ExecutionPolicy Bypass -File (Join-Path $Root 'build.ps1') -Arch x64 $(if ($Clean) { '-Clean' })
