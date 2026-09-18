[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$fixtureRoot = Join-Path $repositoryRoot "demo-fixtures\buggy"
$sampleRoot = Join-Path $repositoryRoot "sample-app"

$files = @(
    "README.md",
    "backend\__tests__\api.test.js",
    "backend\routes\users.js",
    "frontend\src\__tests__\App.test.js",
    "frontend\src\components\Navbar.jsx"
)

foreach ($relativePath in $files) {
    $source = Join-Path $fixtureRoot $relativePath
    $destination = Join-Path $sampleRoot $relativePath

    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Buggy fixture is missing: $source"
    }

    Copy-Item -LiteralPath $source -Destination $destination -Force
}

Write-Host "Sample application reset to the intentionally broken benchmark."
Write-Host "Changed files:"
$files | ForEach-Object { Write-Host "  sample-app\$_" }
