# =============================================================================
# 构建精简版 CEF 87（Chromium 87.0.4280.141），去掉 Flash 占位并做 Flash 调优
#
# 用途：为火影忍者OL 启动器渲染器编译一个修改过源码的 CEF 87，
#       让 Flash 自动运行（去掉 "Right-click to run Adobe Flash Player"）。
# 运行环境：x64 Windows，磁盘 40-60GB，内存 >=16GB（推荐 32GB）
#
# 用法（在目标机器上，管理员 PowerShell 或普通 PowerShell 均可）：
#   powershell -ExecutionPolicy Bypass -File tools/build_cef_flash.ps1
#
# 可选参数：
#   -DownloadDir  <源码根目录，默认 C:\cef-src>
#   -OnlyDistrib  <已有构建产物时，只重出包/裁剪，跳过构建>
#   -SkipBuild    <拉源码+打补丁后不构建（用于先生成补丁）>
# =============================================================================
param(
    [string]$DownloadDir = 'C:\cef-src',
    [switch]$SkipBuild,
    [switch]$OnlyDistrib
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# ---------------------------------------------------------------------------
# 版本常量（已核实，勿随意改动）
# ---------------------------------------------------------------------------
$CEF_BRANCH   = '4280'          # CEF 分支 = Chromium 分支号（不是 CEF 主版本号）
$CEF_CHECKOUT = '481a82af37bd1b0330abe60040bcf261374023e6'  # CEF 87.1.13 对应提交 (g481a82a)
$CHROME_TAG   = '87.0.4280.141' # Chromium 版本
$GN_TARGET    = 'Release_GN_x64'
$OUT_DIR      = "$DownloadDir\chromium\src\out\$GN_TARGET"

# 精简 + 调优的 GN 参数（经 CEF/Chromium 87 源码核实）
$GN_DEFINES = 'symbol_level=1 blink_symbol_level=0 enable_nacl=false ' +
              'enable_background_mode=false enable_resource_allowlist_generation=false chrome_pgo_phase=0'

function Write-Step([string]$m) { Write-Host "`n==== $m ====" -ForegroundColor Cyan }

function Test-Command([string]$name) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { Write-Host "[OK] 找到 $name : $($cmd.Source)" }
    else { Write-Host "[缺] 未找到 $name" -ForegroundColor Red }
    return [bool]$cmd
}

# ---------------------------------------------------------------------------
# 0. 前置环境检查
# ---------------------------------------------------------------------------
Write-Step '0/6 前置环境检查'
$need = @{}
$need.git     = Test-Command 'git'
$hasPython    = Test-Command 'python'
if (-not $hasPython) { $hasPython = Test-Command 'python3' }
$depotTools = Join-Path $DownloadDir 'depot_tools\depot_tools.bat'
$need.depot  = Test-Path $depotTools
if ($need.depot) { Write-Host "[OK] depot_tools 已就绪: $depotTools" }
else { Write-Host "[缺] depot_tools（将自动获取）" }

$vsWhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
if (Test-Path $vsWhere) {
    $vs = & $vsWhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64
    if ($vs) { Write-Host "[OK] 找到 Visual Studio: $($vs.displayName)" }
    else { Write-Host "[缺] 未找到含 C++ x86/x64 组件的 Visual Studio（CEF 87 建议 VS2019 16.11）" -ForegroundColor Red }
} else {
    Write-Host "[?] 无法检查 VS（vswhere 缺失）" -ForegroundColor Yellow
}

if (-not $need.git) { throw '未安装 git，请先安装 Git for Windows' }
if (-not $hasPython) { throw '未安装 Python 3.8+，请先安装并加入 PATH' }
if (-not $onlyDistrib) { Write-Host '环境检查完成，继续。' }

# ---------------------------------------------------------------------------
# 1. 准备 depot_tools
# ---------------------------------------------------------------------------
if (-not $onlyDistrib) {
    Write-Step '1/6 准备 depot_tools'
    if (-not $need.depot) {
        git clone https://chromium.googlesource.com/chromium/tools/depot_tools.git (Join-Path $DownloadDir 'depot_tools')
    }
    $env:Path = "$DownloadDir\depot_tools;" + $env:Path
    $env:DEPOT_TOOLS_WIN_TOOLCHAIN = '0'   # 使用本机已装的 Visual Studio
}

# ---------------------------------------------------------------------------
# 2. 拉取 CEF + Chromium 87 源码并 checkout 到 87.1.13
# ---------------------------------------------------------------------------
if (-not $onlyDistrib) {
    Write-Step '2/6 拉取 CEF 87 + Chromium 87.0.4280.141 源码（首次约 25GB）'
    $cefDir = Join-Path $DownloadDir 'cef'
    if (-not (Test-Path "$cefDir\cef_create_projects.bat")) {
        git clone https://bitbucket.org/chromiumembedded/cef.git $cefDir
        Set-Location $cefDir
        git checkout $CEF_CHECKOUT
    } else {
        Set-Location $cefDir
    }

    # 首次全量构建命令（automate-git 会自动 gclient sync + 打 CEF patch + gn gen）
    $env:GN_DEFINES = $GN_DEFINES
    & python "$cefDir\tools\automate\automate-git.py" `
        --download-dir=$DownloadDir `
        --branch=$CEF_BRANCH `
        --checkout=$CEF_CHECKOUT `
        --x64-build `
        --no-debug-build `
        --no-distrib-docs `
        --no-distrib-archive

    if ($LASTEXITCODE -ne 0) { throw "automate-git.py 失败，退出码 $LASTEXITCODE" }
}

# ---------------------------------------------------------------------------
# 3. 应用"去掉 Flash 占位"的源码补丁
# ---------------------------------------------------------------------------
Write-Step '3/6 应用 Flash 占位补丁'
$srcDir = "$DownloadDir\chromium\src"

# 定位 CEF 渲染端决策文件
$patchTarget = Get-ChildItem -Path "$srcDir\cef" -Recurse -Filter 'content_renderer_client.cc' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match 'libcef\\renderer' } | Select-Object -First 1 -ExpandProperty FullName
if (-not $patchTarget) {
    Write-Host '[警告] 未找到 libcef/renderer/content_renderer_client.cc，跳过打补丁' -ForegroundColor Yellow
} else {
    Write-Host "补丁目标: $patchTarget"

    $marker = '// [naruto] Flash auto-run'
    $content = Get-Content -LiteralPath $patchTarget -Raw -ErrorAction SilentlyContinue

    if ($content -and $content.Contains($marker)) {
        Write-Host '补丁已应用，跳过。'
    } elseif ($content) {
        # 定位 CreatePlugin 函数体（含开括号）
        $funcRegex = [regex]'(?s)CefContentRendererClient::CreatePlugin\([^{]*?\{'
        $fm = $funcRegex.Match($content)
        if (-not $fm.Success) {
            Write-Host '[警告] 未找到 CreatePlugin 函数体，请人工按 docs/CEF_FLASH_FIX.md 第二节修改' -ForegroundColor Yellow
        } else {
            # 插入点：放在 status 赋值声明之后（status 通常声明在 orig_mime_type 之后，
            # 保证 status 与 orig_mime_type 均已声明，避免"先使用后声明"编译错误）。
            $funcBody = $content.Substring($fm.Index + $fm.Length)
            $declRegex = [regex]'(?m)^[^\r\n]*\bstatus\s*=[^\r\n]*$'
            $dm = $declRegex.Match($funcBody)

            if ($dm.Success) {
                $insertAbs = $fm.Index + $fm.Length + $dm.Index + $dm.Length
                $snippet = @"

$marker
  // 强制 Flash 自动运行：忽略浏览器返回的 blocked/click-to-play 状态
  if (status != CefViewHostMsg_GetPluginInfo_Status::kNotFound &&
      base::LowerCaseEqualsASCII(orig_mime_type, "application/x-shockwave-flash")) {
    status = CefViewHostMsg_GetPluginInfo_Status::kAllowed;
  }
"@
                $content = $content.Substring(0, $insertAbs) + $snippet + $content.Substring($insertAbs)
                Set-Content -LiteralPath $patchTarget -Value $content -Encoding UTF8
                Write-Host '补丁已写入 content_renderer_client.cc（status 赋值声明之后）'
            } else {
                Write-Host '[警告] 未在函数体内找到 status 赋值声明，请人工按 docs/CEF_FLASH_FIX.md 第二节修改 CreatePlugin' -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host '[警告] 无法读取补丁目标文件，跳过打补丁' -ForegroundColor Yellow
    }
}

# ---------------------------------------------------------------------------
# 4. GN 配置（精简 + Flash 调优）已通过 GN_DEFINES 注入，这里再显式确认
# ---------------------------------------------------------------------------
if (-not $onlyDistrib -and -not $skipBuild) {
    Write-Step '4/6 确认 GN 精简参数'
    $argsGn = "$OUT_DIR\args.gn"
    if (Test-Path $argsGn) {
        Write-Host "args.gn 已生成于: $argsGn"
        Write-Host "GN_DEFINES = $GN_DEFINES"
    }
}

# ---------------------------------------------------------------------------
# 5. 构建（增量：只重编被打补丁的文件，几分钟）
# ---------------------------------------------------------------------------
if (-not $skipBuild -and -not $onlyDistrib) {
    Write-Step '5/6 增量重编译（只重编 content_renderer_client.cc）'
    if (Test-Path "$OUT_DIR\build.ninja") {
        Push-Location $OUT_DIR
        # 直接调 ninja 重编译 cefclient，避免 automate-git --force-build 重跑
        # hooks 而冲掉我们刚打的补丁。
        & ninja -C $OUT_DIR cefclient
        if ($LASTEXITCODE -ne 0) { throw "ninja 增量构建失败，退出码 $LASTEXITCODE" }
        Pop-Location
    } else {
        Write-Host '[警告] 未找到构建目录，请确认首次构建已完成' -ForegroundColor Yellow
    }
} elseif ($skipBuild -and -not $onlyDistrib) {
    Write-Step '5/6 跳过构建（-SkipBuild），源码与补丁已就绪。'
}

# ---------------------------------------------------------------------------
# 6. 出包（重新生成分发，拿到打补丁后的 libcef.dll）+ 精简
# ---------------------------------------------------------------------------
Write-Step '6/6 重新出包并精简分发目录'

# 若刚打了补丁并重编译，需用 make_distrib 重新生成分发，确保打进 patch 后的 DLL
if (-not $onlyDistrib -and (Test-Path "$OUT_DIR\build.ninja")) {
    Push-Location "$srcDir\cef"
    $env:GN_DEFINES = $GN_DEFINES
    & python "$srcDir\cef\tools\make_distrib.py" `
        --output-dir="$DownloadDir\cef\distrib" `
        --ninja-build `
        --x64-build `
        --minimal `
        --no-archive `
        --no-docs
    if ($LASTEXITCODE -ne 0) { Write-Host '[警告] make_distrib 失败，请检查' -ForegroundColor Yellow }
    Pop-Location
}

# 定位分发目录（make_distrib 生成的 cef_binary_*_distribution 或我们指定的 distrib）
$distribDir = Get-ChildItem -Path "$srcDir\cef" -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match 'distribution|^distrib$' } | Select-Object -First 1
if (-not $distribDir) {
    # 兜底：在 output 目录下查找
    $distribDir = Get-ChildItem -Path "$DownloadDir\cef" -Directory -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match 'distribution|^distrib$' } | Select-Object -First 1
}
if ($distribDir) {
    $releaseDir = "$($distribDir.FullName)\Release"
    Write-Host "分发目录: $($distribDir.FullName)"

    # 裁剪 locales：只留 zh-CN 和 en-US
    $locales = "$releaseDir\locales"
    if (Test-Path $locales) {
        Get-ChildItem $locales -Filter '*.pak' | Where-Object { $_.BaseName -notin @('zh-CN','en-US') } | Remove-Item -Force
        Write-Host 'locales 已裁剪（保留 zh-CN/en-US）'
    }

    # 删除 devtools（不用远程调试时可删）
    $devtools = "$releaseDir\devtools_resources.pak"
    if (Test-Path $devtools) { Remove-Item $devtools -Force; Write-Host '已删 devtools_resources.pak' }

    Write-Host "`n==== 完成 ====" -ForegroundColor Green
    Write-Host "产物位于: $releaseDir"
    Write-Host "把 libcef.dll、Resources 下的 pak/icudtl.dat、locales/zh-CN.pak 等替换回项目的 third_party/cef_runtime/"
    Write-Host "注意：若目标机器无可靠硬件 GPU，请保留 swiftshader/ 目录，否则老显卡会白屏。"
} else {
    Write-Host '[警告] 未找到分发目录，请检查构建是否成功' -ForegroundColor Yellow
}
