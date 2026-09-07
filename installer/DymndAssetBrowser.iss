#ifndef MyAppVersion
  #define MyAppVersion "2.5.1"
#endif

#ifndef MyAppId
  #define MyAppId "C487E573-76D4-44E7-80A0-779289142ADB"
#endif

#ifndef MyAppName
  #define MyAppName "Dym&D Asset Companion"
#endif

#ifndef MyOutputBaseFilename
  #define MyOutputBaseFilename "Dymnd-Asset-Companion-Setup-" + MyAppVersion
#endif

#define MyAppExeName "Dym&D Asset Companion.exe"

[Setup]
AppId={{{#MyAppId}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Dymnd
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename={#MyOutputBaseFilename}
SetupIconFile=..\src\FAFamilyBrowser.App\Assets\Dymnd.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: filesandordirs; Name: "{app}\*"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Feature Guide"; Filename: "{app}\docs\Dymnd-Asset-Companion-Feature-Guide.pdf"
Name: "{group}\Documentation Folder"; Filename: "{app}\docs"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Deliberately empty: mutable state under %LOCALAPPDATA%\DymndAssetBrowser survives uninstall.
