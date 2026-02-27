# Deploy as Windows Service

## Prerequisites
- Windows host with .NET 8 runtime
- SumatraPDF installed at configured path (default `C:\Tools\SumatraPDF\SumatraPDF.exe`)
- Target printer installed and visible in Windows

## Publish
```powershell
dotnet publish .\src\MozzartPrintReceiver\MozzartPrintReceiver.csproj -c Release -r win-x64 --self-contained false -o C:\Apps\MozzartPrintReceiver
```

## Configure
Edit `C:\Apps\MozzartPrintReceiver\appsettings.json`:
- Set `Receiver.Token` to the shared `PRINT_BRIDGE_RECEIVER_TOKEN`
- Adjust `Print.SumatraPath` if needed
- Confirm `Print.TempDir`

## Install service
```powershell
sc create MozzartPrintReceiver binPath= "C:\Apps\MozzartPrintReceiver\MozzartPrintReceiver.exe" start= auto
sc start MozzartPrintReceiver
```

## Verify
```powershell
sc query MozzartPrintReceiver
netstat -ano | findstr 8089
```

## Firewall
Open inbound TCP 8089 and restrict source to Tailscale peer where possible.

Example rule:
```powershell
New-NetFirewallRule -DisplayName "Mozzart Print Receiver 8089" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 8089
```
