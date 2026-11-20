param(
    [string]$GameDir = $env:PROBABLY_STOLEN_GAME_DIR,
    [string]$PythonExe
)
$ErrorActionPreference = 'Stop'
if (-not $GameDir) { throw '请用 -GameDir 指定游戏安装目录，或设置 PROBABLY_STOLEN_GAME_DIR。' }
$root = (Get-Item -LiteralPath (Join-Path $PSScriptRoot '../..')).FullName
$game = (Get-Item -LiteralPath $GameDir).FullName
$core = Join-Path $root 'ProbablyTradingCards/dist/ProbablyTradingCards/ProbablyTradingCards.dll'
function Run-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet exited with $LASTEXITCODE" }
}
$coreBuild = @('build', (Join-Path $root 'ProbablyTradingCards/ProbablyTradingCards.csproj'), '-c', 'Release', "-p:GameDir=$game")
if ($PythonExe) { $coreBuild += "-p:PythonExe=$PythonExe" }
Run-Dotnet $coreBuild
$before = (Get-FileHash -LiteralPath $core).Hash
foreach ($name in 'DockLetters', 'CollectionBounty') {
    $build = @('build', (Join-Path $root "Examples/$name/$name.csproj"), '-c', 'Release', "-p:GameDir=$game", "-p:TradingCardsDll=$core")
    if ($PythonExe) { $build += "-p:PythonExe=$PythonExe" }
    Run-Dotnet $build
}
if ((Get-FileHash -LiteralPath $core).Hash -ne $before) { throw 'Building the add-ons changed the base DLL.' }
Run-Dotnet @('run', '--project', (Join-Path $PSScriptRoot 'Extensions.csproj'), '-c', 'Release', "-p:GameDir=$game", '--', $root, $game)
