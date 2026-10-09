[Setup]
AppName=DeskRail
AppVersion=1.0.0
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
SetupIconFile=..\DeskRail.App\Assets\DeskRail.ico
Publisher=DeskRail
DefaultDirName={autopf}\DeskRail
DefaultGroupName=DeskRail
OutputBaseFilename=DeskRail-Setup-1.0.0
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=lowest
DisableDesktopShortcut=yes

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\DeskRail"; Filename: "{app}\DeskRail.App.exe"

[Run]
Filename: "{app}\DeskRail.App.exe"; Description: "启动 DeskRail"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\DeskRail.App.exe"; Parameters: "--restore-desktop"; Flags: runhidden
