$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$readme = Join-Path $root 'README.md'
$content = Get-Content -LiteralPath "$readme" -Raw
$matches = [regex]::Matches($content, '\]\(([^)]+)\)')
foreach ($match in $matches) {
    $link = $match.Groups[1].Value
    if ($link -notmatch '^https?://' -and -not (Test-Path -LiteralPath (Join-Path $root $link))) { throw "README链接失效：$link" }
}
$tracked = & git -C "$root" ls-files --cached --others --exclude-standard
if ($LASTEXITCODE -ne 0) { throw '无法获取公开文件清单。' }
foreach ($relative in $tracked) {
    if ($relative -match '\.(png|ico)$') { continue }
    if ($relative -match '(^|/)(auth\.json|settings\.json|\.env($|\.))|\.(zip|exe|dll|pdb)$') { throw "禁止公开的文件：$relative" }
    $text = Get-Content -LiteralPath (Join-Path $root $relative) -Raw
    if ($text -match '[A-Za-z]:[/\\](Codes|Users)[/\\]') { throw "机器专属路径未清理：$relative" }
    if ($text -match '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----|\bgh[pousr]_[A-Za-z0-9]{30,}|\bsk-[A-Za-z0-9_-]{32,}') { throw "疑似凭据：$relative" }
}
Write-Host '通过：README本地链接、公开文件清单、机器专属路径及常见凭据格式检查。'
Write-Host '限制：模式检查不是完整历史秘密扫描；发布前仍应检查Git历史与截图。'
