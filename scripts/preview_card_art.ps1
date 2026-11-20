param(
    [string]$ProjectRoot = (Join-Path $PSScriptRoot '..\ProbablyTradingCards'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\art-preview')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetRoot = Join-Path $ProjectRoot 'Textures'
$previewRoot = $OutputDirectory
New-Item -ItemType Directory -Path $previewRoot -Force | Out-Null
$names = @('巷口跑腿（C）','废铁拾荒者（C）','夜班修理工（C）','自动售货机（C）','义体快递员（R）','黑市中间人（R）','轨道巡警（R）','霓虹女王（SR）','空间站之心（SR）')
$font = [System.Drawing.Font]::new('Microsoft YaHei', 14)
$brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(224,218,203))

foreach ($preview in @(@{File='preview-all.png'; Cards=@(0,1,2,3,4,5,6,7,8)}, @{File='preview-rarity.png'; Cards=@(2,6,7)})) {
    $rows = [int][Math]::Ceiling($preview.Cards.Count / 3.0)
    $sheet = [System.Drawing.Bitmap]::new(900, $rows * 340)
    $graphics = [System.Drawing.Graphics]::FromImage($sheet)
    $graphics.Clear([System.Drawing.Color]::FromArgb(40,35,46))
    try {
        for ($position = 0; $position -lt $preview.Cards.Count; $position++) {
            $index = $preview.Cards[$position]
            $left = ($position % 3) * 300
            $top = [int][Math]::Floor($position / 3) * 340
            $art = [System.Drawing.Image]::FromFile((Join-Path $assetRoot "Cards\card_$index.png"))
            $icon = [System.Drawing.Image]::FromFile((Join-Path $assetRoot "Items\card_$index.png"))
            try {
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.DrawImage($art, [System.Drawing.Rectangle]::new($left+16,$top+12,184,276))
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
                $graphics.DrawImage($icon, [System.Drawing.Rectangle]::new($left+218,$top+84,64,128))
                $graphics.DrawString($names[$index], $font, $brush, $left+16, $top+302)
            }
            finally {
                $art.Dispose()
                $icon.Dispose()
            }
        }
        $sheet.Save((Join-Path $previewRoot $preview.File), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $sheet.Dispose()
    }
}
$font.Dispose()
$brush.Dispose()
