# WinForms Package Deployment

## Build package
From repository root:

```powershell
.\scripts\build-winforms-package.ps1
```

Output:
- `artifacts\MozzartPrintHub-win-x64.zip`

## Build EXE installer (Inno Setup)
1. Install Inno Setup 6.
2. From repository root run:

```powershell
.\scripts\build-winforms-installer.ps1
```

Output:
- `artifacts\MozzartPrintHub-Setup-win-x64.exe`

Installer behavior:
- Requests admin privileges (UAC)
- Installs app to `C:\Program Files\MozzartPrintHub`
- Adds URL ACL for `http://+:8089/` for installer user (`%USERNAME%`)
- Adds firewall rules for TCP `8089` and `9100`

## Build MSI installer (WiX, update-capable)
1. Install WiX CLI:

```powershell
dotnet tool install --global wix
```

2. Build MSI:

```powershell
.\scripts\build-winforms-msi.ps1
```

Output:
- `artifacts\MozzartPrintHub-win-x64.msi`

MSI includes:
- app version `1.0.1`
- stable `UpgradeCode` + `MajorUpgrade` behavior
- URL ACL install step for `http://+:8089/`

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
