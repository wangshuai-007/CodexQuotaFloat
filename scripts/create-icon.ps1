$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$path = Join-Path $root "src/QuotaFloat/Assets/App.ico"
$sizes = @(16,24,32,48,64,128,256)
$images = @()
foreach ($size in $sizes) {
    $bitmap = New-Object System.Drawing.Bitmap($size,$size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 256.0),($size / 256.0))
    $box = New-Object System.Drawing.Drawing2D.GraphicsPath
    $box.AddArc(8,8,80,80,180,90)
    $box.AddArc(168,8,80,80,270,90)
    $box.AddArc(168,168,80,80,0,90)
    $box.AddArc(8,168,80,80,90,90)
    $box.CloseFigure()
    $back = New-Object System.Drawing.Drawing2D.LinearGradientBrush([System.Drawing.Point]::new(8,8),[System.Drawing.Point]::new(248,248),[System.Drawing.ColorTranslator]::FromHtml("#143E4B"),[System.Drawing.ColorTranslator]::FromHtml("#277584"))
    $graphics.FillPath($back,$box)
    $pill = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pill.AddArc(80,28,96,96,180,180)
    $pill.AddArc(80,132,96,96,0,180)
    $pill.CloseFigure()
    $graphics.FillPath([System.Drawing.Brushes]::WhiteSmoke,$pill)
    $graphics.SetClip($pill)
    $water = New-Object System.Drawing.Drawing2D.GraphicsPath
    $water.AddBezier(80,130,112,116,144,145,176,127)
    $water.AddLine(176,127,176,240)
    $water.AddLine(176,240,80,240)
    $water.CloseFigure()
    $fill = New-Object System.Drawing.Drawing2D.LinearGradientBrush([System.Drawing.Point]::new(80,125),[System.Drawing.Point]::new(80,228),[System.Drawing.ColorTranslator]::FromHtml("#80CFD1"),[System.Drawing.ColorTranslator]::FromHtml("#3F9EAE"))
    $graphics.FillPath($fill,$water)
    $graphics.ResetClip()
    foreach ($line in @(@(86,60,"#287CBA"),@(105,48,"#148568"),@(124,32,"#B76621"))) {
        $pen = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml($line[2]),6)
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $graphics.DrawLine($pen,98,[int]$line[0],(98+[int]$line[1]),[int]$line[0])
        $pen.Dispose()
    }
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,$stream.ToArray()
    if ($size -eq 256) { $bitmap.Save((Join-Path $root "artifacts/app-icon.png"),[System.Drawing.Imaging.ImageFormat]::Png) }
    $stream.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
    $box.Dispose(); $pill.Dispose(); $water.Dispose(); $back.Dispose(); $fill.Dispose()
}
$file = [System.IO.File]::Create($path)
$writer = New-Object System.IO.BinaryWriter($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($index = 0; $index -lt $sizes.Count; $index++) {
    $dimension = $sizes[$index] % 256
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$index].Length); $writer.Write([uint32]$offset)
    $offset += $images[$index].Length
}
foreach ($bytes in $images) { $writer.Write([byte[]]$bytes) }
$writer.Dispose(); $file.Dispose()
