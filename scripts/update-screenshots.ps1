$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = Join-Path $root 'artifacts'
$destination = Join-Path $root 'docs/screenshots'
New-Item -ItemType Directory -Path "$destination" -Force | Out-Null
$images = @{
    'preview-hover.png' = 'account-hover.png'
    'preview-3.png' = 'account-list.png'
    'preview-traffic-hover-0.png' = 'traffic-hover.png'
    'preview-water-modes.png' = 'water-modes.png'
}
foreach ($entry in $images.GetEnumerator()) {
    $file = Join-Path $source $entry.Key
    if (-not (Test-Path -LiteralPath "$file")) { throw '请先运行 --render-preview 生成匿名演示截图。' }
    Copy-Item -LiteralPath "$file" -Destination (Join-Path $destination $entry.Value) -Force
}
Write-Host '已更新4张匿名离线预览图，提交前须目视核验。'
