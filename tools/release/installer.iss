; DrawFight's single-file Windows installer, compiled by tools/release/release.sh:
;
;   ISCC.exe /DAppVersion=0.7.2 tools/release/installer.iss
;
; Packs the export in build/windows/ into build/release/DrawFight-Setup.exe. See .ai/releasing.md.
;
; It installs per user, into %LOCALAPPDATA%\Programs\DrawFight, with no admin prompt. That is not
; just politeness: the game updates itself by robocopying new files over its own folder
; (scripts/UpdateScreen.cs), so the folder has to stay writable by the player. Under Program
; Files it would silently never update.

#ifndef AppVersion
  #error Pass the version: ISCC /DAppVersion=X.Y.Z installer.iss
#endif

[Setup]
; The AppId is how Windows knows a new installer is an upgrade of the old one, not a second
; game. It must never change between releases.
AppId={{6F3D3A5E-EA98-416E-A24F-370FE3301268}
AppName=DrawFight
AppVersion={#AppVersion}
AppVerName=DrawFight {#AppVersion}
AppPublisher=ECEC
AppPublisherURL=https://ecec.dev/games/drawfight
; Paths below are relative to the repo root, two folders up from this script.
SourceDir=..\..
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\DrawFight
DisableProgramGroupPage=yes
DisableDirPage=auto
UsePreviousAppDir=yes
WizardStyle=modern
; No SetupIconFile: the game has no .ico (its icon is icon.png), so Inno's default is used.
UninstallDisplayIcon={app}\DrawFight.exe
OutputDir=build\release
OutputBaseFilename=DrawFight-Setup
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "build\windows\*"; DestDir: "{app}"; Excludes: "How to play.txt"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\DrawFight"; Filename: "{app}\DrawFight.exe"
Name: "{autodesktop}\DrawFight"; Filename: "{app}\DrawFight.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DrawFight.exe"; Description: "Play DrawFight now"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
; Files the self-updater copied in were never installed, so the uninstaller would leave them.
; The player's saves live in AppData\Roaming\Godot and are deliberately kept.
Type: filesandordirs; Name: "{app}"
