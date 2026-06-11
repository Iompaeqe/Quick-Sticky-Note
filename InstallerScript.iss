#define MyAppName "QuickSticky"
#define MyAppExeName "QuickSticky.exe"
#define MyAppPublishDir "WpfApp1\bin\Release\net9.0-windows\win-x64\publish"

; Pull the version straight from the just-published exe so it always matches the
; csproj <Version>. Requires `dotnet publish` to have run first (the build
; scripts in "WpfApp1\Docs\Dev Notes.txt" do this before invoking ISCC).
#define MyAppVersion GetVersionNumbersString(SourcePath + MyAppPublishDir + "\" + MyAppExeName)

[Setup]
AppId={{7B6A40E2-0E83-4F8F-A5F1-51E8C2E3A111}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\QuickSticky
DefaultGroupName=QuickSticky
OutputDir=InstallerOutput
OutputBaseFilename=QuickSticky_Setup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
DisableProgramGroupPage=yes

[Files]
Source: "WpfApp1\bin\Release\net9.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion; Excludes: "*.pdb"

[Icons]
Name: "{group}\QuickSticky"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall QuickSticky"; Filename: "{uninstallexe}"

[Registry]

; ------------------------------------------------------------
; .qnote extension association
; ------------------------------------------------------------

Root: HKCU; Subkey: "Software\Classes\.qnote"; ValueType: string; ValueName: ""; ValueData: "QuickSticky.Note"; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\QuickSticky.Note"; ValueType: string; ValueName: ""; ValueData: "Quick Note"; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\QuickSticky.Note\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\QuickSticky.Note\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" /open ""%1"""; Flags: uninsdeletekey

; ------------------------------------------------------------
; Right click -> New -> Quick Note
; Calls:
; QuickSticky.exe /new-from-shell "%1"
; ------------------------------------------------------------

Root: HKCU; Subkey: "Software\Classes\.qnote\ShellNew"; ValueType: string; ValueName: "Command"; ValueData: """{app}\{#MyAppExeName}"" /new-from-shell ""%1"""; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\Classes\QuickSticky.Note\ShellNew"; ValueType: string; ValueName: "ItemName"; ValueData: "Quick Note"; Flags: uninsdeletekey

; ------------------------------------------------------------
; Run on startup
; ------------------------------------------------------------

Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "QuickSticky"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue

[Run]
Filename: "{cmd}"; Parameters: "/c ie4uinit.exe -show"; Flags: runhidden
Filename: "{cmd}"; Parameters: "/c assoc .qnote=QuickSticky.Note"; Flags: runhidden

[UninstallRun]
Filename: "{cmd}"; Parameters: "/c taskkill /f /im {#MyAppExeName}"; Flags: runhidden
