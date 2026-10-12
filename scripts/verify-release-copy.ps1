param([ValidateSet("Debug", "Release")][string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = [System.IO.Path]::GetFullPath((Join-Path $root "src/QuotaFloat/bin/$Configuration/net10.0-windows"))
$release = Join-Path $root "release"
$files = Get-ChildItem -LiteralPath "$source" -File -Recurse | Where-Object { $_.Name -ne "preview-error.txt" }
if (-not $files) { throw "没有应用构建输出，请先build。" }
foreach ($file in $files) {
    $relative = $file.FullName.Substring($source.Length).TrimStart([char]'\')
    $destination = Join-Path $release $relative
    if (-not (Test-Path -LiteralPath "$destination" -PathType Leaf)) {
        throw "本地测试目录缺少文件：$relative"
    }
    $expected = (Get-FileHash -LiteralPath "$($file.FullName)" -Algorithm SHA256).Hash
    $actual = (Get-FileHash -LiteralPath "$destination" -Algorithm SHA256).Hash
    if ($expected -ne $actual) { throw "本地测试文件与构建输出不一致：$relative" }
}
Write-Output "通过：$Configuration 完整输出已复制到release，$($files.Count)个文件SHA256一致。"
