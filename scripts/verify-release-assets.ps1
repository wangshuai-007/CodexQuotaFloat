param([ValidatePattern('^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$')][string]$Version = '0.1.0')

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$assets = Join-Path $root "artifacts/github-release/$Version"
$verification = Join-Path $root ('artifacts/package-check/' + [Guid]::NewGuid().ToString('N'))
$lines = @(Get-Content -LiteralPath (Join-Path $assets 'SHA256SUMS.txt'))
if ($lines.Count -ne 2) { throw '发布包校验数量不是2。' }
foreach ($line in $lines) {
    if ($line -notmatch '^([a-f0-9]{64})  (CodexQuotaFloat-[A-Za-z0-9.-]+\.zip)$') { throw '校验文件格式错误。' }
    $expected = $Matches[1]
    $name = $Matches[2]
    $archive = Join-Path $assets $name
    if ((Get-FileHash -LiteralPath "$archive" -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw "哈希不一致：$name" }
    $target = Join-Path $verification ([System.IO.Path]::GetFileNameWithoutExtension($name))
    Expand-Archive -LiteralPath "$archive" -DestinationPath "$target"
    $folders = @(Get-ChildItem -LiteralPath "$target" -Directory)
    if ($folders.Count -ne 1) { throw 'ZIP必须包含唯一程序目录。' }
    $folder = $folders[0].FullName
    $files = Get-ChildItem -LiteralPath "$folder" -File -Recurse
    $forbidden = @($files | Where-Object { $_.Name -match '^(settings|auth)\.json$|^\.env($|\.)|\.pdb$' })
    if ($forbidden.Count -ne 0) { throw '发布包包含禁止分发的本地配置或调试文件。' }
    $config = Get-Content -LiteralPath (Join-Path $folder 'CodexQuotaFloat.runtimeconfig.json') -Raw | ConvertFrom-Json
    if ($name -match '-self-contained\.zip$') {
        $runtime = @($config.runtimeOptions.includedFrameworks | Where-Object { $_.name -eq 'Microsoft.WindowsDesktop.App' -and $_.version -like '10.*' })
        if ($runtime.Count -ne 1 -or -not (Test-Path -LiteralPath (Join-Path $folder 'coreclr.dll'))) { throw '自包含.NET10 Desktop运行时缺失。' }
        if (-not (Test-Path -LiteralPath (Join-Path $folder 'licenses/Microsoft.NETCore.App/LICENSE.TXT')) -or
            -not (Test-Path -LiteralPath (Join-Path $folder 'licenses/Microsoft.WindowsDesktop.App/LICENSE'))) { throw '运行时许可证缺失。' }
    }
    $output = Join-Path $target 'preview-check'
    $exe = Join-Path $folder 'CodexQuotaFloat.exe'
    $process = Start-Process -FilePath "$exe" -ArgumentList "--render-preview `"$output`"" -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(60000)) { Stop-Process -Id $process.Id; throw '发布包预览检查超时。' }
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $output 'ui-checks.txt')) -or
        (Test-Path -LiteralPath (Join-Path $output 'preview-error.txt'))) { throw "解压后程序检查失败：$name" }
    $count = @(Select-String -LiteralPath (Join-Path $output 'ui-checks.txt') -Pattern '^通过').Count
    if ($count -lt 74) { throw '发布包界面检查未完整执行。' }
    Write-Host "通过：$name SHA256、包内容、解压后EXE $count 项WPF检查。"
}
