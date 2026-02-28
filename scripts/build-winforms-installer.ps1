param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "src\\MozzartPrintHub.WinForms\\MozzartPrintHub.WinForms.csproj"
$publishDir = Join-Path $repoRoot "artifacts\\publish\\MozzartPrintHub\\$Runtime"
$issPath = Join-Path $repoRoot "installer\\winforms\\MozzartPrintHub.iss"

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish $project `
  -c $Configuration `
  -r $Runtime `
  --self-contained false `
  -o $publishDir

$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup compiler not found. Install Inno Setup 6 and retry."
}

& $iscc $issPath
if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed with exit code $LASTEXITCODE"
}

Write-Host "Installer created in artifacts folder:"
Write-Host " - artifacts\\MozzartPrintHub-Setup-win-x64.exe"
