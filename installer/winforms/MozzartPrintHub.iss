; Inno Setup script for Mozzart Print Hub
; Requires admin privileges (UAC) and configures URL ACL + firewall rules.

#define MyAppName "Mozzart Print Hub"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Mozzart"
#define MyAppExeName "MozzartPrintHub.WinForms.exe"

[Setup]
AppId={{A13F2C1F-73EC-4764-8DFE-0A37B9A67A57}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\MozzartPrintHub
DefaultGroupName={#MyAppName}
OutputDir=..\..\artifacts
OutputBaseFilename=MozzartPrintHub-Setup-win-x64
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
WizardStyle=modern
DisableDirPage=no
DisableProgramGroupPage=yes

[Files]
Source: "..\..\artifacts\publish\MozzartPrintHub\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{cmd}"; Parameters: "/C netsh http add urlacl url=http://+:8089/ user=""%USERNAME%"""; Flags: runhidden waituntilterminated; StatusMsg: "Configuring HTTP URL ACL..."
Filename: "{cmd}"; Parameters: "/C powershell -NoProfile -ExecutionPolicy Bypass -Command ""if (-not (Get-NetFirewallRule -DisplayName 'Mozzart Print Hub HTTP 8089' -ErrorAction SilentlyContinue)) { New-NetFirewallRule -DisplayName 'Mozzart Print Hub HTTP 8089' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 8089 | Out-Null }"""; Flags: runhidden waituntilterminated; StatusMsg: "Adding firewall rule for TCP 8089..."
Filename: "{cmd}"; Parameters: "/C powershell -NoProfile -ExecutionPolicy Bypass -Command ""if (-not (Get-NetFirewallRule -DisplayName 'Mozzart Print Hub TCP 9100' -ErrorAction SilentlyContinue)) { New-NetFirewallRule -DisplayName 'Mozzart Print Hub TCP 9100' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 9100 | Out-Null }"""; Flags: runhidden waituntilterminated; StatusMsg: "Adding firewall rule for TCP 9100..."
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C netsh http delete urlacl url=http://+:8089/"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C powershell -NoProfile -ExecutionPolicy Bypass -Command ""if (Get-NetFirewallRule -DisplayName 'Mozzart Print Hub HTTP 8089' -ErrorAction SilentlyContinue) { Remove-NetFirewallRule -DisplayName 'Mozzart Print Hub HTTP 8089' | Out-Null }"""; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C powershell -NoProfile -ExecutionPolicy Bypass -Command ""if (Get-NetFirewallRule -DisplayName 'Mozzart Print Hub TCP 9100' -ErrorAction SilentlyContinue) { Remove-NetFirewallRule -DisplayName 'Mozzart Print Hub TCP 9100' | Out-Null }"""; Flags: runhidden waituntilterminated
