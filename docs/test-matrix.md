# Test Matrix

## API/Auth
1. Token disabled: no `X-Bridge-Token` should return `200` on valid request.
2. Token enabled and missing header: should return `401`.
3. Token enabled and wrong header: should return `403`.
4. Token enabled and correct header: processing should continue.

## Validation
1. Missing `job_id` => `400`.
2. Missing `kind` => `400`.
3. Unknown `kind` => `400`.
4. Missing `printer_name` => `400`.
5. `receipt_pdf` with invalid `pdf_base64` => `400`.
6. `bar_ticket` with empty `items` => `400`.

## Functional
1. Valid `receipt_pdf` prints and temp file is cleaned up.
2. Valid `bar_ticket` prints formatted text.
3. Unknown printer name => `500`.

## Idempotency
1. Same `job_id` retried within 10 minutes => one physical print, second `200 duplicate_suppressed`.
2. Same `job_id` after 10 minutes => prints again.
3. Service restart clears dedupe cache (expected in v1).

## Non-functional
1. Endpoint reachable from Tailscale source.
2. Service auto-start on reboot.
3. Logs contain correlation fields for each job.
