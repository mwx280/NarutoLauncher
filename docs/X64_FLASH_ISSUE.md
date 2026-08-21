# x64 构建 Flash 无法运行问题专项记录

> 状态：已确认根因。x64 Flash 在**当前开发机（Windows ARM64 Parallels VM）**上无法运行，
> 分两个阶段问题：沙盒崩溃（已修复）与 ARM64 模拟器 Shell API 崩溃（环境限制）。
> 在 **x64 真机**上可正常运行（朋友已在 x64 真机验证）。
> 日期：2026-08-21

## 一、问题现象

1. x64 版 GameHost（huoyin_launcher.exe + CEF 87 x64 + Flash x64）启动游戏后，Flash 无法加载。
2. 右键页面出现"启用 Flash"提示，手动启用后 Flash 仍不启动。
3. 对比：x86 版（CEF 87 x86 + Flash x86）在当前环境完全正常。
4. 朋友在 **x64 真机**上运行相同架构的 x64 版本正常 → 崩溃为当前环境（ARM64 VM）特有。

## 二、阶段一：沙盒导致 ppapi 进程启动即崩（已修复）

### 现象
- x64 下 Flash 插件进程（ppapi）启动后立即崩溃（无 ppapi 进程出现）。
- WER 崩溃报告：`BEX64`，异常 `0xc0000005`，`StackHash_e8ad`，崩溃点 `ntdll+0x16DD54`。
- ppapi 进程加载了 `C:\WINDOWS\System32\xtajit64se.dll`（微软 x64-on-ARM64 CPU 模拟器组件）。

### 根因
- 沙盒模式下，ppapi Flash 插件进程在 ARM64 模拟的 x64 环境中初始化崩溃。
- `settings.no_sandbox = true` 只影响浏览器进程，**未传递给子进程**。

### 修复
- 在 `OnBeforeCommandLineProcessing` 中对所有子进程追加：
  - `--no-sandbox`
  - `--disable-setuid-sandbox`
- 修复后 ppapi 进程可正常启动（`--no-sandbox` 出现在 ppapi 命令行中已确认）。

## 三、阶段二：Flash 初始化时 windows.storage.dll 崩溃（环境限制，未修复）

### 现象
- 沙盒修复后，ppapi 进程能启动，但 Flash 加载 entry.swf 时崩溃。
- verbose 日志：`ppapi plugin process crashed`（反复出现，表现为"卡住"）。
- WER 报告：`APPCRASH`，异常 `0xc0000005`，故障模块 **`C:\WINDOWS\SYSTEM32\windows.storage.dll`**，
  偏移 `0x13ba02`。

### 根因
- Flash 插件在初始化时调用 Windows Shell/存储 API（windows.storage.dll）。
- 在 **x64→ARM64 Prism 模拟器**中，windows.storage.dll 的某些 API 在模拟的 x64 进程里崩溃
  （ARM64 模拟兼容性问题，非代码缺陷）。
- 崩溃点与具体 Flash 插件版本无关（官方 330/380、优化版 380 均一致）。

### 结论
- **当前 ARM64 开发机上 x64 Flash 无法运行**（模拟器 Shell API 兼容限制）。
- **x64 真机上可正常运行**（朋友已验证）。
- x86 Flash 走成熟的 x86→ARM 模拟路径，在当前环境完全正常。

## 四、优化版 Flash 测试记录

- 第三方"特殊优化版"（签名 HashMismatch）：
  - `pepflashplayer32_34_0_0_380.dll`（x86，8993768 字节）
  - `pepflashplayer64_34_0_0_380.dll`（x64，16091624 字节）
- 已替换 `third_party/pepflashplayer.dll` 与 `pepflashplayer_x64.dll` 并入库。
- x86 优化版测试通过；x64 优化版在 ARM64 VM 上同样崩溃（与插件版本无关）。

## 五、当前状态与建议

- 已修复：沙盒对 ppapi 子进程的崩溃（代码保留 `--no-sandbox` 开关）。
- 未修复：ARM64 VM 上 x64 Flash 的 windows.storage.dll 崩溃（环境限制，无法通过代码解决）。
- **正式版本：x86**（当前环境已验证正常）。
- **x64 版本：可在 x64 真机使用**（本机 ARM64 VM 无法测试，朋友已验证）。
- 若要在 ARM64 VM 上使用 x64：需等 Windows Prism 模拟器对 windows.storage.dll 兼容性改进，或换 x64 真机。

## 六、相关文件

- 崩溃修复点：`app/src/main.cpp` OnBeforeCommandLineProcessing（--no-sandbox）
- Flash 插件：`third_party/pepflashplayer.dll`（x86）、`pepflashplayer_x64.dll`（x64）
- 构建：`tools/build.ps1`（-Arch x86/x64）
