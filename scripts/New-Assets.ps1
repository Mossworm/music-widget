param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
foreach ($entry in @(@('StoreLogo.png',50), @('SmallLogo.png',44), @('Logo.png',150))) {
    $size = [int]$entry[1]
    $bitmap = New-Object System.Drawing.Bitmap($size,$size)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#DE555B'))
    $g.FillEllipse($brush, [single]($size*.05), [single]($size*.05), [single]($size*.9), [single]($size*.9))
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, [single]($size*.035))
    $g.DrawEllipse($pen,[single]($size*.2),[single]($size*.2),[single]($size*.6),[single]($size*.6))
    $points = [System.Drawing.PointF[]]@([System.Drawing.PointF]::new($size*.43,$size*.34),[System.Drawing.PointF]::new($size*.43,$size*.66),[System.Drawing.PointF]::new($size*.68,$size*.5))
    $g.FillPolygon([System.Drawing.Brushes]::White,$points)
    $bitmap.Save((Join-Path $Destination $entry[0]), [System.Drawing.Imaging.ImageFormat]::Png)
    $pen.Dispose(); $brush.Dispose(); $g.Dispose(); $bitmap.Dispose()
}
