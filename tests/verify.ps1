param(
    [string]$GameDir = $env:PROBABLY_STOLEN_GAME_DIR,
    [string]$PythonExe
)

$ErrorActionPreference = 'Stop'
if (-not $GameDir) { throw '请用 -GameDir 指定游戏安装目录，或设置 PROBABLY_STOLEN_GAME_DIR。' }
$root = (Get-Item -LiteralPath (Join-Path $PSScriptRoot '..')).FullName
$game = (Get-Item -LiteralPath $GameDir).FullName
$localPython = Join-Path $root '.venv/Scripts/python.exe'
if (-not $PythonExe) {
    $PythonExe = if (Test-Path -LiteralPath $localPython) { $localPython } else { 'python' }
} elseif (Test-Path -LiteralPath $PythonExe -PathType Leaf) {
    $PythonExe = (Get-Item -LiteralPath $PythonExe).FullName
}

& $PythonExe -X utf8 -m unittest discover -s $PSScriptRoot -p 'test_*.py'
if ($LASTEXITCODE -ne 0) { throw "Python tests failed ($LASTEXITCODE)." }

& $PythonExe -X utf8 (Join-Path $root 'scripts/check_art_exports.py')
if ($LASTEXITCODE -ne 0) { throw "Artwork export comparison failed ($LASTEXITCODE)." }

& dotnet run --project (Join-Path $PSScriptRoot 'Smoke/Smoke.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "Smoke tests failed ($LASTEXITCODE)." }

& $PythonExe -X utf8 (Join-Path $PSScriptRoot 'check_series_identities.py')
if ($LASTEXITCODE -ne 0) { throw "Series identity checks failed ($LASTEXITCODE)." }

& (Join-Path $PSScriptRoot 'Extensions/run.ps1') -GameDir $game -PythonExe $PythonExe

& dotnet build (Join-Path $root 'ProbablyTradingCards/ProbablyTradingCards.csproj') -c Release "-p:GameDir=$game" "-p:PythonExe=$PythonExe" '-p:NativeValidation=true'
if ($LASTEXITCODE -ne 0) { throw "Native validation package build failed ($LASTEXITCODE)." }

Write-Output 'PASS: Python tests, Smoke, add-on integration, and both package builds. No in-game checks ran.'
