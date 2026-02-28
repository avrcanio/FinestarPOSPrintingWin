# WinForms Package Deployment

## Build package
From repository root:

```powershell
.\scripts\build-winforms-package.ps1
```

Output:
- `artifacts\MozzartPrintHub-win-x64.zip`

## Install on target Windows machine
1. Unzip package.
2. Open PowerShell **as Administrator**.
3. Run:

```powershell
.\install.ps1
```

Installer actions:
- Copies app to `C:\Program Files\MozzartPrintHub` (default)
- Adds URL ACL for `http://+:8089/` for current user
- Adds firewall rules for TCP 8089 and 9100

## Uninstall
Open PowerShell as Administrator in package folder:

```powershell
.\uninstall.ps1
```
