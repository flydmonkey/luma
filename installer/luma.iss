#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

#define MyAppName "Luma"
#define MyAppExeName "Luma.exe"

[Setup]
AppId={{B7E2A1C4-9D35-4F68-A1E0-6C8B4D2F0A17}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=flydmonkey
DefaultDirName={autopf}\Luma
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputBaseFilename=Luma-{#MyAppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
WizardStyle=modern
SetupIconFile=..\src\Luma.App\Assets\App.ico

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Luma"; Filename: "{app}\{#MyAppExeName}"
