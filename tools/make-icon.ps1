# Draws the Tamewin logo (blue tile, white shield, check mark) and writes:
#   src\Tamewin\app.ico   (16-256 px, used as the EXE and window icon)
#   docs\logo.png         (256 px, for the README)
# Run it again after changing the drawing below.
Add-Type -AssemblyName System.Drawing
$repo = Split-Path $PSScriptRoot -Parent

function New-Logo([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
    $g.ScaleTransform($size / 256.0, $size / 256.0)   # draw in a 256 x 256 space

    # Tile: rounded square with a blue gradient
    $tile = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = 56; $x = 8; $w = 240
    $tile.AddArc($x, $x, $r, $r, 180, 90); $tile.AddArc($x + $w - $r, $x, $r, $r, 270, 90)
    $tile.AddArc($x + $w - $r, $x + $w - $r, $r, $r, 0, 90); $tile.AddArc($x, $x + $w - $r, $r, $r, 90, 90)
    $tile.CloseFigure()
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point 256, 256),
        ([System.Drawing.Color]::FromArgb(255, 58, 150, 221)), ([System.Drawing.Color]::FromArgb(255, 0, 78, 156))
    $g.FillPath($grad, $tile)

    # Shield
    $shield = New-Object System.Drawing.Drawing2D.GraphicsPath
    $shield.AddLine(128, 46, 198, 72); $shield.AddLine(198, 72, 198, 126)
    $shield.AddBezier(198, 126, 198, 172, 168, 198, 128, 216)
    $shield.AddBezier(128, 216, 88, 198, 58, 172, 58, 126)
    $shield.AddLine(58, 126, 58, 72); $shield.CloseFigure()
    $g.FillPath([System.Drawing.Brushes]::White, $shield)

    # Check mark
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 0, 95, 184)), 20
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $g.DrawLines($pen, [System.Drawing.Point[]]@((New-Object System.Drawing.Point 94, 132), (New-Object System.Drawing.Point 119, 157), (New-Object System.Drawing.Point 164, 106)))

    $g.Dispose()
    return $bmp
}

function Get-PngBytes($bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $ms.ToArray()
}

# .ico file: header, one directory entry per size, then each image as PNG
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = foreach ($s in $sizes) { $b = New-Logo $s; , (Get-PngBytes $b); $b.Dispose() }
$out = New-Object System.IO.MemoryStream
$wr = New-Object System.IO.BinaryWriter $out
$wr.Write([uint16]0); $wr.Write([uint16]1); $wr.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $images[$i].Length
    $wr.Write([byte]($s % 256)); $wr.Write([byte]($s % 256)); $wr.Write([byte]0); $wr.Write([byte]0)
    $wr.Write([uint16]1); $wr.Write([uint16]32); $wr.Write([uint32]$len); $wr.Write([uint32]$offset)
    $offset += $len
}
foreach ($img in $images) { $wr.Write($img) }
$wr.Flush()
$ico = Join-Path $repo 'src\Tamewin\app.ico'
[IO.File]::WriteAllBytes($ico, $out.ToArray())

$logo = New-Logo 256
$logo.Save((Join-Path $repo 'docs\logo.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$logo.Dispose()
"Wrote $ico and docs\logo.png"
