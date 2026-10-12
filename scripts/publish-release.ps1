param([ValidatePattern('^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$')][string]$Version = '0.1.0')

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'src/QuotaFloat/QuotaFloat.csproj'
$staging = Join-Path $root ('artifacts/publish/' + [Guid]::NewGuid().ToString('N'))
$assets = Join-Path $root "artifacts/github-release/$Version"
New-Item -ItemType Directory -Path "$assets" -Force | Out-Null
$archives = @()

foreach ($kind in @('self-contained', 'framework-dependent')) {
    $name = "CodexQuotaFloat-$Version-win-x64-$kind"
    $folder = Join-Path $staging $name
    $contained = if ($kind -eq 'self-contained') { 'true' } else { 'false' }
    & dotnet publish "$project" -c Release -r win-x64 --self-contained $contained -o "$folder" `
        "-p:Version=$Version" -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -p:SkipLocalTestReleaseCopy=true
    if ($LASTEXITCODE -ne 0) { throw "发布失败：$kind" }
    if ($kind -eq 'self-contained') {
        foreach ($required in @('hostfxr.dll', 'hostpolicy.dll', 'coreclr.dll', 'PresentationFramework.dll')) {
            if (-not (Test-Path -LiteralPath (Join-Path $folder $required))) { throw "自包含包缺少运行时：$required" }
        }
    }
    if ($kind -eq 'self-contained') {
        $cacheLine = & dotnet nuget locals global-packages --list
        if ($LASTEXITCODE -ne 0) { throw '无法读取NuGet运行时包位置。' }
        $cache = ($cacheLine -replace '^[^:]+:\s*', '').Trim()
        $config = Get-Content -LiteralPath (Join-Path $folder 'CodexQuotaFloat.runtimeconfig.json') -Raw | ConvertFrom-Json
        foreach ($framework in $config.runtimeOptions.includedFrameworks) {
            $package = Join-Path $cache "$($framework.name.ToLowerInvariant()).runtime.win-x64/$($framework.version)"
            $licenseFolder = Join-Path $folder "licenses/$($framework.name)"
            New-Item -ItemType Directory -Path "$licenseFolder" -Force | Out-Null
            $notices = @(Get-ChildItem -LiteralPath "$package" -File | Where-Object { $_.Name -match '^LICENSE(\.TXT)?$|^THIRD-PARTY-NOTICES\.TXT$' })
            if ($notices.Count -eq 0) { throw "缺少运行时许可证：$($framework.name)" }
            foreach ($notice in $notices) { Copy-Item -LiteralPath "$($notice.FullName)" -Destination "$licenseFolder" }
        }
    }
    Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination "$folder"
    Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination "$folder" -Recurse
    $archive = Join-Path $assets "$name.zip"
    Compress-Archive -LiteralPath "$folder" -DestinationPath "$archive" -Force
    $archives += $archive
}

$hashes = $archives | ForEach-Object {
    ((Get-FileHash -LiteralPath "$_" -Algorithm SHA256).Hash.ToLowerInvariant()) + '  ' + [System.IO.Path]::GetFileName($_)
}
[System.IO.File]::WriteAllLines((Join-Path $assets 'SHA256SUMS.txt'), $hashes, [System.Text.UTF8Encoding]::new($false))
$notes = Get-Content -LiteralPath (Join-Path $root 'docs/release-notes.md') -Raw
[System.IO.File]::WriteAllText((Join-Path $assets 'release-notes.md'), $notes.Replace('{{VERSION}}', $Version), [System.Text.UTF8Encoding]::new($false))
Write-Host "GitHub Release产物已生成：$assets"
Write-Host "自包含发布目录：$(Join-Path $staging "CodexQuotaFloat-$Version-win-x64-self-contained")"
