param(
    [string]$InstallDir = "$env:ProgramFiles\\MozzartPrintHub",
    [int]$HttpPort = 8089,
    [int]$EmulatorPort = 9100,
    [switch]$SkipFirewall
)

$ErrorActionPreference = "Stop"

function Assert-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Run installer from elevated PowerShell (Run as Administrator)."
    }
}

function Invoke-Netsh {
    param([string[]]$Args)
    & netsh @Args | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "netsh failed: netsh $($Args -join ' ')"
    }
}

function Ensure-UrlAcl {
    param([int]$Port, [string]$UserName)
    $url = "http://+:$Port/"
    & netsh http show urlacl url=$url | Out-Null
    if ($LASTEXITCODE -eq 0) {
        return
    }

    Invoke-Netsh -Args @("http", "add", "urlacl", "url=$url", "user=$UserName")
}

function Ensure-FirewallRule {
    param([string]$Name, [int]$Port)
    $exists = Get-NetFirewallRule -DisplayName $Name -ErrorAction SilentlyContinue
    if ($null -ne $exists) {
        return
    }

    New-NetFirewallRule `
        -DisplayName $Name `
        -Direction Inbound `
        -Action Allow `
        -Protocol TCP `
        -LocalPort $Port `
        | Out-Null
}

Assert-Admin

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$appSource = Join-Path $scriptRoot "app"
if (-not (Test-Path $appSource)) {
    throw "Package app folder not found next to installer: $appSource"
}

Write-Host "Installing to $InstallDir ..."
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -Path (Join-Path $appSource "*") -Destination $InstallDir -Recurse -Force

$user = (& whoami).Trim()
Ensure-UrlAcl -Port $HttpPort -UserName $user

if (-not $SkipFirewall) {
    Ensure-FirewallRule -Name "Mozzart Print Hub HTTP $HttpPort" -Port $HttpPort
    Ensure-FirewallRule -Name "Mozzart Print Hub TCP $EmulatorPort" -Port $EmulatorPort
}

$exePath = Join-Path $InstallDir "MozzartPrintHub.WinForms.exe"
if (-not (Test-Path $exePath)) {
    throw "Installed executable missing: $exePath"
}

Write-Host "Installation complete."
Write-Host "Executable: $exePath"
Write-Host "URLACL added for http://+:$HttpPort/ user=$user"
