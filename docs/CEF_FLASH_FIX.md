# CEF 87 改源码：去掉 Flash 占位 + 精简构建 + Flash 游戏调优

本文档用于在 **x64 Windows 机器**上重新编译 CEF 87（基于 Chromium 87.0.4280.141），实现三件事：

1. **去掉 Flash 的 "Right-click to run Adobe Flash Player" 占位**（click-to-play），让 Flash 自动运行
2. **针对 Flash 游戏的性能调优**
3. **精简构建与分发**，减小体积、缩短编译时间

> 背景：我们做的火影忍者OL 启动器，渲染器用 CEF 87（x86）内嵌 Flash。标准 CEF API
> （`CefBrowserSettings.plugins=STATE_ENABLED`、`OnBeforePluginLoad` 返回 `PLUGIN_POLICY_ALLOW`、
> `SetPreference("profile.default_content_setting_values.plugins", 1)`）在 CEF 87 中**均无法**去掉
> Flash 占位。研究确认（CEF issues #2768/#2859）这是 CEF 87 对 Flash 的硬编码行为，**必须改源码**。

---

## 一、占位的根因与源码位置

### 决策链

```
页面 <embed type="application/x-shockwave-flash">
  ▼ Blink HTMLPlugInElement → LocalFrame::CreatePlugin
  ▼ content::RenderFrameImpl::CreatePlugin(params)   [content/renderer/render_frame_impl.cc:3890]
  ▼ GetContentClient()->renderer()->OverrideCreatePlugin(...)   ← CEF 接管点
  │
  ├─ CefContentRendererClient::OverrideCreatePlugin  [libcef/renderer/content_renderer_client.cc:485]
  │    └─ Send(CefViewHostMsg_GetPluginInfo)（同步 IPC 到浏览器）
  │         ▼ CefPluginInfoMessageFilter::OnGetPluginInfo
  │         ▼ Context::FindEnabledPlugin → Context::DecidePluginStatus
  │              │     [libcef/browser/plugins/plugin_info_message_filter.cc:303]
  │              ├─ GetPluginContentSetting()：读 plugins content setting（默认非 ALLOW）
  │              └─ CefPluginServiceFilter::IsPluginAvailable → handler->OnBeforePluginLoad(...)
  │                   （即使这里返回 ALLOW，content setting 门更优先 → 状态仍非 kAllowed）
  │
  ▼ CefContentRendererClient::CreatePlugin  [libcef/renderer/content_renderer_client.cc:643]
  ▼ switch(status):
  │    kAllowed / kPlayImportantContent → render_frame->CreatePlugin(...)  // 真 Flash
  │    kBlocked / kBlockedByPolicy / ... → CefPluginPlaceholder（占位）
  ▼ 显示 "Right-click to run Adobe Flash Player"
      右键菜单 "Run this plugin" → MENU_COMMAND_PLUGIN_RUN → LoadPlugin() → 换真插件
```

### 关键源码文件（路径相对 `chromium/src`，即 CEF 87 打补丁后的源码树）

| 文件 | 函数/位置 | 作用 |
|---|---|---|
| `libcef/renderer/content_renderer_client.cc` | `OverrideCreatePlugin` ~485 / `CreatePlugin` ~643 | 渲染端决策，按 status 建真插件或占位 |
| `libcef/browser/plugins/plugin_info_message_filter.cc` | `DecidePluginStatus` ~303 | **浏览器端根因**，按 content setting 定 status |
| `libcef/renderer/plugins/cef_plugin_placeholder.cc` | `CreateBlockedPlugin` / `ShowContextMenu` | 占位 UI + "Run this plugin" 菜单 |
| `components/plugins/renderer/loadable_plugin_placeholder.cc` | `LoadPlugin` ~281 | 占位基类，真正加载插件 |
| `chrome/app/generated_resources.grd` | `IDS_PLUGIN_BLOCKED` ~5079 | "Right-click to run $1" 字符串本体 |

---

## 二、去掉占位的源码改动（二选一）

> 注意：改源码必须**完整构建 CEF 87**（见第四节），磁盘 40-60GB、编译 2-4 小时起。

### 方案 A（推荐，渲染端改动，最小、最稳）

文件：`libcef/renderer/content_renderer_client.cc` → `CefContentRendererClient::CreatePlugin`
（读 `output.status` 之后，约 652 行）

```cpp
// Flash：无视浏览器返回的 blocked/click-to-play 状态，直接创建真实插件
if (status != CefViewHostMsg_GetPluginInfo_Status::kNotFound &&
    base::LowerCaseEqualsASCII(orig_mime_type, "application/x-shockwave-flash")) {
  status = CefViewHostMsg_GetPluginInfo_Status::kAllowed;
}
```

效果：Flash 落进 `case kAllowed` → `render_frame->CreatePlugin(info, params, throttler)`，占位不再出现。
只要求状态非 kNotFound 就强制加载，改动最小。

### 方案 B（浏览器端，打在根因上）

文件：`libcef/browser/plugins/plugin_info_message_filter.cc` → `Context::DecidePluginStatus`（~303）

在函数开头对 Flash 短路：

```cpp
if (base::LowerCaseEqualsASCII(params.mime_type, "application/x-shockwave-flash")) {
  *status = CefViewHostMsg_GetPluginInfo_Status::kAllowed;
  return;
}
```

> ⚠️ 行号以你本地 CEF 87 源码为准；函数名与机制确定，具体分支写法需在
> `libcef/browser/plugins/plugin_info_message_filter.cc` 的 87 版里确认（应对 Flash 有专门分支）。

**注意**：CEF 87 没有 `CefPreferenceManager`（87 版才有），无法运行时设
`plugins.run_all_flash_in_allow_mode`；也没有 `PreferFlashOverVideos` / `PreferHtmlOverPlugins` feature
（M87 已内联）。所以运行期开关无效，只能改源码。

---

## 三、Flash 游戏性能调优（运行期命令行开关 + GN 参数）

### 运行期 `--` 开关（浏览器进程命令行，加到 CefSettings 或 OnBeforeCommandLineProcessing）

```
--enable-gpu                       # 开启 GPU 加速（wmode=direct 依赖）
--ignore-gpu-blocklist             # 忽略 GPU 黑名单，强制硬件加速
--enable-gpu-rasterization         # GPU 光栅化
--disable-features=WebContentsOcclusion   # M87 已从 CalculateNativeWinOcclusion 更名
--use-angle=d3d11                  # ANGLE 后端（默认；老显卡可 d3d9）
--ppapi-flash-path=<pepflashplayer.dll 绝对路径>
--ppapi-flash-version=34.0.0.380
```

> ⚠️ `--disable-direct-composition` 在 M87 是否有效待验证（历史用于修 Flash 合成闪烁）。
> `wmode=direct` 是 SWF 的 embed 参数，与浏览器无关；Flash 走 GPU 合成依赖
> `libEGL/libGLESv2` + GPU 进程，**分发时不能删这两件**。

### GN 构建参数（见第四节 args.gn）

- 关 `enable_nacl=false`、`enable_background_mode=false`、`chrome_pgo_phase=0`
- `symbol_level=1`、`blink_symbol_level=0`（大幅省编译时间，勿用 0 会因无 PDB 失败）
- 需要 GPU/合成，**不要**动 `media_use_ffmpeg`、`enable_plugins`、`enable_widevine`、`enable_basic_printing`

---

## 四、精简构建 CEF 87（x64 Windows）

### 前置环境

- Windows 10/11 x64，磁盘预留 **40-60GB**，内存 **≥16GB（推荐 32GB）**
- Visual Studio（CEF 87 官方构建机用 VS2019 16.11 系列；具体小版本**待核实**）
- Windows SDK（约 10.0.19041）
- Python 3.8+、Git、**depot_tools**（Google 的 Chromium 构建工具）

### 关键版本信息

- CEF 分支名 = Chromium 分支号 = **`4280`**（不是 CEF 主版本号）
- Chromium 固定 `refs/tags/87.0.4280.141`
- 87.1.13 对应 CEF 提交 **`481a82af37bd1b0330abe60040bcf261374023e6`**（g481a82a）
- depot_tools 固定 `39d870e1f0`（见 `CHROMIUM_BUILD_COMPATIBILITY.txt`）

### 首次全量构建命令

```bat
set DEPOT_TOOLS_WIN_TOOLCHAIN=0
set GN_DEFINES=symbol_level=1 blink_symbol_level=0 enable_nacl=false
python <cef-checkout>\tools\automate\automate-git.py ^
  --download-dir=C:\cef-src --branch=4280 ^
  --checkout=481a82af37bd1b0330abe60040bcf261374023e6 ^
  --x64-build --no-debug-build --no-distrib-docs --no-distrib-archive
```

脚本会自动 clone Chromium 87.0.4280.141 → `gclient sync` → 打全部 CEF patch → `gn gen` →
`ninja -C out\Release_GN_x64 cefclient`。

> 注：`automate-git.py` 只接受数字 `--branch`（=4280），87.1.13 用 `--checkout` 精确定位。

### 精简参数（GN_DEFINES，已验证安全）

**不能动**：`enable_plugins=true`（PPAPI 开关，Flash 依赖）、`enable_widevine=true`、`enable_basic_printing`、
`optimize_webui`、`clang_use_chrome_plugins=false`、`is_component_build=false`、`media_use_ffmpeg`。

**推荐关**：`enable_nacl=false`、`enable_background_mode=false`、`enable_resource_allowlist_generation=false`、
`chrome_pgo_phase=0`、`symbol_level=1`、`blink_symbol_level=0`。

**待验证可选**：`enable_dav1d_decoder=false`、`enable_media_remoting=false`、`enable_vr=false`。

> 不要开 `is_official_build=true`（引入 ThinLTO，构建明显变慢）。

### 打补丁 / 固化改动

1. 改 Chromium 源码（见第二节）后，增量调试：`ninja -C out\Release_GN_x64 cefclient`
2. 固化改动（防被 `gclient runhooks` 清掉）：
   - 生成 unified diff → 存 `src\cef\patch\patches\<name>.patch`
   - 在 `src\cef\patch\patch.cfg` 加条目
3. 重出包：
   ```bat
   set GN_DEFINES=symbol_level=1 blink_symbol_level=0 enable_nacl=false
   python <cef-checkout>\tools\automate\automate-git.py ^
     --download-dir=C:\cef-src --branch=4280 ^
     --checkout=481a82af37bd1b0330abe60040bcf261374023e6 ^
     --x64-build --no-debug-build --force-build --force-distrib ^
     --minimal-distrib-only --no-distrib-docs --no-distrib-archive
   ```

### 分发裁剪（make_distrib --minimal 之后）

**保留**（Release 目录）：`libcef.dll`、`libcef.lib`、`chrome_elf.dll`、`libEGL.dll`、`libGLESv2.dll`、
`d3dcompiler_47.dll`、`snapshot_blob.bin`、`v8_context_snapshot.bin`
（Resources）：`icudtl.dat`、`cef.pak`、`cef_100_percent.pak`、`cef_200_percent.pak`、`cef_extensions.pak`

**可删（游戏启动器场景）**：
- `locales/` 只留 `zh-CN.pak`（建议再留 `en-US.pak` 兜底）
- `devtools_resources.pak`（不用 DevTools 时，省几 MB）
- `swiftshader/`（约 50MB，仅当能保证硬件 GPU 才删，否则老显卡白屏）

---

## 五、验证清单（编译后）

1. 加载含 Flash 的页面，确认 **无 "Right-click to run" 占位**，SWF 自动播放
2. 加载火影OL `game.huoying.qq.com/main.html`，确认登录后能进游戏
3. 检查 GPU 合成正常（`wmode=direct` 无白屏）
4. 将改好的 `libcef.dll` 等产物替换 `third_party/cef_runtime/`，重新链接渲染器

---

## 附：本仓库现状（截至本文）

- 渲染器：`renderer/`，CEF 87 x86，已含 Flash 注册（`--ppapi-flash-path`）、`plugins=STATE_ENABLED`、
  `OnBeforePluginLoad` 返回 ALLOW、`SetPreference(plugins=1)` 尝试（均未能去掉占位）
- 依赖下载：`tools/download_deps.ps1`（NuGet cef.redist.x86 + cef.sdk）
- 待朋友在 x64 机器按本文编译出改好源码的 CEF 87 产物后，替换 `third_party/cef_runtime/`
