#ifndef SourceDirectory
  #error SourceDirectory must identify the complete Flutter release directory.
#endif
#ifndef OutputDirectory
  #error OutputDirectory is required.
#endif
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef PackageVersion
  #define PackageVersion "0.1.0-1"
#endif

[Setup]
AppId={{D746A181-0F4A-48C9-B9B6-3F36EC57C91B}
AppName=ShowVault
AppVersion={#AppVersion}
AppPublisher=ShowVault
#ifdef InstallDirectory
DefaultDirName={#InstallDirectory}
#else
DefaultDirName={localappdata}\Programs\ShowVault
#endif
DefaultGroupName=ShowVault
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
OutputDir={#OutputDirectory}
OutputBaseFilename=ShowVault-{#PackageVersion}-windows-x64-setup
UninstallDisplayIcon={app}\ShowVault.exe
ChangesAssociations=yes
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Files]
Source: "{#SourceDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: filesandordirs; Name: "{app}\*"

[Icons]
Name: "{group}\ShowVault"; Filename: "{app}\ShowVault.exe"
Name: "{userdesktop}\ShowVault"; Filename: "{app}\ShowVault.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Registry]
Root: HKCU; Subkey: "Software\Classes\showvault"; ValueType: string; ValueName: ""; ValueData: "URL:ShowVault authentication callback"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\showvault"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\showvault\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\ShowVault.exe,0"
Root: HKCU; Subkey: "Software\Classes\showvault\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\ShowVault.exe"" ""%1"""

[Run]
Filename: "{app}\ShowVault.exe"; Description: "Launch ShowVault"; Flags: nowait postinstall skipifsilent
