# 构建全 x64 版本（WPF x64 + GameHost x64）
# 用法：powershell -ExecutionPolicy Bypass -File build-x64.ps1 [-Clean]
#
# 注意：x64 构建的 GameHost 无法加载 Flash 插件（pepflashplayer 仅有 32 位），游戏不可运行。
param([switch]$Clean)
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
& powershell -ExecutionPolicy Bypass -File (Join-Path $Root 'build.ps1') -Arch x64 $(if ($Clean) { '-Clean' })
