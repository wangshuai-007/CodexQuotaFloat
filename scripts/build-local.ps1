param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'src\QuotaFloat\QuotaFloat.csproj'
$releaseExe = Join-Path $root 'release\CodexQuotaFloat.exe'
$buildExe = Join-Path $root "src\QuotaFloat\bin\$Configuration\net10.0-windows\CodexQuotaFloat.exe"
$restart = $false

# 仅结束本项目测试程序，不按名字误杀其他目录的同名程序。
foreach ($process in @(Get-Process -Name 'CodexQuotaFloat' -ErrorAction SilentlyContinue)) {
    if ($process.Path -ieq $releaseExe -or $process.Path -ieq $buildExe) {
        $restart = $true
        Write-Host "结束本项目测试程序，释放构建文件：$($process.Id)"
        Stop-Process -Id $process.Id -ErrorAction Stop
        if (-not $process.WaitForExit(10000)) { throw '测试程序未退出，停止更新。' }
    }
}

& dotnet build "$project" -c $Configuration
if ($LASTEXITCODE -ne 0) { throw '构建或release复制失败，未重启程序。' }
& (Join-Path $PSScriptRoot 'verify-release-copy.ps1') -Configuration $Configuration
if ($restart) {
    Start-Process -FilePath "$releaseExe" -WorkingDirectory "$root" -WindowStyle Hidden
    Write-Host '已更新release并重新启动测试程序。'
}
