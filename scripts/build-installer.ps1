param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$')][string]$Version,
    [Parameter(Mandatory)][string]$SourceDir,
    [Parameter(Mandatory)][string]$OutputDir
)
$ErrorActionPreference = 'Stop'
$compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
$iscc = if ($env:ISCC_EXE) { $env:ISCC_EXE } elseif ($compiler) { $compiler.Source } else { Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe' }
if (-not $env:ISCC_EXE -and -not (Test-Path -LiteralPath "$iscc")) { $iscc = Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe' }
if (-not (Test-Path -LiteralPath "$iscc")) { throw '未找到Inno Setup编译器；请手动安装官方编译器，或设置ISCC_EXE；本地脚本不自动下载安装。' }
$script = Join-Path $PSScriptRoot '../installer/CodexQuotaFloat.iss'
& "$iscc" "/DAppVersion=$Version" "/DSourceDir=$SourceDir" "/DOutputDir=$OutputDir" "$script"
if ($LASTEXITCODE -ne 0) { throw '安装包编译失败。' }
