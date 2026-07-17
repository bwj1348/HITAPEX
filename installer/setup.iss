; ============================================================
; HITAPEX 安装脚本
; 使用方法：
;   1. 先发布项目：
;      dotnet publish -c Release -r win-x64 --self-contained true -o publish
;   2. 在 Inno Setup Compiler 中打开此文件，Ctrl+F9 编译
; ============================================================

#define MyAppName "HITAPEX"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "HITAPEX"
#define MyAppExeName "HITAPEX.exe"
#define MySourceDir "..\bin\Release\net9.0-windows\publish\win-x64"  ; 相对于此 .iss 文件

[Setup]
; 安装包基础信息
AppId={{42CC3399-212D-478A-9C4A-1C46658A71E9}  ; 用 Tools → Generate GUID 生成一个唯一的
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=.\output
OutputBaseFilename=HITAPEX_Setup_v{#MyAppVersion}
SetupIconFile="..\Assets\AppIcon.ico"
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
AllowNoIcons=yes

; 不创建开始菜单卸载快捷方式（Windows 10/11 在设置中卸载更自然）
; 如果需要，改为 yes
; UninstallDisplayIcon={app}\{#MyAppExeName}

; 权限：普通用户也能安装（写用户目录），需要管理员则改为 admin
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"; LicenseFile: "LICENSE_chinesesimplified.rtf"
Name: "english"; MessagesFile: "compiler:Default.isl"; LicenseFile: "LICENSE_english.txt"

[Tasks]
; 桌面快捷方式
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; 把所有发布文件打入安装包
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; 确保关键文件存在时才继续
Source: "{#MySourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; 开始菜单快捷方式
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
; 开始菜单卸载入口（可选）
; Name: "{group}\卸载 HITAPEX"; Filename: "{uninstallexe}"
; 桌面快捷方式（根据用户选择）
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; 安装完成后是否运行程序（用户可勾选取消）
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

; ============================================================
; 卸载时清理（可选）
; ============================================================
[UninstallDelete]
Type: filesandordirs; Name: "{app}"
