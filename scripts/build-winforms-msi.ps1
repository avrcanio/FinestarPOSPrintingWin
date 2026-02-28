param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "src\\MozzartPrintHub.WinForms\\MozzartPrintHub.WinForms.csproj"
$publishDir = Join-Path $repoRoot "artifacts\\publish\\MozzartPrintHub\\$Runtime"
$msiPath = Join-Path $repoRoot "artifacts\\MozzartPrintHub-$Runtime.msi"
$wxsPath = Join-Path $repoRoot "installer\\wix\\MozzartPrintHub.wxs"

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $msiPath) { Remove-Item $msiPath -Force }

dotnet publish $project `
  -c $Configuration `
  -r $Runtime `
  --self-contained false `
  -o $publishDir

$wixCmd = Get-Command wix -ErrorAction SilentlyContinue
if (-not $wixCmd) {
    throw "WiX v4 CLI not found. Install with: dotnet tool install --global wix"
}

Push-Location $repoRoot
try {
    wix build $wxsPath `
      -arch x64 `
      -d PublishDir="$publishDir" `
      -out $msiPath

    if ($LASTEXITCODE -ne 0) {
        throw "wix build failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

Write-Host "MSI created:"
Write-Host " - $msiPath"
