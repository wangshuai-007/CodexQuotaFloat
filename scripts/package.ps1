param([string]$PublishDirectory = "dist")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root $PublishDirectory
$readme = Join-Path $root "README.md"
$preview = Join-Path $root "artifacts/preview-hover.png"
$archive = Join-Path $root "artifacts/CodexQuotaFloat-win-x64.zip"
if (-not (Test-Path -LiteralPath (Join-Path $dist "CodexQuotaFloat.exe"))) {
    throw (-join [char[]](0x8BF7,0x5148,0x53D1,0x5E03,0x5E94,0x7528,0x3002))
}
Copy-Item -LiteralPath $readme -Destination (Join-Path $dist "README.md") -Force
Copy-Item -LiteralPath $preview -Destination (Join-Path $dist "preview.png") -Force
Compress-Archive -LiteralPath $dist -DestinationPath $archive -Force
Write-Output ((-join [char[]](0x5DF2,0x6253,0x5305,0xFF1A)) + $archive)
