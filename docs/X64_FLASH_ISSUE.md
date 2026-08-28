# x64 构建 Flash 无法运行问题专项记录

> 状态：**已解决**。x64 Flash 已能在当前开发机（Windows ARM64 Parallels VM）正常运行。
> 根因是 no_console_hook 的手工 inline hook 在 x64 下跳转溢出崩溃（非模拟器问题），
> 已用 MinHook 重写修复。
> 日期：2026-08-21

## 一、问题现象

1. x64 版 GameHost（CEFFlashGameHost.exe + CEF 87 x64 + Flash x64）启动游戏后，Flash 无法加载。
2. 右键页面出现"启用 Flash"提示，手动启用后 Flash 仍不启动（无 ppapi 进程）。
3. 对比：x86 版（CEF 87 x86 + Flash x86）完全正常。
4. 朋友在 x64 真机上跑极简版正常 → 排除"x64 Flash 本身不可用"。

## 二、根因一：沙盒未传递给子进程（已修复）

### 现象
- x64 下 ppapi Flash 插件进程启动后立即崩溃。
- WER：`BEX64`，异常 `0xc0000005`，崩溃点 `ntdll+0x16DD54`。

### 根因
- `settings.no_sandbox = true` 只影响浏览器进程，**未传递给子进程**。

### 修复
- `OnBeforeCommandLineProcessing` 中对所有进程追加 `--no-sandbox` + `--disable-setuid-sandbox`。
- 修复后 ppapi 进程可正常启动。

## 三、根因二：no_console_hook 手工 inline hook 在 x64 下崩溃（真正根因，已修复）

### 现象
- 沙盒修复后，ppapi 进程能启动，但 Flash 加载 entry.swf 时崩溃。
- verbose 日志：`ppapi plugin process crashed`（反复出现）。
- WER：`APPCRASH`，故障模块 `windows.storage.dll`（实为 hook 破坏代码流后的连锁反应）。

### 排查过程
1. 误判为 ARM64 模拟器问题（故障模块 `xtajit64se.dll` 是微软 x64-on-ARM64 模拟器，实际无关）。
2. **决定性实验**：新建极简 CEF 宿主（cefsimple_test，仅注册 Flash + 加载页面，无 hook），
   x64 Flash 完全正常加载 → 排除环境限制，问题在完整版某个功能。
3. **定位**：完整版禁用 ppapi 的 no_console_hook 后，Flash 恢复正常 → 根因确认。

### 根因
- `no_console_hook.cpp` 手工实现 x86 inline hook：向 `CreateProcessW` 开头写
  `jmp rel32`（5 字节），rel32 为**有符号 32 位偏移，只能跳 ±2GB**。
- x86 下 kernel32 与 exe 都在低 2GB 内，正常；**x64 下 ASLR 后两者距离可能超过 ±2GB**，
  偏移溢出 → 跳到错误地址 → ppapi 进程调用 CreateProcessW 时崩溃。

### 修复
- 引入 **MinHook v1.3.4**（third_party/minhook，x86/x64 通用）：
  - HDE 反汇编引擎精确解析指令长度，不截断指令。
  - 自动处理 x64 绝对跳转，不受 ±2GB 限制。
  - `MH_CreateHookApi("kernel32.dll", "CreateProcessW/A", ...)` 一行完成 hook。
  - 另 hook `CreateProcessInternalW`（kernel32 私有导出，Flash 探测可能直接调用）。
- **注意**：`MH_EnableHook(pTarget)` 的参数是**目标函数地址**（CreateProcessW），
  不是 detour 地址。传错会导致 hook 安装但不生效（字节未被 patch）。
- CMakeLists 按架构链接 MinHook lib，POST_BUILD 复制 MinHook DLL 到 exe 目录。

## 四、验证结果

- 完整版 x64：ppapi 进程稳定，Flash 正常加载，cmd 窗口不再闪烁（hook 拦截沙箱探测）。
- x86 版改用 MinHook 后仍正常。
- 一键构建（tools/build.ps1）自动部署 MinHook DLL。

## 五、优化版 Flash 说明

- 第三方"特殊优化版"（签名 HashMismatch）已入库：
  - `third_party/pepflashplayer.dll`（x86）
  - `third_party/pepflashplayer_x64.dll`（x64）
- x86 与 x64 均已验证可正常运行 Flash。

## 六、相关文件

- 根因修复点：
  - `app/src/no_console_hook.cpp`（MinHook 重写）
  - `app/src/main.cpp`（--no-sandbox + ppapi hook 调用）
  - `app/CMakeLists.txt`（MinHook 链接与部署）
- 依赖：`third_party/minhook/`（MinHook v1.3.4）
- 实验宿主：`cefsimple_test/`（NarutoLauncher 同级，独立实验目录）
- Flash：`third_party/pepflashplayer.dll` / `pepflashplayer_x64.dll`
