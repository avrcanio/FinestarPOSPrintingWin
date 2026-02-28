# Mozzart Print Hub

Windows .NET 8 print receiver/emulator for `print_bridge` traffic over Tailscale.

## Current architecture
- Primary app: `src/MozzartPrintHub.WinForms`
- Optional legacy service: `src/MozzartPrintReceiver` (kept for compatibility)

## What it does
- Listens on:
  - HTTP `POST /print` (default `0.0.0.0:8089`)
  - Raw TCP ESC/POS (default `0.0.0.0:9100`)
- Token auth for HTTP with `X-Bridge-Token` (when token configured)
- PDF-first pipeline:
  - `receipt_pdf` uses backend-provided PDF directly
  - `bar_ticket` can be converted to PDF and handled through the same flow
- Real visual PDF preview in UI (first page render)
- Optional auto-print via SumatraPDF
- History and last-job preview in app UI

## Project layout
- `src/MozzartPrintHub.WinForms` WinForms app (main runtime)
- `src/MozzartPrintReceiver` legacy ASP.NET receiver
- `installer/winforms/install.ps1` installer script
- `installer/winforms/uninstall.ps1` uninstaller script
- `scripts/build-winforms-package.ps1` package build script
- `docs/deploy-winforms-package.md` package deployment guide
- `docs/operations-runbook.md` ops troubleshooting

## Run locally
```powershell
dotnet run --project src/MozzartPrintHub.WinForms/MozzartPrintHub.WinForms.csproj
```

## Build installer package
```powershell
.\scripts\build-winforms-package.ps1
```

Output:
- `artifacts\MozzartPrintHub-win-x64.zip`

## Build EXE installer (admin/UAC)
Requires Inno Setup 6:

```powershell
.\scripts\build-winforms-installer.ps1
```

Output:
- `artifacts\MozzartPrintHub-Setup-win-x64.exe`

The EXE installer:
- requests admin privileges,
- installs to `C:\Program Files\MozzartPrintHub`,
- runs URL ACL setup for `http://+:8089/`,
- adds firewall rules for ports `8089` and `9100`.

## Build MSI installer (supports upgrade path)
Requires WiX Toolset v4 CLI:

```powershell
dotnet tool install --global wix
.\scripts\build-winforms-msi.ps1
```

Output:
- `artifacts\MozzartPrintHub-win-x64.msi`

MSI notes:
- current app version: `1.0.1` (requested `1.001` mapped to MSI-compatible semantic version)
- uses stable `UpgradeCode` for updates
- includes `MajorUpgrade` rule (newer MSI upgrades older install)
- installer runs URL ACL step for `http://+:8089/` during install.

## Install package on target Windows host
1. Unzip package.
2. Open PowerShell as Administrator.
3. Run:

```powershell
.\install.ps1
```

Installer will:
- copy app files to `C:\Program Files\MozzartPrintHub`
- add URL ACL for `http://+:8089/` for current user
- add firewall rules for ports `8089` and `9100`

## HTTP API
### Endpoint
- `POST /print`

### Headers
- `Content-Type: application/json`
- Optional `X-Bridge-Token: <token>`

### Request example (`receipt_pdf`)
```json
{
  "job_id": "fixture-receipt-001",
  "kind": "receipt_pdf",
  "printer_name": "STAR_TSP100",
  "payload": {
    "filename": "pos-receipt-999.pdf",
    "pdf_base64": "JVBERi0xLjcKJc..."
  },
  "meta": {
    "source": "mozzart",
    "receipt_id": 999
  }
}
```

## WinForms app settings
File:
- `src/MozzartPrintHub.WinForms/appsettings.json`

Key fields:
- `Receiver:Bind`, `Receiver:Port`, `Receiver:Token`
- `Emulator:Enabled`, `Emulator:Bind`, `Emulator:Port`
- `Print:DefaultPrinterName`, `Print:AutoPrint`
- `Print:SumatraPath`, `Print:SumatraTimeoutSeconds`

## Notes
- For preview-only mode, keep `Auto print` OFF.
- For real printing, set valid `SumatraPath` and turn `Auto print` ON.
