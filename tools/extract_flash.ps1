# 获取 Flash.cn PPAPI 34 x86 插件（pepflashplayer.dll）
# 用法：powershell -ExecutionPolicy Bypass -File tools/extract_flash.ps1
#
# 背景（2026-08 实测）：
#   Flash.cn 官方 PPAPI 安装包（flashplayerpp_install_cn_web.exe，约 2.3MB）是专有引导程序，
#   不支持 /extract 静默解压，也不内嵌可直接读取的 pepflashplayer.dll。
#   因此本脚本改为两条路径：
#     1) 若系统已安装 Flash Player（C:\Windows\SysWOW64\Macromed\Flash\pepflashplayer.dll），
#        则直接从系统目录复制（x86 插件位于 SysWOW64，即 32 位目录）。
#     2) 否则，下载官方安装包到 third_party，供你手动运行安装后，再运行本脚本复制。
#
# 安全提示：请务必使用官方安装包并核对签名，勿从第三方站点下载 pepflashplayer.dll。

param(
    [string]$RootDir = (Resolve-Path (Join-Path $PSScriptRoot '..'))
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$ThirdParty = Join-Path $RootDir 'third_party'
New-Item -ItemType Directory -Force -Path $ThirdParty | Out-Null
$dest = Join-Path $ThirdParty 'pepflashplayer.dll'

# 候选来源（x86 插件位于 SysWOW64）
$candidates = @(
    (Join-Path $env:SystemRoot 'SysWOW64\Macromed\Flash\pepflashplayer.dll'),
    (Join-Path $env:SystemRoot 'SysWOW64\Macromed\Flash\NPSWF32.dll'),
    (Join-Path $env:windir 'SysWOW64\Macromed\Flash\pepflashplayer.dll')
)

$found = $null
foreach ($c in $candidates) {
    if (Test-Path -LiteralPath $c) { $found = $c; break }
}

if ($found) {
    Copy-Item -LiteralPath $found -Destination $dest -Force
    Write-Host "==> 已从系统目录复制 Flash 插件: $dest ($((Get-Item $dest).Length) 字节)" -ForegroundColor Cyan
}
else {
    # 未安装：下载官方安装包备用，提示手动安装
    $installer = Join-Path $ThirdParty 'flashplayerpp_install_cn_web.exe'
    if (-not (Test-Path -LiteralPath $installer)) {
        $url = 'https://www.flash.cn/cdm/hm/webplayer/flashplayerpp_install_cn_web.exe'
        Write-Host '==> 下载 Flash PPAPI 官方安装包 (34.0.0.380)' -ForegroundColor Cyan
        Invoke-WebRequest -Uri $url -OutFile $installer -UseBasicParsing
        Write-Host "   已下载: $installer ($((Get-Item $installer).Length) 字节)"
    }
    Write-Host ''
    Write-Host '尚未找到已安装的 Flash 插件。请手动运行以下官方安装包完成安装，' -ForegroundColor Yellow
    Write-Host "    $installer" -ForegroundColor Yellow
    Write-Host '安装完成后（pepflashplayer.dll 会落在 C:\Windows\SysWOW64\Macromed\Flash\），' -ForegroundColor Yellow
    Write-Host '重新运行本脚本即可自动复制到 third_party。' -ForegroundColor Yellow
}
