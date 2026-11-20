param(
    [string]$ProjectRoot = (Join-Path $PSScriptRoot '..\ProbablyTradingCards')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$cardFiles = foreach ($index in 0..8) {
    foreach ($suffix in @('', '_wear_0', '_wear_1')) {
        Get-Item -LiteralPath (Join-Path $ProjectRoot "Textures\Cards\card_$index$suffix.png")
    }
}
foreach ($sourceFile in $cardFiles) {
    $spriteKey = $sourceFile.BaseName
    $sourcePath = $sourceFile.FullName
    $targetPath = Join-Path $ProjectRoot "Textures\Items\$spriteKey.png"
    $sourceImage = [System.Drawing.Image]::FromFile($sourcePath)
    $icon = [System.Drawing.Bitmap]::new(16, 32, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($icon)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $destination = [System.Drawing.Rectangle]::new(0, 4, 16, 24)
        $graphics.DrawImage($sourceImage, $destination)
        $icon.Save($targetPath, [System.Drawing.Imaging.ImageFormat]::Png)
        if ($icon.GetPixel(8, 8).A -eq 0 -or $icon.GetPixel(8, 24).A -eq 0) {
            throw "Inventory grid center is transparent in $spriteKey"
        }
        Write-Output "Exported $spriteKey 16x32 from the final card artwork"
    }
    finally {
        $graphics.Dispose()
        $icon.Dispose()
        $sourceImage.Dispose()
    }
}
