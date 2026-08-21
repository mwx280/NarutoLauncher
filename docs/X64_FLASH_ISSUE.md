# x64 构建 Flash 无法运行问题专项记录

> 状态：已确认根因，x64 Flash 在当前环境（Windows ARM64 模拟器）与 x64 真机均无法运行。
> 日期：2026-08-21

## 一、问题现象

1. x64 版 GameHost（huoyin_launcher.exe + CEF 87 x64 + Flash x64）启动游戏后，Flash 无法加载。
2. 右键页面出现"启用 Flash"提示，手动启用后 Flash 仍不启动（无 ppapi 进程）。
3. 对比：x86 版（CEF 87 x86 + Flash x86）完全正常，Flash 可正常加载运行。

## 二、调查过程

### 2.1 确认 Flash 插件确实存在 64 位版本

- 系统安装的官方 Flash 同时提供 32/64 位版本：
  - `C:\Windows\SysWOW64\Macromed\Flash\pepflashplayer32_34_0_0_380.dll`（x86）
  - `C:\Windows\System32\Macromed\Flash\pepflashplayer64_34_0_0_380.dll`（x64，签名有效）
- 已复制到项目 `third_party/`：
  - `pepflashplayer.dll`（x86 380）→ x86 构建使用
  - `pepflashplayer_x64.dll`（x64 380）→ x64 构建使用

### 2.2 排查过的排除项

| 假设 | 验证结果 |
|---|---|
| Flash 只有 32 位版本 | 错误，官方确有 64 位（34.0.0.380，签名有效） |
| 架构不匹配（CEF 32 + Flash 64） | 错误，x64 构建三者全为 x64 |
| Flash 插件 DLL 损坏 | 错误，LoadLibrary 可加载，5 个 PPAPI 导出函数齐全 |
| 360 安全软件干扰 | 排除（退出 360 后仍崩溃；故障模块实为微软模拟器） |
| GPU 相关问题 | 排除（--disable-gpu 后仍崩溃） |

### 2.3 根因：崩溃在 ARM64 模拟器层

- 开发机为 **Windows 11 ARM64（Parallels VM）**，x64 程序需经系统模拟器运行。
- WER 崩溃报告：`BEX64` 异常，异常码 `0xc0000005`，`StackHash_e8ad`，
  崩溃点 `ntdll+0x16DD54`，故障偏移 `0x7ffab938b820`。
- ppapi 进程加载了 `C:\WINDOWS\System32\xtajit64se.dll`——
  这是**微软官方的 "x64-on-ARM64 CPU" 模拟器组件**（Microsoft 签名，
  版本 10.0.26100.9168），非 360 模块。
- 结论：**x64 Flash 的内部 JIT / 内存操作在 x64→ARM64 Prism 模拟层执行时崩溃**。
  x86 Flash 走成熟的 x86→ARM 模拟路径，正常；x64 Flash 走 x64→ARM64 模拟路径，崩溃。

### 2.4 x64 真机同样无法运行（补充记录）

- 2026-08-21 用户反馈：在 **x64 真机**上打包的 x64 测试版同样无法运行 Flash。
- 说明问题不止于 ARM64 模拟器，CEF 87 x64 + Flash x64 组合本身也存在兼容问题。
- 具体错误表现待进一步记录（用户暂未提供真机崩溃日志）。

## 三、结论

- **Flash 游戏（火影忍者OL）在 x64 构建下无法运行**，无论 ARM64 模拟器还是 x64 真机。
- 生产环境必须使用 **x86 构建**（已验证完全正常）。
- x64 构建保留（脚本已支持），但仅作编译产物，不作为可运行版本发布。

## 四、当前状态与遗留

- 项目已支持 x86/x64 双架构构建（`tools/build-*.ps1`），Flash 按架构自动复制。
- **推荐方案：以 x86 版本为正式版本。**
- 若未来要解决 x64 Flash：需逆向 CEF/Flash x64 兼容问题（独立课题，暂缓）。

## 五、相关文件

- 官方 Flash 提取来源：`C:\Windows\...\Macromed\Flash\pepflashplayer*_34_0_0_380.dll`
- 项目内：`third_party/pepflashplayer.dll`（x86）、`third_party/pepflashplayer_x64.dll`（x64）
- 构建：`tools/build.ps1`（-Arch x86/x64）、`tools/build-x86.ps1`、`tools/build-x64.ps1`
