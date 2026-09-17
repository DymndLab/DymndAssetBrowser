#ifndef MyAppVersion
  #error MyAppVersion must be supplied by build-release.ps1
#endif
#ifndef MyPublishDir
  #error MyPublishDir must be supplied by build-release.ps1
#endif
#ifndef MyOutputDir
  #error MyOutputDir must be supplied by build-release.ps1
#endif
#ifndef MyAppId
  #define MyAppId "C487E573-76D4-44E7-80A0-779289142ADB"
#endif
#define MyAppName "DYM&D Asset Browser"
#define MyAppExeName "DYM&D Asset Browser.exe"

[Setup]
AppId={{{#MyAppId}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Dymnd
DefaultDirName={localappdata}\Programs\{#MyAppName}
UsePreviousAppDir=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#MyOutputDir}
OutputBaseFilename=Dymnd-Asset-Browser-Setup-{#MyAppVersion}
SetupIconFile=..\src\DymndAssetBrowser.App\Assets\Dymnd.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Only known obsolete application binaries. Never delete arbitrary user files.
Type: files; Name: "{app}\Dym&D Asset Companion.exe"
Type: files; Name: "{app}\Dym&D Asset Companion.dll"
Type: files; Name: "{app}\Dymnd Asset Browser.exe"
Type: files; Name: "{app}\FAFamilyBrowser.App.exe"
Type: files; Name: "{app}\FAFamilyBrowser.App.dll"
Type: files; Name: "{app}\FAFamilyBrowser.Core.dll"
#ifndef MyIsTestInstall
Type: files; Name: "{userprograms}\Dym&D Asset Companion\Dym&D Asset Companion.lnk"
Type: files; Name: "{userprograms}\Dym&D Asset Companion\Feature Guide.lnk"
Type: files; Name: "{userprograms}\Dym&D Asset Companion\Documentation Folder.lnk"
Type: files; Name: "{userdesktop}\Dym&D Asset Companion.lnk"
#endif

[Icons]
#ifndef MyIsTestInstall
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
#endif

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Mutable state under %LOCALAPPDATA%\DymndAssetBrowser survives uninstall.
