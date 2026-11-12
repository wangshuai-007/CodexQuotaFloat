#ifndef AppVersion
  #error AppVersion is required
#endif
[Setup]
AppId={{7EA67AC4-A7D4-4B78-968F-5FEA8072909E}
AppName=CodexQuotaFloat
AppVersion={#AppVersion}
AppPublisher=wangshuai-007
AppPublisherURL=https://github.com/wangshuai-007/CodexQuotaFloat
DefaultDirName={localappdata}\Programs\CodexQuotaFloat
DefaultGroupName=CodexQuotaFloat
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=CodexQuotaFloat-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\CodexQuotaFloat.exe
SetupIconFile=..\src\QuotaFloat\Assets\App.ico
[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"
[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked
[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\CodexQuotaFloat"; Filename: "{app}\CodexQuotaFloat.exe"
Name: "{autodesktop}\CodexQuotaFloat"; Filename: "{app}\CodexQuotaFloat.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\CodexQuotaFloat.exe"; Description: "启动 CodexQuotaFloat"; Flags: nowait postinstall skipifsilent