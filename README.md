# Mozzart Print Receiver

Windows .NET 8 receiver service for print jobs coming from `print_bridge`.

## Features
- `POST /print` endpoint on configurable bind/port (default `0.0.0.0:8089`)
- Optional shared token auth with `X-Bridge-Token`
- `receipt_pdf` printing through SumatraPDF
- `bar_ticket` text rendering and printing through `PrintDocument`
- In-memory idempotency (`job_id`, 10 minutes)
- Structured logging with job correlation fields

## Project layout
- `src/MozzartPrintReceiver` application source
- `docs/deploy-windows-service.md` deployment and service install
- `docs/operations-runbook.md` operations and troubleshooting
- `docs/test-matrix.md` validation scenarios

## Local run
```bash
dotnet run --project src/MozzartPrintReceiver/MozzartPrintReceiver.csproj
```

## API
### Endpoint
- `POST /print`

### Headers
- `Content-Type: application/json`
- Optional `X-Bridge-Token: <token>` when `Receiver:Token` is configured

### Request example (`receipt_pdf`)
```json
{
  "job_id": "job-001",
  "kind": "receipt_pdf",
  "printer_name": "LBP653C654C",
  "payload": {
    "pdf_base64": "JVBERi0xLjQKJ..."
  },
  "meta": {
    "source": "pos.finestar.barion"
  }
}
```

### Request example (`bar_ticket`)
```json
{
  "job_id": "job-002",
  "kind": "bar_ticket",
  "printer_name": "LBP653C654C",
  "payload": {
    "table": "A12",
    "waiter": "Milan",
    "round_number": 2,
    "items": [
      { "name": "Espresso", "qty": 2 },
      { "name": "Gin Tonic", "qty": 1, "note": "Less ice" }
    ]
  }
}
```

### Response codes
- `200` printed or duplicate suppressed
- `401` missing token (token required)
- `403` invalid token
- `400` invalid payload
- `500` print error

## Configuration
`src/MozzartPrintReceiver/appsettings.json`

- `Receiver:Bind` default `0.0.0.0`
- `Receiver:Port` default `8089`
- `Receiver:Token` optional
- `Print:TempDir` default `C:\ProgramData\MozzartPrintReceiver\spool`
- `Print:SumatraPath` default `C:\Tools\SumatraPDF\SumatraPDF.exe`
- `Print:SumatraTimeoutSeconds` default `60`

See docs for Windows service deployment and firewall/Tailscale setup.
