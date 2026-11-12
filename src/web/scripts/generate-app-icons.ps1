# Renderiza el SVG de formas simples con GDI+ de Windows, sin dependencias externas.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$brandPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../public/brand'))
[xml]$source = Get-Content -LiteralPath (Join-Path $brandPath 'app-icon-orange.svg') -Raw
$background = [Drawing.ColorTranslator]::FromHtml($source.svg.rect.fill)
$foreground = [Drawing.ColorTranslator]::FromHtml($source.svg.g.stroke)
$culture = [Globalization.CultureInfo]::InvariantCulture

function Export-AppIcon([int]$size, [string]$name, [single]$symbolScale) {
  $canvasSize = 2048
  $canvas = [Drawing.Bitmap]::new($canvasSize, $canvasSize)
  $graphics = [Drawing.Graphics]::FromImage($canvas)
  $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $graphics.Clear($background)
  $graphics.ScaleTransform($canvasSize / 512, $canvasSize / 512)
  $graphics.TranslateTransform(256, 256)
  $graphics.ScaleTransform($symbolScale, $symbolScale)
  $graphics.TranslateTransform(-256, -256)
  try {
    foreach ($shape in $source.svg.g.ChildNodes) {
      if ($shape.NodeType -ne [Xml.XmlNodeType]::Element) { continue }
      $pen = [Drawing.Pen]::new($foreground, [single]::Parse($shape.GetAttribute('stroke-width'), $culture))
      $pen.StartCap = $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
      $pen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
      try {
        switch ($shape.LocalName) {
          'line' {
            $graphics.DrawLine($pen,
              [single]::Parse($shape.x1, $culture), [single]::Parse($shape.y1, $culture),
              [single]::Parse($shape.x2, $culture), [single]::Parse($shape.y2, $culture))
          }
          'polyline' {
            [Drawing.PointF[]]$points = foreach ($pair in ($shape.points -split ' ')) {
              $coordinates = $pair -split ','
              [Drawing.PointF]::new([single]::Parse($coordinates[0], $culture), [single]::Parse($coordinates[1], $culture))
            }
            $graphics.DrawLines($pen, $points)
          }
          default { throw "Forma SVG no soportada: $($shape.LocalName)" }
        }
      } finally { $pen.Dispose() }
    }
    $output = [Drawing.Bitmap]::new($size, $size)
    $scaled = [Drawing.Graphics]::FromImage($output)
    try {
      $scaled.Clear($background)
      $scaled.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
      $scaled.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
      $scaled.DrawImage($canvas, [Drawing.Rectangle]::new(0, 0, $size, $size))
      $output.Save((Join-Path $brandPath $name), [Drawing.Imaging.ImageFormat]::Png)
    } finally { $scaled.Dispose(); $output.Dispose() }
  } finally { $graphics.Dispose(); $canvas.Dispose() }
}

Export-AppIcon 32 'favicon-orange-32.png' 1.12
Export-AppIcon 48 'favicon-orange-48.png' 1.12
Export-AppIcon 180 'apple-touch-icon-orange.png' 1.12
Export-AppIcon 192 'app-icon-orange-192.png' 1.12
Export-AppIcon 512 'app-icon-orange-512.png' 1.12
Export-AppIcon 192 'app-icon-orange-maskable-192.png' 1
Export-AppIcon 512 'app-icon-orange-maskable-512.png' 1

# ICO con dos imágenes PNG: también cubre los navegadores que consultan /favicon.ico.
$iconPath = [IO.Path]::GetFullPath((Join-Path $brandPath '../favicon.ico'))
$stream = [IO.File]::Create($iconPath)
$writer = [IO.BinaryWriter]::new($stream)
try {
  $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]2)
  $images = @(32, 48) | ForEach-Object { ,([IO.File]::ReadAllBytes((Join-Path $brandPath "favicon-orange-$_.png"))) }
  $offset = 6 + 16 * 2
  for ($index = 0; $index -lt 2; $index++) {
    $size = @(32, 48)[$index]
    $writer.Write([byte]$size); $writer.Write([byte]$size)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$index].Length); $writer.Write([uint32]$offset)
    $offset += $images[$index].Length
  }
  foreach ($bytes in $images) { $writer.Write([byte[]]$bytes) }
} finally { $writer.Dispose(); $stream.Dispose() }
Write-Output 'Íconos naranja generados desde app-icon-orange.svg.'
