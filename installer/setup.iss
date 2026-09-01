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

; 说明：不再使用 [Run] 启动程序——把 requireAdministrator 的 exe 放进 [Run] 会触发
; CreateProcess 报错 740。改为在完成页用 [Code] 自定义"运行 HITAPEX"勾选框，
; 选中后用 ShellExec('runas', ...) 提权启动，避开 740。

; ============================================================
; 卸载时清理（可选）
; ============================================================
[UninstallDelete]
Type: filesandordirs; Name: "{app}"

; ============================================================
; 安装完成后"运行 HITAPEX"勾选框 + 提权自动启动
; ============================================================
[Code]
var
  LaunchCheckBox: TNewCheckBox;
  InstallationCompleted: Boolean;

procedure InitializeWizard;
begin
  InstallationCompleted := False;

  // 在"完成"页新增一个可勾选的"运行 HITAPEX"（默认勾选）
  LaunchCheckBox := TNewCheckBox.Create(WizardForm);
  LaunchCheckBox.Parent := WizardForm.FinishedPage;
  LaunchCheckBox.Left := WizardForm.FinishedLabel.Left;
  LaunchCheckBox.Top := WizardForm.FinishedLabel.Top + WizardForm.FinishedLabel.Height + 12;
  LaunchCheckBox.Width := WizardForm.FinishedLabel.Width;
  LaunchCheckBox.Caption := '运行 HITAPEX';
  LaunchCheckBox.Checked := True;
end;

// 安装真正完成后记录标志（此时仅显示完成页，尚未启动程序）
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    InstallationCompleted := True;
end;

// 用户在完成页点击"完成"、安装器关闭后才执行——确保启动动作发生在关闭安装程序之后
procedure DeinitializeSetup;
var
  ResultCode: Integer;
begin
  if InstallationCompleted and (LaunchCheckBox <> nil) and LaunchCheckBox.Checked then
  begin
    // 用 runas 动词重新提权启动，规避非提升令牌下 CreateProcess 报 740
    ShellExec('runas', ExpandConstant('{app}\HITAPEX.exe'),
              '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
  end;
end;
