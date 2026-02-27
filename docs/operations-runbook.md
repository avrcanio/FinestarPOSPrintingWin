# Operations Runbook

## Expected topology
`Mozzart backend -> print_bridge -> Tailscale -> Windows Print Receiver -> local printer`

## Environment alignment
- `BARION_BAR_RECEIVER_URL=http://100.64.0.8:8089/print`
- `POS_RECEIPT_RECEIVER_URL=http://100.64.0.8:8089/print`
- `BARION_BAR_PRINTER_NAME=LBP653C654C`
- `POS_RECEIPT_PRINTER_NAME=LBP653C654C`
- `PRINT_BRIDGE_RECEIVER_TOKEN=<shared token>`

## Health checks
- Service state:
```powershell
sc query MozzartPrintReceiver
```
- Listener:
```powershell
netstat -ano | findstr 8089
```
- Basic request:
```powershell
curl -X POST http://127.0.0.1:8089/print -H "Content-Type: application/json" -d "{\"job_id\":\"healthcheck\",\"kind\":\"bar_ticket\",\"printer_name\":\"LBP653C654C\",\"payload\":{\"items\":[{\"name\":\"Ping\",\"qty\":1}]}}"
```

## Log interpretation
Structured logs include:
- `job_id`
- `kind`
- `printer_name`
- `duration_ms`
- `status`
- `duplicate`

## Common failures
- `401 missing_token`: token configured but header not sent.
- `403 invalid_token`: token mismatch.
- `print_error` with Sumatra path issue: verify `Print.SumatraPath`.
- Printer invalid: verify exact Windows printer name.

## Recovery steps
1. Validate service is running.
2. Validate local curl test.
3. Validate printer availability on Windows host.
4. Validate Tailscale connectivity from bridge host.
5. Restart service if configuration changed.
