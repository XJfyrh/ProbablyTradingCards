#requires -Version 7.0
param(
    [string]$GameDir = $env:PROBABLY_STOLEN_GAME_DIR,
    [string]$PythonExe,
    [string]$OutputDir,
    [string]$PreviewFile,
    [UInt64]$WorkshopItemId = 3842861920,
    [ValidateSet('Hidden', 'FriendsOnly', 'Public')]
    [string]$Visibility,
    [string]$ChangeNote
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
$name = 'ProbablyTradingCards'
$projectDir = Join-Path $repo $name
$docs = Join-Path $repo 'docs/release'
$manifest = ([xml](Get-Content -Raw -LiteralPath (Join-Path $projectDir 'manifest.xml'))).Manifest
$version = [string]$manifest.ModVersion
if ($manifest.ID -cne 'probably_trading_cards' -or $version -cnotmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][A-Za-z0-9.+-]+)?$') {
    throw 'Expected the ProbablyTradingCards manifest and a semantic version.'
}

if (-not $GameDir) { throw 'Specify the Probably Stolen directory with -GameDir or PROBABLY_STOLEN_GAME_DIR.' }
$game = (Resolve-Path -LiteralPath $GameDir).ProviderPath
if (-not $PythonExe) {
    $venvPython = Join-Path $repo '.venv/Scripts/python.exe'
    $PythonExe = if (Test-Path -LiteralPath $venvPython -PathType Leaf) { $venvPython } else { 'python' }
} elseif (Test-Path -LiteralPath $PythonExe -PathType Leaf) {
    $PythonExe = (Resolve-Path -LiteralPath $PythonExe).ProviderPath
}

if (-not $PreviewFile) {
    $PreviewFile = Join-Path $projectDir 'Textures/Cards/card_legends_joe.png'
}
$preview = Get-Item -LiteralPath $PreviewFile
if ($preview.PSIsContainer -or $preview.Length -lt 16 -or $preview.Length -ge 1MB) {
    throw 'The Workshop preview must be an image file smaller than 1 MB.'
}
$validatePreview = @'
from pathlib import Path
import sys
from PIL import Image

path = Path(sys.argv[1])
formats = {".jpg": "JPEG", ".jpeg": "JPEG", ".png": "PNG", ".gif": "GIF"}
with Image.open(path) as image:
    if image.format != formats.get(path.suffix.lower()):
        raise SystemExit(f"Expected a PNG, JPEG, or GIF preview with the matching extension: {path}")
    image.verify()
'@
& $PythonExe -X utf8 -c $validatePreview $preview.FullName
if ($LASTEXITCODE -ne 0) { throw 'Workshop preview validation failed.' }

if (-not $OutputDir) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $OutputDir = Join-Path $repo "artifacts/releases/v$version-$stamp"
}
$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDir)
if (Test-Path -LiteralPath $output) { throw "Output already exists: $output" }
$outputParent = [IO.Path]::GetDirectoryName($output)
$outputLeaf = [IO.Path]::GetFileName($output)
if (-not $outputParent -or -not $outputLeaf) { throw 'Choose a new release output directory.' }
[IO.Directory]::CreateDirectory($outputParent) | Out-Null
$outputParent = (Resolve-Path -LiteralPath $outputParent).ProviderPath
$output = Join-Path $outputParent $outputLeaf

function Assert-ReleaseDll([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        $pe = [Reflection.PortableExecutable.PEReader]::new($stream)
        try {
            $metadata = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
            foreach ($handle in $metadata.TypeDefinitions) {
                $type = $metadata.GetTypeDefinition($handle)
                if ($metadata.GetString($type.Name) -in 'NativeValidation', 'NativeAgentBridge') {
                    throw "Developer validation code in release DLL: $Path"
                }
            }
        } finally {
            $pe.Dispose()
        }
    } finally {
        $stream.Dispose()
    }
}

function Write-WorkshopVdf([string]$Path, [System.Collections.IDictionary]$Fields) {
    $lines = @('"workshopitem"', '{')
    foreach ($key in $Fields.Keys) {
        $value = ([string]$Fields[$key]).Replace('\', '\\').Replace('"', '\"')
        $lines += '    "{0}" "{1}"' -f $key, $value
    }
    $lines += '}'
    [IO.File]::WriteAllLines($Path, [string[]]$lines, [Text.UTF8Encoding]::new($false))
}

$stageName = '.release-' + [Guid]::NewGuid().ToString('N')
$stage = Join-Path $outputParent $stageName
[IO.Directory]::CreateDirectory($stage) | Out-Null
try {
    $content = Join-Path $stage $name
    & dotnet build (Join-Path $projectDir "$name.csproj") -c Release "-p:GameDir=$game" "-p:PythonExe=$PythonExe" "-p:PackageDir=$content" '-p:NativeValidation=false' '-p:UpdateSeriesIdentities=false'
    if ($LASTEXITCODE -ne 0) { throw "Release build failed ($LASTEXITCODE)." }

    $required = @("$name.dll", 'manifest.xml', 'README.md', 'CREDITS.md', 'LICENSE')
    $required += @(Get-ChildItem -LiteralPath (Join-Path $projectDir 'Sounds') -Filter '*.wav' -File |
        ForEach-Object { "Sounds/$($_.Name)" })
    $files = @(Get-ChildItem -LiteralPath $content -Recurse -Force -File | ForEach-Object {
        [IO.Path]::GetRelativePath($content, $_.FullName).Replace('\', '/')
    })
    foreach ($file in $required) {
        if ($file -cnotin $files) { throw "Missing release file: $file" }
    }
    foreach ($file in $files) {
        if ($file -cnotin $required -and $file -cnotmatch '^Textures/(?:Items|Paper)/[A-Za-z0-9_-]+\.png$') {
            throw "Unexpected release file: $file"
        }
    }
    $packedManifest = ([xml](Get-Content -Raw -LiteralPath (Join-Path $content 'manifest.xml'))).Manifest
    $assemblies = @($packedManifest.Assemblies.Assembly)
    if ($packedManifest.ID -cne $manifest.ID -or $packedManifest.ModVersion -cne $version -or
        $assemblies.Count -ne 1 -or $assemblies[0] -cne "$name.dll") {
        throw 'The packaged manifest does not match the release DLL and version.'
    }
    Assert-ReleaseDll (Join-Path $content "$name.dll")

    $zipName = "$name-v$version.zip"
    $zipPath = Join-Path $stage $zipName
    [IO.Compression.ZipFile]::CreateFromDirectory($content, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $true)
    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $expected = @($files | ForEach-Object { "$name/$_" } | Sort-Object)
        $actual = @($archive.Entries.FullName | Sort-Object)
        if (Compare-Object $expected $actual -CaseSensitive) { throw 'ZIP entries differ from the Workshop payload.' }
    } finally {
        $archive.Dispose()
    }
    $zipSha = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$zipSha  $zipName" | Set-Content -LiteralPath (Join-Path $stage SHA256SUMS.txt) -Encoding utf8NoBOM

    $description = (Get-Content -Raw -LiteralPath (Join-Path $docs 'workshop-description.txt')).Trim()
    $fields = [ordered]@{
        appid = '4348910'
        publishedfileid = $WorkshopItemId.ToString()
        contentfolder = (Join-Path $output $name)
        title = $manifest.Name
        description = $description
        changenote = if ($ChangeNote) { $ChangeNote } else { "版本 $version" }
    }
    $previewName = 'workshop-preview' + $preview.Extension.ToLowerInvariant()
    Copy-Item -LiteralPath $preview.FullName -Destination (Join-Path $stage $previewName)
    $fields['previewfile'] = Join-Path $output $previewName
    if ($Visibility -or $WorkshopItemId -eq 0) {
        $fields['visibility'] = switch ($Visibility) {
            'Public' { '0' }
            'FriendsOnly' { '1' }
            default { '2' }
        }
    }
    Write-WorkshopVdf (Join-Path $stage 'workshop.vdf') $fields

    [IO.Directory]::Move($stage, $output)
    Write-Output "Release ${version}: $output"
} finally {
    if (Test-Path -LiteralPath $stage -PathType Container) {
        $resolved = (Resolve-Path -LiteralPath $stage).ProviderPath
        if ([IO.Path]::GetDirectoryName($resolved) -ceq $outputParent -and [IO.Path]::GetFileName($resolved) -ceq $stageName) {
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}
