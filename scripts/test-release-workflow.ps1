$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$workflow = Get-Content -LiteralPath (Join-Path $root '.github/workflows/release.yml') -Raw
$job = ($workflow -split '  publish-release:', 2)[1]
$source = (($job -split "`n" | Where-Object { $_.StartsWith('          ') -and -not $_.StartsWith('          GH_') }) -replace '^          ', '') -join "`n"
# 仅提取run正文，排除前面的artifact路径。
$source = ($job -split '        run: \|', 2)[1]
$source = (($source -split "`n") -replace '^          ', '') -join "`n"
$tokens = $null; $errors = $null
[void][System.Management.Automation.Language.Parser]::ParseInput($source, [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
$sandbox = Join-Path $root ('artifacts/release-publish-fixture/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $sandbox 'assets') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $sandbox 'assets/release-notes.md') -Value 'fixture'
$old = Get-Location
Set-Location -LiteralPath "$sandbox"
$previousTag = $env:GITHUB_REF_NAME
function gh {
    $script:calls.Add(($args -join ' '))
    $global:LASTEXITCODE = 0
    if ($args[1] -eq 'view') {
        if ($script:scenario -eq 'new') { $global:LASTEXITCODE = 1; return }
        if ($script:scenario -eq 'published') { 'false' } else { 'true' }
    }
    if ($args[1] -eq 'upload' -and $script:scenario -eq 'upload-failed') { $global:LASTEXITCODE = 1 }
}
try {
    foreach ($case in @('new', 'draft', 'published', 'upload-failed', 'prerelease')) {
        $script:scenario = $case
        $script:calls = [System.Collections.Generic.List[string]]::new()
        $env:GITHUB_REF_NAME = if ($case -eq 'prerelease') { 'v1.2.0-beta.1' } else { 'v1.2.0' }
        $failed = $false
        try { & ([scriptblock]::Create($source)) } catch { $failed = $true }
        $publish = @($script:calls | Where-Object { $_ -match '--draft=false' })
        if ($case -in 'published', 'upload-failed') {
            if (-not $failed -or $publish.Count) { throw "错误路径意外发布：$case" }
        } else {
            if ($failed -or $publish.Count -ne 1) { throw "发布流程失败：$case" }
            if ($case -eq 'prerelease') {
                if ($publish[0] -notmatch '--latest=false' -or $publish[0] -notmatch '--prerelease ') { throw '预发布错误标为Latest' }
            } elseif ($publish[0] -notmatch '--prerelease=false --latest$') { throw '正式版未设置Latest' }
            $uploadIndex = $script:calls.FindIndex([Predicate[string]]{ param($s) $s -match '^release upload ' })
            $publishIndex = $script:calls.FindIndex([Predicate[string]]{ param($s) $s -match '--draft=false' })
            if ($uploadIndex -lt 0 -or $publishIndex -le $uploadIndex) { throw '发布早于上传' }
        }
        Write-Host "通过：模拟发布路径 $case（未调用真实GitHub）"
    }
} finally {
    $env:GITHUB_REF_NAME = $previousTag
    Set-Location -LiteralPath "$old"
    Remove-Item Function:gh
}