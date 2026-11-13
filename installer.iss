[Setup]
AppName=Robo Island
AppVersion=1.0.0
DefaultDirName={autopf}\RoboIsland
DefaultGroupName=Robo Island
OutputDir=out
OutputBaseFilename=RoboIsland-Setup
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
[Files]
Source: "publish\RoboIsland.exe"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{group}\Robo Island"; Filename: "{app}\RoboIsland.exe"
[Run]
Filename: "{app}\RoboIsland.exe"; Flags: nowait postinstall skipifsilent
