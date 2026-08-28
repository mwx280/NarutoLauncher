; 火影忍者Online 启动器（开源版）—— Inno Setup 安装脚本
; 用法：ISCC.exe NarutoLauncher.iss（Inno Setup 6/7）
; 需 Inno Setup 6+（https://jrsoftware.org/isdl.php）
; 注：中文语言文件使用官方自带的 Languages\ChineseSimplified.isl，无需随仓库分发。

#define AppName "火影忍者Online 启动器"
#define AppVersion "1.0.0"
#define AppPublisher "XiaoWu"
#define AppCopyright "Copyright © 2026 XiaoWu"
#define AppExe "NarutoLauncher.exe"
#define SrcDir "..\NarutoLauncher\bin\Release\net10.0-windows\win-x64"

[Setup]
AppId={{B5F3C1A2-9E41-4B7C-A6D3-8F5E2C1A9B04}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\NarutoLauncher
DefaultGroupName={#AppName}
OutputDir=..\publish
OutputBaseFilename=NarutoLauncher-{#AppVersion}-Setup
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
DisableProgramGroupPage=yes

; ---- 版本信息（避免 FileVersion 为空/显示 0.0.0.0）----
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} 安装程序
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoCopyright={#AppCopyright}
VersionInfoCompany={#AppPublisher}

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："

[Files]
Source: "{#SrcDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SrcDir}\*"; DestDir: "{app}"; Excludes: "{#AppExe},*.WebView2,*.log,speed.txt,publish,server_catalog.log,scan_debug.log,CEFFlashGameHost\userdata,CEFFlashGameHost\GPUCache,GameHost"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "立即运行 {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\*.WebView2"

; ---- 安装完成文案（品牌化，覆盖官方通用文案） ----
[CustomMessages]
chinesesimplified.BeveledLabel=开源项目
chinesesimplified.WizardReady=准备安装
chinesesimplified.WizardReadyLabel1=准备安装 {#AppName} {#AppVersion}
chinesesimplified.WizardReadyLabel2=点击「安装」开始，装完就能直接用。
chinesesimplified.WizardSelectDir=选择安装位置
chinesesimplified.WizardSelectDirLabel2=建议保持默认，直接点「下一步」。
chinesesimplified.WizardInstalling=正在安装
chinesesimplified.WizardInstallingLabel2=稍等片刻，马上就好。
chinesesimplified.FinishedHeadingLabel={#AppName} 安装完成
chinesesimplified.FinishedLabel=安装完成，感谢使用。欢迎到 GitHub 仓库 Star 或反馈问题。
