param(
    [string]$InstallDir = "$env:ProgramFiles\\MozzartPrintHub",
    [int]$HttpPort = 8089,
    [int]$EmulatorPort = 9100
)

$ErrorActionPreference = "Stop"

function Assert-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Run uninstaller from elevated PowerShell (Run as Administrator)."
    }
}

function Try-Netsh {
    param([string[]]$Args)
    & netsh @Args | Out-Null
}

function Remove-FirewallRuleSafe {
    param([string]$Name)
    $rule = Get-NetFirewallRule -DisplayName $Name -ErrorAction SilentlyContinue
    if ($null -ne $rule) {
        Remove-NetFirewallRule -DisplayName $Name | Out-Null
    }
}

Assert-Admin

$httpUrl = "http://+:$HttpPort/"
Try-Netsh -Args @("http", "delete", "urlacl", "url=$httpUrl")

Remove-FirewallRuleSafe -Name "Mozzart Print Hub HTTP $HttpPort"
Remove-FirewallRuleSafe -Name "Mozzart Print Hub TCP $EmulatorPort"

if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force
}

Write-Host "Uninstall complete."
