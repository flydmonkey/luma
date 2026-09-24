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
CloseApplications=no
WizardStyle=modern
SetupIconFile=..\src\Luma.App\Assets\App.ico
ShowLanguageDialog=auto
LanguageDetectionMethod=uilanguage

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "zh"; MessagesFile: "languages\ChineseSimplified.isl"
Name: "zht"; MessagesFile: "languages\ChineseTraditional.isl"
Name: "ja"; MessagesFile: "languages\Japanese.isl"
Name: "ko"; MessagesFile: "languages\Korean.isl"

[CustomMessages]
en.StillRunning=Luma is still running and could not be closed. End Luma.exe and Luma.ObsHost.exe in Task Manager, then run the installer again.
zh.StillRunning=无法结束正在运行的 Luma。请在任务管理器中结束 Luma.exe 和 Luma.ObsHost.exe 后重新安装。
zht.StillRunning=無法結束正在執行的 Luma。請在工作管理員中結束 Luma.exe 和 Luma.ObsHost.exe 後重新安裝。
ja.StillRunning=実行中の Luma を終了できません。タスク マネージャーで Luma.exe と Luma.ObsHost.exe を終了してから、もう一度インストーラーを実行してください。
ko.StillRunning=실행 중인 Luma를 끝낼 수 없습니다. 작업 관리자에서 Luma.exe와 Luma.ObsHost.exe를 끝낸 다음 설치 프로그램을 다시 실행하세요.

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Icons]
Name: "{autoprograms}\Luma"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Luma"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Code]
function ImageRunning(const ImageName: String): Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  if Exec(ExpandConstant('{cmd}'), '/C tasklist /NH /FI "IMAGENAME eq ' + ImageName + '" | find /I "' + ImageName + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := ResultCode = 0;
end;

procedure StopImage(const ImageName: String);
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /T /IM ' + ImageName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Attempt: Integer;
begin
  Result := '';
  StopImage('Luma.exe');
  StopImage('Luma.ObsHost.exe');
  for Attempt := 1 to 10 do
  begin
    if (not ImageRunning('Luma.exe')) and (not ImageRunning('Luma.ObsHost.exe')) then
      Exit;
    StopImage('Luma.exe');
    StopImage('Luma.ObsHost.exe');
    Sleep(300);
  end;
  Result := CustomMessage('StillRunning');
end;
