param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "src\\MozzartPrintHub.WinForms\\MozzartPrintHub.WinForms.csproj"
$publishDir = Join-Path $repoRoot "artifacts\\publish\\MozzartPrintHub\\$Runtime"
$packageRoot = Join-Path $repoRoot "artifacts\\package\\MozzartPrintHub"
$zipPath = Join-Path $repoRoot "artifacts\\MozzartPrintHub-$Runtime.zip"

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $packageRoot) { Remove-Item $packageRoot -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

dotnet publish $project `
  -c $Configuration `
  -r $Runtime `
  --self-contained false `
  -o $publishDir

New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Copy-Item -Path $publishDir -Destination (Join-Path $packageRoot "app") -Recurse -Force
Copy-Item -Path (Join-Path $repoRoot "installer\\winforms\\install.ps1") -Destination $packageRoot -Force
Copy-Item -Path (Join-Path $repoRoot "installer\\winforms\\uninstall.ps1") -Destination $packageRoot -Force

$readme = @"
Mozzart Print Hub package

1) Run PowerShell as Administrator
2) Execute: .\\install.ps1
3) Start: `"$env:ProgramFiles\\MozzartPrintHub\\MozzartPrintHub.WinForms.exe`"

The installer adds URL ACL for HTTP receiver:
http://+:8089/
"@
Set-Content -Path (Join-Path $packageRoot "README.txt") -Value $readme -Encoding UTF8

Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $zipPath

Write-Host "Package created: $zipPath"
