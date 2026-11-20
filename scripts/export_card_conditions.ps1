param(
    [string]$ProjectRoot = (Join-Path $PSScriptRoot '..\ProbablyTradingCards')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$artRoot = Join-Path $ProjectRoot 'Textures\Cards'
for ($index = 0; $index -lt 9; $index++) {
    foreach ($condition in @(0,1)) {
        $overlayName = if ($condition -eq 0) { 'wear_creased.png' } else { 'wear_light.png' }
        $baseImage = [System.Drawing.Image]::FromFile((Join-Path $artRoot "card_$index.png"))
        $wearImage = [System.Drawing.Image]::FromFile((Join-Path $artRoot $overlayName))
        $composite = [System.Drawing.Bitmap]::new($baseImage)
        $graphics = [System.Drawing.Graphics]::FromImage($composite)
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
            $graphics.DrawImageUnscaled($wearImage, 0, 0)
            $composite.Save((Join-Path $artRoot "card_${index}_wear_$condition.png"),[System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $graphics.Dispose()
            $composite.Dispose()
            $wearImage.Dispose()
            $baseImage.Dispose()
        }
    }
}
Write-Output 'Exported 18 condition variants from the approved card artwork and damage layers'
