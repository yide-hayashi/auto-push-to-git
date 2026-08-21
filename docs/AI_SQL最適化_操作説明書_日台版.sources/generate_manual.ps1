param(
    [Parameter(Mandatory = $false)]
    [string]$ContentPath = (Join-Path $PSScriptRoot 'manual-content.json'),

    [Parameter(Mandatory = $false)]
    [string]$ImageDirectory = (Join-Path $PSScriptRoot 'images'),

    [Parameter(Mandatory = $false)]
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Value
    )

    [System.IO.File]::WriteAllText($Path, $Value, [System.Text.UTF8Encoding]::new($false))
}

function New-ZipFromDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDirectory,
        [Parameter(Mandatory = $true)][string]$DestinationPath
    )

    Add-Type -AssemblyName System.IO.Compression
    $rootPath = [System.IO.Path]::GetFullPath($SourceDirectory).TrimEnd([char]92, [char]47)
    $fileStream = [System.IO.File]::Open($DestinationPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    $archive = [System.IO.Compression.ZipArchive]::new($fileStream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $rootPath -Recurse -File) {
            $relativePath = $file.FullName.Substring($rootPath.Length).TrimStart([char]92, [char]47).Replace([char]92, [char]47)
            $entry = $archive.CreateEntry($relativePath, [System.IO.Compression.CompressionLevel]::Optimal)
            $inputStream = [System.IO.File]::OpenRead($file.FullName)
            $entryStream = $entry.Open()
            try {
                $inputStream.CopyTo($entryStream)
            }
            finally {
                $entryStream.Dispose()
                $inputStream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
        $fileStream.Dispose()
    }
}

function Escape-Xml {
    param([AllowNull()][string]$Value)

    if ($null -eq $Value) {
        return ''
    }

    return [System.Security.SecurityElement]::Escape($Value)
}

function New-RoundedRectanglePath {
    param(
        [Parameter(Mandatory = $true)][System.Drawing.RectangleF]$Rectangle,
        [Parameter(Mandatory = $true)][single]$Radius
    )

    $diameter = $Radius * 2.0
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($Rectangle.X, $Rectangle.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Rectangle.Right - $diameter, $Rectangle.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Rectangle.Right - $diameter, $Rectangle.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rectangle.X, $Rectangle.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Draw-Arrow {
    param(
        [Parameter(Mandatory = $true)][System.Drawing.Graphics]$Graphics,
        [Parameter(Mandatory = $true)][System.Drawing.Color]$Color,
        [Parameter(Mandatory = $true)][single]$X1,
        [Parameter(Mandatory = $true)][single]$Y1,
        [Parameter(Mandatory = $true)][single]$X2,
        [Parameter(Mandatory = $true)][single]$Y2
    )

    $pen = [System.Drawing.Pen]::new($Color, 7.0)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $brush = [System.Drawing.SolidBrush]::new($Color)
    try {
        $Graphics.DrawLine($pen, $X1, $Y1, $X2, $Y2)
        $angle = [Math]::Atan2($Y2 - $Y1, $X2 - $X1)
        $arrowLength = 24.0
        $arrowWidth = 13.0
        $backX = $X2 - ($arrowLength * [Math]::Cos($angle))
        $backY = $Y2 - ($arrowLength * [Math]::Sin($angle))
        $perpX = $arrowWidth * [Math]::Sin($angle)
        $perpY = $arrowWidth * [Math]::Cos($angle)
        $points = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new($X2, $Y2),
            [System.Drawing.PointF]::new([single]($backX + $perpX), [single]($backY - $perpY)),
            [System.Drawing.PointF]::new([single]($backX - $perpX), [single]($backY + $perpY))
        )
        $Graphics.FillPolygon($brush, $points)
    }
    finally {
        $brush.Dispose()
        $pen.Dispose()
    }
}

function Draw-StepBox {
    param(
        [Parameter(Mandatory = $true)][System.Drawing.Graphics]$Graphics,
        [Parameter(Mandatory = $true)][System.Drawing.RectangleF]$Rectangle,
        [Parameter(Mandatory = $true)][int]$Number,
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][System.Drawing.Font]$TextFont,
        [Parameter(Mandatory = $true)][System.Drawing.Font]$NumberFont,
        [Parameter(Mandatory = $true)][bool]$Accent
    )

    $fillColor = if ($Accent) { [System.Drawing.Color]::FromArgb(255, 255, 246, 224) } else { [System.Drawing.Color]::FromArgb(255, 236, 245, 255) }
    $borderColor = if ($Accent) { [System.Drawing.Color]::FromArgb(255, 221, 126, 35) } else { [System.Drawing.Color]::FromArgb(255, 41, 103, 178) }
    $fill = [System.Drawing.SolidBrush]::new($fillColor)
    $border = [System.Drawing.Pen]::new($borderColor, 4.0)
    $numberFill = [System.Drawing.SolidBrush]::new($borderColor)
    $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $textBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 35, 48, 66))
    $path = New-RoundedRectanglePath -Rectangle $Rectangle -Radius 24.0
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    try {
        $Graphics.FillPath($fill, $path)
        $Graphics.DrawPath($border, $path)
        $circle = [System.Drawing.RectangleF]::new($Rectangle.X + 18.0, $Rectangle.Y + 18.0, 58.0, 58.0)
        $Graphics.FillEllipse($numberFill, $circle)
        $Graphics.DrawString($Number.ToString(), $NumberFont, $white, $circle, $format)
        $textRectangle = [System.Drawing.RectangleF]::new(
            $Rectangle.X + 78.0,
            $Rectangle.Y + 16.0,
            $Rectangle.Width - 94.0,
            $Rectangle.Height - 32.0)
        $Graphics.DrawString($Text, $TextFont, $textBrush, $textRectangle, $format)
    }
    finally {
        $format.Dispose()
        $path.Dispose()
        $textBrush.Dispose()
        $white.Dispose()
        $numberFill.Dispose()
        $border.Dispose()
        $fill.Dispose()
    }
}

function New-FlowDiagram {
    param(
        [Parameter(Mandatory = $true)]$Flow,
        [Parameter(Mandatory = $true)][string]$FontName,
        [Parameter(Mandatory = $true)][string]$OutputFile
    )

    Add-Type -AssemblyName System.Drawing
    $bitmap = [System.Drawing.Bitmap]::new(1600, 900, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
    $titleFont = [System.Drawing.Font]::new($FontName, 34.0, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $stepFont = [System.Drawing.Font]::new($FontName, 25.0, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $numberFont = [System.Drawing.Font]::new('Segoe UI', 28.0, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $smallFont = [System.Drawing.Font]::new($FontName, 23.0, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $dark = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 29, 48, 78))
    $muted = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 70, 79, 91))
    $loopFill = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 239, 248, 239))
    $loopBorder = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 51, 135, 76), 4.0)
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center

    try {
        $graphics.Clear([System.Drawing.Color]::White)
        $graphics.DrawString([string]$Flow.title, $titleFont, $dark, [System.Drawing.RectangleF]::new(40, 25, 1520, 70), $format)

        $boxes = @(
            [System.Drawing.RectangleF]::new(70, 140, 420, 180),
            [System.Drawing.RectangleF]::new(590, 140, 420, 180),
            [System.Drawing.RectangleF]::new(1110, 140, 420, 180),
            [System.Drawing.RectangleF]::new(1110, 420, 420, 180),
            [System.Drawing.RectangleF]::new(590, 420, 420, 180),
            [System.Drawing.RectangleF]::new(70, 420, 420, 180)
        )

        Draw-Arrow -Graphics $graphics -Color ([System.Drawing.Color]::FromArgb(255, 77, 101, 132)) -X1 500 -Y1 230 -X2 575 -Y2 230
        Draw-Arrow -Graphics $graphics -Color ([System.Drawing.Color]::FromArgb(255, 77, 101, 132)) -X1 1020 -Y1 230 -X2 1095 -Y2 230
        Draw-Arrow -Graphics $graphics -Color ([System.Drawing.Color]::FromArgb(255, 77, 101, 132)) -X1 1320 -Y1 330 -X2 1320 -Y2 405
        Draw-Arrow -Graphics $graphics -Color ([System.Drawing.Color]::FromArgb(255, 77, 101, 132)) -X1 1100 -Y1 510 -X2 1025 -Y2 510
        Draw-Arrow -Graphics $graphics -Color ([System.Drawing.Color]::FromArgb(255, 77, 101, 132)) -X1 580 -Y1 510 -X2 505 -Y2 510

        for ($index = 0; $index -lt 6; $index++) {
            Draw-StepBox -Graphics $graphics -Rectangle $boxes[$index] -Number ($index + 1) -Text ([string]$Flow.steps[$index]) -TextFont $stepFont -NumberFont $numberFont -Accent ($index -eq 4)
        }

        $loopRectangle = [System.Drawing.RectangleF]::new(250, 680, 1100, 100)
        $loopPath = New-RoundedRectanglePath -Rectangle $loopRectangle -Radius 20.0
        $graphics.FillPath($loopFill, $loopPath)
        $graphics.DrawPath($loopBorder, $loopPath)
        $graphics.DrawString([string]$Flow.loop, $smallFont, $muted, $loopRectangle, $format)
        Draw-Arrow -Graphics $graphics -Color ([System.Drawing.Color]::FromArgb(255, 51, 135, 76)) -X1 280 -Y1 610 -X2 420 -Y2 665
        Draw-Arrow -Graphics $graphics -Color ([System.Drawing.Color]::FromArgb(255, 51, 135, 76)) -X1 1350 -Y1 730 -X2 1450 -Y2 615
        $graphics.DrawString([string]$Flow.footer, $smallFont, $muted, [System.Drawing.RectangleF]::new(80, 815, 1440, 55), $format)

        $bitmap.Save($OutputFile, [System.Drawing.Imaging.ImageFormat]::Png)
        $loopPath.Dispose()
    }
    finally {
        $format.Dispose()
        $loopBorder.Dispose()
        $loopFill.Dispose()
        $muted.Dispose()
        $dark.Dispose()
        $smallFont.Dispose()
        $numberFont.Dispose()
        $stepFont.Dispose()
        $titleFont.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function New-RunXml {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Language,
        [Parameter(Mandatory = $true)][string]$Font,
        [Parameter(Mandatory = $false)][bool]$Bold = $false,
        [Parameter(Mandatory = $false)][string]$Color = '',
        [Parameter(Mandatory = $false)][int]$Size = 0
    )

    $fontValue = Escape-Xml $Font
    $langValue = Escape-Xml $Language
    $properties = "<w:rFonts w:ascii=`"$fontValue`" w:hAnsi=`"$fontValue`" w:eastAsia=`"$fontValue`"/>"
    if ($Bold) {
        $properties += '<w:b/>'
    }
    if ($Color.Length -gt 0) {
        $properties += "<w:color w:val=`"$(Escape-Xml $Color)`"/>"
    }
    if ($Size -gt 0) {
        $properties += "<w:sz w:val=`"$Size`"/><w:szCs w:val=`"$Size`"/>"
    }
    $properties += "<w:lang w:val=`"$langValue`" w:eastAsia=`"$langValue`"/>"
    $textValue = Escape-Xml $Text
    return "<w:r><w:rPr>$properties</w:rPr><w:t xml:space=`"preserve`">$textValue</w:t></w:r>"
}

function New-ParagraphXml {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Language,
        [Parameter(Mandatory = $true)][string]$Font,
        [Parameter(Mandatory = $false)][string]$Style = 'Normal',
        [Parameter(Mandatory = $false)][bool]$Bold = $false,
        [Parameter(Mandatory = $false)][string]$Color = '',
        [Parameter(Mandatory = $false)][int]$Size = 0,
        [Parameter(Mandatory = $false)][string]$Alignment = ''
    )

    $paragraphProperties = "<w:pStyle w:val=`"$(Escape-Xml $Style)`"/>"
    if ($Alignment.Length -gt 0) {
        $paragraphProperties += "<w:jc w:val=`"$(Escape-Xml $Alignment)`"/>"
    }
    $run = New-RunXml -Text $Text -Language $Language -Font $Font -Bold $Bold -Color $Color -Size $Size
    return "<w:p><w:pPr>$paragraphProperties</w:pPr>$run</w:p>"
}

function New-PageBreakXml {
    return '<w:p><w:r><w:br w:type="page"/></w:r></w:p>'
}

function New-CalloutXml {
    param(
        [Parameter(Mandatory = $true)]$Block,
        [Parameter(Mandatory = $true)][string]$Language,
        [Parameter(Mandatory = $true)][string]$Font,
        [Parameter(Mandatory = $true)][bool]$Warning
    )

    $fill = if ($Warning) { 'FFF2CC' } else { 'EAF3F8' }
    $border = if ($Warning) { 'C88B00' } else { '3D7EA6' }
    $titleColor = if ($Warning) { '8A5400' } else { '225A78' }
    $builder = [System.Text.StringBuilder]::new()
    [void]$builder.Append("<w:tbl><w:tblPr><w:tblW w:w=`"0`" w:type=`"auto`"/><w:tblBorders><w:top w:val=`"single`" w:sz=`"10`" w:color=`"$border`"/><w:left w:val=`"single`" w:sz=`"10`" w:color=`"$border`"/><w:bottom w:val=`"single`" w:sz=`"10`" w:color=`"$border`"/><w:right w:val=`"single`" w:sz=`"10`" w:color=`"$border`"/></w:tblBorders><w:tblCellMar><w:top w:w=`"120`" w:type=`"dxa`"/><w:left w:w=`"160`" w:type=`"dxa`"/><w:bottom w:w=`"120`" w:type=`"dxa`"/><w:right w:w=`"160`" w:type=`"dxa`"/></w:tblCellMar></w:tblPr><w:tblGrid><w:gridCol w:w=`"9200`"/></w:tblGrid><w:tr><w:tc><w:tcPr><w:tcW w:w=`"9200`" w:type=`"dxa`"/><w:shd w:val=`"clear`" w:color=`"auto`" w:fill=`"$fill`"/></w:tcPr>")
    if ($null -ne $Block.title -and ([string]$Block.title).Length -gt 0) {
        [void]$builder.Append((New-ParagraphXml -Text ([string]$Block.title) -Language $Language -Font $Font -Style 'CalloutTitle' -Bold $true -Color $titleColor))
    }
    if ($null -ne $Block.text -and ([string]$Block.text).Length -gt 0) {
        [void]$builder.Append((New-ParagraphXml -Text ([string]$Block.text) -Language $Language -Font $Font -Style 'CalloutText'))
    }
    if ($null -ne $Block.items) {
        foreach ($item in $Block.items) {
            [void]$builder.Append((New-ParagraphXml -Text ("- " + [string]$item) -Language $Language -Font $Font -Style 'CalloutText'))
        }
    }
    [void]$builder.Append('</w:tc></w:tr></w:tbl>')
    return $builder.ToString()
}

function New-TableXml {
    param(
        [Parameter(Mandatory = $true)]$Headers,
        [Parameter(Mandatory = $true)]$Rows,
        [Parameter(Mandatory = $true)][string]$Language,
        [Parameter(Mandatory = $true)][string]$Font,
        [Parameter(Mandatory = $false)][bool]$Troubleshooting = $false
    )

    $columnCount = $Headers.Count
    if ($Troubleshooting -and $columnCount -eq 3) {
        $widths = @(2000, 2500, 4700)
    }
    else {
        $baseWidth = [Math]::Floor(9200 / $columnCount)
        $widths = @()
        for ($index = 0; $index -lt $columnCount; $index++) {
            $widths += $baseWidth
        }
    }

    $builder = [System.Text.StringBuilder]::new()
    [void]$builder.Append('<w:tbl><w:tblPr><w:tblW w:w="9200" w:type="dxa"/><w:tblBorders><w:top w:val="single" w:sz="6" w:color="7A8794"/><w:left w:val="single" w:sz="6" w:color="7A8794"/><w:bottom w:val="single" w:sz="6" w:color="7A8794"/><w:right w:val="single" w:sz="6" w:color="7A8794"/><w:insideH w:val="single" w:sz="4" w:color="B8C0C8"/><w:insideV w:val="single" w:sz="4" w:color="B8C0C8"/></w:tblBorders><w:tblLayout w:type="fixed"/><w:tblCellMar><w:top w:w="80" w:type="dxa"/><w:left w:w="100" w:type="dxa"/><w:bottom w:w="80" w:type="dxa"/><w:right w:w="100" w:type="dxa"/></w:tblCellMar></w:tblPr><w:tblGrid>')
    foreach ($width in $widths) {
        [void]$builder.Append("<w:gridCol w:w=`"$width`"/>")
    }
    [void]$builder.Append('</w:tblGrid><w:tr><w:trPr><w:tblHeader/></w:trPr>')
    for ($index = 0; $index -lt $columnCount; $index++) {
        [void]$builder.Append("<w:tc><w:tcPr><w:tcW w:w=`"$($widths[$index])`" w:type=`"dxa`"/><w:shd w:val=`"clear`" w:color=`"auto`" w:fill=`"D9EAF7`"/></w:tcPr>")
        [void]$builder.Append((New-ParagraphXml -Text ([string]$Headers[$index]) -Language $Language -Font $Font -Style 'TableHeader' -Bold $true))
        [void]$builder.Append('</w:tc>')
    }
    [void]$builder.Append('</w:tr>')

    $rowIndex = 0
    foreach ($row in $Rows) {
        $rowFill = if (($rowIndex % 2) -eq 1) { 'F7F9FB' } else { 'FFFFFF' }
        [void]$builder.Append('<w:tr>')
        for ($columnIndex = 0; $columnIndex -lt $columnCount; $columnIndex++) {
            $cellText = if ($columnIndex -lt $row.Count) { [string]$row[$columnIndex] } else { '' }
            [void]$builder.Append("<w:tc><w:tcPr><w:tcW w:w=`"$($widths[$columnIndex])`" w:type=`"dxa`"/><w:shd w:val=`"clear`" w:color=`"auto`" w:fill=`"$rowFill`"/></w:tcPr>")
            [void]$builder.Append((New-ParagraphXml -Text $cellText -Language $Language -Font $Font -Style 'TableText'))
            [void]$builder.Append('</w:tc>')
        }
        [void]$builder.Append('</w:tr>')
        $rowIndex++
    }
    [void]$builder.Append('</w:tbl>')
    return $builder.ToString()
}

$script:DrawingId = 1

function New-ImageXml {
    param(
        [Parameter(Mandatory = $true)][string]$RelationshipId,
        [Parameter(Mandatory = $true)][string]$ImagePath,
        [Parameter(Mandatory = $true)][string]$ImageName
    )

    Add-Type -AssemblyName System.Drawing
    $image = [System.Drawing.Image]::FromFile($ImagePath)
    try {
        $cx = [long]5600000
        $cy = [long][Math]::Round($cx * $image.Height / $image.Width)
    }
    finally {
        $image.Dispose()
    }

    $drawingId = $script:DrawingId
    $script:DrawingId++
    $nameValue = Escape-Xml $ImageName
    return @"
<w:p><w:pPr><w:pStyle w:val="Image"/><w:jc w:val="center"/></w:pPr><w:r><w:drawing><wp:inline distT="0" distB="0" distL="0" distR="0"><wp:extent cx="$cx" cy="$cy"/><wp:effectExtent l="0" t="0" r="0" b="0"/><wp:docPr id="$drawingId" name="$nameValue" descr="$nameValue"/><wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect="1"/></wp:cNvGraphicFramePr><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic><pic:nvPicPr><pic:cNvPr id="$drawingId" name="$nameValue"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed="$RelationshipId"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="$cx" cy="$cy"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>
"@
}

function Get-StylesXml {
    return @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:docDefaults>
    <w:rPrDefault><w:rPr><w:rFonts w:ascii="Yu Gothic UI" w:hAnsi="Yu Gothic UI" w:eastAsia="Yu Gothic UI"/><w:sz w:val="20"/><w:szCs w:val="20"/></w:rPr></w:rPrDefault>
    <w:pPrDefault><w:pPr><w:spacing w:after="120" w:line="300" w:lineRule="auto"/></w:pPr></w:pPrDefault>
  </w:docDefaults>
  <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:qFormat/><w:pPr><w:spacing w:after="120" w:line="300" w:lineRule="auto"/></w:pPr><w:rPr><w:sz w:val="20"/><w:szCs w:val="20"/></w:rPr></w:style>
  <w:style w:type="paragraph" w:styleId="Title"><w:name w:val="Title"/><w:basedOn w:val="Normal"/><w:qFormat/><w:pPr><w:spacing w:before="1200" w:after="300"/><w:jc w:val="center"/></w:pPr><w:rPr><w:b/><w:color w:val="17365D"/><w:sz w:val="38"/><w:szCs w:val="38"/></w:rPr></w:style>
  <w:style w:type="paragraph" w:styleId="Subtitle"><w:name w:val="Subtitle"/><w:basedOn w:val="Normal"/><w:qFormat/><w:pPr><w:spacing w:after="260"/><w:jc w:val="center"/></w:pPr><w:rPr><w:color w:val="44546A"/><w:sz w:val="24"/><w:szCs w:val="24"/></w:rPr></w:style>
  <w:style w:type="paragraph" w:styleId="PartTitle"><w:name w:val="Part Title"/><w:basedOn w:val="Normal"/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before="600" w:after="360"/><w:jc w:val="center"/></w:pPr><w:rPr><w:b/><w:color w:val="17365D"/><w:sz w:val="34"/><w:szCs w:val="34"/></w:rPr></w:style>
  <w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:uiPriority w:val="9"/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before="260" w:after="180"/><w:outlineLvl w:val="0"/></w:pPr><w:rPr><w:b/><w:color w:val="1F4E79"/><w:sz w:val="30"/><w:szCs w:val="30"/></w:rPr></w:style>
  <w:style w:type="paragraph" w:styleId="Heading2"><w:name w:val="heading 2"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before="220" w:after="140"/><w:outlineLvl w:val="1"/></w:pPr><w:rPr><w:b/><w:color w:val="2F5597"/><w:sz w:val="25"/><w:szCs w:val="25"/></w:rPr></w:style>
  <w:style w:type="paragraph" w:styleId="ListBullet"><w:name w:val="List Bullet"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="80"/><w:ind w:left="520" w:hanging="260"/></w:pPr></w:style>
  <w:style w:type="paragraph" w:styleId="ListNumber"><w:name w:val="List Number"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="80"/><w:ind w:left="520" w:hanging="260"/></w:pPr></w:style>
  <w:style w:type="paragraph" w:styleId="Caption"><w:name w:val="Caption"/><w:basedOn w:val="Normal"/><w:pPr><w:keepNext/><w:spacing w:after="180"/><w:jc w:val="center"/></w:pPr><w:rPr><w:i/><w:color w:val="595959"/><w:sz w:val="18"/><w:szCs w:val="18"/></w:rPr></w:style>
  <w:style w:type="paragraph" w:styleId="Image"><w:name w:val="Image"/><w:basedOn w:val="Normal"/><w:pPr><w:keepNext/><w:spacing w:before="100" w:after="60"/><w:jc w:val="center"/></w:pPr></w:style>
  <w:style w:type="paragraph" w:styleId="CalloutTitle"><w:name w:val="Callout Title"/><w:basedOn w:val="Normal"/><w:pPr><w:keepNext/><w:spacing w:after="80"/></w:pPr><w:rPr><w:b/><w:sz w:val="21"/><w:szCs w:val="21"/></w:rPr></w:style>
  <w:style w:type="paragraph" w:styleId="CalloutText"><w:name w:val="Callout Text"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="60" w:line="270" w:lineRule="auto"/></w:pPr><w:rPr><w:sz w:val="19"/><w:szCs w:val="19"/></w:rPr></w:style>
  <w:style w:type="paragraph" w:styleId="TableHeader"><w:name w:val="Table Header"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="0" w:line="240" w:lineRule="auto"/></w:pPr><w:rPr><w:b/><w:sz w:val="17"/><w:szCs w:val="17"/></w:rPr></w:style>
  <w:style w:type="paragraph" w:styleId="TableText"><w:name w:val="Table Text"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="0" w:line="230" w:lineRule="auto"/></w:pPr><w:rPr><w:sz w:val="16"/><w:szCs w:val="16"/></w:rPr></w:style>
</w:styles>
'@
}

function Get-SettingsXml {
    return @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:settings xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:zoom w:percent="100"/>
  <w:defaultTabStop w:val="720"/>
  <w:characterSpacingControl w:val="doNotCompress"/>
  <w:compat><w:compatSetting w:name="compatibilityMode" w:uri="http://schemas.microsoft.com/office/word" w:val="15"/></w:compat>
</w:settings>
'@
}

function Invoke-OpenXmlValidation {
    param([Parameter(Mandatory = $true)][string]$DocumentPath)

    $packageRoot = Join-Path $env:USERPROFILE '.nuget\packages'
    $frameworkDll = Join-Path $packageRoot 'documentformat.openxml.framework\3.5.1\lib\net46\DocumentFormat.OpenXml.Framework.dll'
    $openXmlDll = Join-Path $packageRoot 'documentformat.openxml\3.5.1\lib\net46\DocumentFormat.OpenXml.dll'
    if (-not (Test-Path -LiteralPath $frameworkDll) -or -not (Test-Path -LiteralPath $openXmlDll)) {
        throw 'DocumentFormat.OpenXml 3.5.1 is required in the local NuGet cache for validation.'
    }

    Add-Type -AssemblyName WindowsBase
    if ($null -eq ('DocumentFormat.OpenXml.OpenXmlElement' -as [type])) {
        [void][System.Reflection.Assembly]::LoadFrom($frameworkDll)
    }
    if ($null -eq ('DocumentFormat.OpenXml.Packaging.WordprocessingDocument' -as [type])) {
        [void][System.Reflection.Assembly]::LoadFrom($openXmlDll)
    }

    $document = [DocumentFormat.OpenXml.Packaging.WordprocessingDocument]::Open($DocumentPath, $false)
    try {
        $validator = [DocumentFormat.OpenXml.Validation.OpenXmlValidator]::new()
        $errors = @($validator.Validate($document))
        if ($errors.Count -gt 0) {
            $message = ($errors | Select-Object -First 20 | ForEach-Object { $_.Description }) -join [Environment]::NewLine
            throw "Open XML validation failed with $($errors.Count) error(s):$([Environment]::NewLine)$message"
        }
    }
    finally {
        $document.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $ContentPath)) {
    throw "Manual content was not found: $ContentPath"
}

$content = Get-Content -LiteralPath $ContentPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($OutputPath.Length -eq 0) {
    $OutputPath = Join-Path (Split-Path $PSScriptRoot -Parent) ([string]$content.document.outputFile)
}
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force -Path $ImageDirectory | Out-Null

New-FlowDiagram -Flow $content.flows.'ja-JP' -FontName 'Yu Gothic UI' -OutputFile (Join-Path $ImageDirectory 'flow-ja.png')
New-FlowDiagram -Flow $content.flows.'zh-TW' -FontName 'Microsoft JhengHei' -OutputFile (Join-Path $ImageDirectory 'flow-zh-tw.png')

$requiredImages = @('actual-input-draft.png', 'actual-awaiting-user.png', 'flow-ja.png', 'flow-zh-tw.png')
foreach ($requiredImage in $requiredImages) {
    $requiredPath = Join-Path $ImageDirectory $requiredImage
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required manual image was not found: $requiredPath"
    }
}

$manualTempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("SqlOptimizerManual-" + [Guid]::NewGuid().ToString('N'))
$packageRoot = Join-Path $manualTempRoot 'package'
New-Item -ItemType Directory -Path $packageRoot | Out-Null
New-Item -ItemType Directory -Path (Join-Path $packageRoot '_rels') | Out-Null
New-Item -ItemType Directory -Path (Join-Path $packageRoot 'docProps') | Out-Null
New-Item -ItemType Directory -Path (Join-Path $packageRoot 'word') | Out-Null
New-Item -ItemType Directory -Path (Join-Path $packageRoot 'word\_rels') | Out-Null
New-Item -ItemType Directory -Path (Join-Path $packageRoot 'word\media') | Out-Null

try {
    foreach ($requiredImage in $requiredImages) {
        Copy-Item -LiteralPath (Join-Path $ImageDirectory $requiredImage) -Destination (Join-Path $packageRoot ("word\media\" + $requiredImage))
    }

    $imageRelationships = [ordered]@{
        'actual-input-draft.png' = 'rId4'
        'actual-awaiting-user.png' = 'rId5'
        'flow-ja.png' = 'rId6'
        'flow-zh-tw.png' = 'rId7'
    }

    $documentBuilder = [System.Text.StringBuilder]::new()
    [void]$documentBuilder.Append('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>')
    [void]$documentBuilder.Append('<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"><w:body>')

    [void]$documentBuilder.Append((New-ParagraphXml -Text ([string]$content.document.title) -Language 'ja-JP' -Font 'Yu Gothic UI' -Style 'Title' -Bold $true -Alignment 'center'))
    [void]$documentBuilder.Append((New-ParagraphXml -Text ([string]$content.document.subtitle) -Language 'zh-TW' -Font 'Microsoft JhengHei' -Style 'Subtitle' -Alignment 'center'))
    [void]$documentBuilder.Append((New-ParagraphXml -Text ("Version " + [string]$content.document.version + " / " + [string]$content.document.issued) -Language 'ja-JP' -Font 'Yu Gothic UI' -Style 'Subtitle' -Alignment 'center'))
    [void]$documentBuilder.Append((New-ParagraphXml -Text ([string]$content.document.classification) -Language 'zh-TW' -Font 'Microsoft JhengHei' -Style 'Subtitle' -Bold $true -Color '9C6500' -Alignment 'center'))
    $coverNotice = [PSCustomObject]@{ title = [string]$content.document.coverCalloutTitle; text = [string]$content.document.notice; items = $null }
    [void]$documentBuilder.Append((New-CalloutXml -Block $coverNotice -Language 'ja-JP' -Font 'Yu Gothic UI' -Warning $true))
    [void]$documentBuilder.Append((New-PageBreakXml))

    [void]$documentBuilder.Append((New-ParagraphXml -Text ([string]$content.document.tocHeading) -Language 'ja-JP' -Font 'Yu Gothic UI' -Style 'Heading1' -Bold $true))
    foreach ($tocPart in $content.toc) {
        $tocFont = if ([string]$tocPart.lang -eq 'zh-TW') { 'Microsoft JhengHei' } else { 'Yu Gothic UI' }
        [void]$documentBuilder.Append((New-ParagraphXml -Text ([string]$tocPart.part) -Language ([string]$tocPart.lang) -Font $tocFont -Style 'Heading2' -Bold $true))
        foreach ($chapterTitle in $tocPart.chapters) {
            [void]$documentBuilder.Append((New-ParagraphXml -Text ([string]$chapterTitle) -Language ([string]$tocPart.lang) -Font $tocFont -Style 'ListBullet'))
        }
    }

    $partIndex = 0
    foreach ($part in $content.parts) {
        [void]$documentBuilder.Append((New-PageBreakXml))
        [void]$documentBuilder.Append((New-ParagraphXml -Text ([string]$part.heading) -Language ([string]$part.lang) -Font ([string]$part.font) -Style 'PartTitle' -Bold $true -Alignment 'center'))

        $chapterIndex = 0
        foreach ($chapter in $part.chapters) {
            if ($chapterIndex -gt 0) {
                [void]$documentBuilder.Append((New-PageBreakXml))
            }
            $chapterHeading = [string]$content.toc[$partIndex].chapters[$chapterIndex]
            [void]$documentBuilder.Append((New-ParagraphXml -Text $chapterHeading -Language ([string]$part.lang) -Font ([string]$part.font) -Style 'Heading1' -Bold $true))

            foreach ($block in $chapter.blocks) {
                switch ([string]$block.type) {
                    'paragraph' {
                        [void]$documentBuilder.Append((New-ParagraphXml -Text ([string]$block.text) -Language ([string]$part.lang) -Font ([string]$part.font) -Style 'Normal'))
                    }
                    'steps' {
                        $stepNumber = 1
                        foreach ($item in $block.items) {
                            [void]$documentBuilder.Append((New-ParagraphXml -Text ("$stepNumber. " + [string]$item) -Language ([string]$part.lang) -Font ([string]$part.font) -Style 'ListNumber'))
                            $stepNumber++
                        }
                    }
                    'warning' {
                        [void]$documentBuilder.Append((New-CalloutXml -Block $block -Language ([string]$part.lang) -Font ([string]$part.font) -Warning $true))
                    }
                    'note' {
                        $noteBlock = [PSCustomObject]@{ title = [string]$part.noteTitle; text = [string]$block.text; items = $null }
                        [void]$documentBuilder.Append((New-CalloutXml -Block $noteBlock -Language ([string]$part.lang) -Font ([string]$part.font) -Warning $false))
                    }
                    'table' {
                        [void]$documentBuilder.Append((New-TableXml -Headers $block.headers -Rows $block.rows -Language ([string]$part.lang) -Font ([string]$part.font)))
                    }
                    'troubleshooting' {
                        [void]$documentBuilder.Append((New-TableXml -Headers $block.headers -Rows $block.rows -Language ([string]$part.lang) -Font ([string]$part.font) -Troubleshooting $true))
                    }
                    'image' {
                        $imageKey = [string]$block.key
                        $imagePath = Join-Path $ImageDirectory $imageKey
                        [void]$documentBuilder.Append((New-ImageXml -RelationshipId ([string]$imageRelationships[$imageKey]) -ImagePath $imagePath -ImageName $imageKey))
                        [void]$documentBuilder.Append((New-ParagraphXml -Text ([string]$block.caption) -Language ([string]$part.lang) -Font ([string]$part.font) -Style 'Caption' -Alignment 'center'))
                    }
                    default {
                        throw "Unknown block type: $($block.type)"
                    }
                }
            }
            $chapterIndex++
        }
        $partIndex++
    }

    [void]$documentBuilder.Append('<w:sectPr><w:footerReference w:type="default" r:id="rId3"/><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1080" w:right="1250" w:bottom="1080" w:left="1250" w:header="720" w:footer="720" w:gutter="0"/><w:cols w:space="720"/><w:docGrid w:linePitch="360"/></w:sectPr></w:body></w:document>')

    Write-Utf8NoBom -Path (Join-Path $packageRoot 'word\document.xml') -Value $documentBuilder.ToString()
    Write-Utf8NoBom -Path (Join-Path $packageRoot 'word\styles.xml') -Value (Get-StylesXml)
    Write-Utf8NoBom -Path (Join-Path $packageRoot 'word\settings.xml') -Value (Get-SettingsXml)

    $footerXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:p><w:pPr><w:jc w:val="center"/></w:pPr><w:r><w:rPr><w:rFonts w:ascii="Yu Gothic UI" w:hAnsi="Yu Gothic UI" w:eastAsia="Yu Gothic UI"/><w:color w:val="777777"/><w:sz w:val="16"/><w:lang w:val="ja-JP" w:eastAsia="ja-JP"/></w:rPr><w:t>AI SQL Optimizer - Internal / Page </w:t></w:r><w:fldSimple w:instr="PAGE"><w:r><w:rPr><w:color w:val="777777"/><w:sz w:val="16"/></w:rPr><w:t>1</w:t></w:r></w:fldSimple></w:p></w:ftr>
'@
    Write-Utf8NoBom -Path (Join-Path $packageRoot 'word\footer1.xml') -Value $footerXml

    $documentRelationships = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings" Target="settings.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer" Target="footer1.xml"/>
  <Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/actual-input-draft.png"/>
  <Relationship Id="rId5" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/actual-awaiting-user.png"/>
  <Relationship Id="rId6" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/flow-ja.png"/>
  <Relationship Id="rId7" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/flow-zh-tw.png"/>
</Relationships>
'@
    Write-Utf8NoBom -Path (Join-Path $packageRoot 'word\_rels\document.xml.rels') -Value $documentRelationships

    $rootRelationships = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
</Relationships>
'@
    Write-Utf8NoBom -Path (Join-Path $packageRoot '_rels\.rels') -Value $rootRelationships

    $contentTypes = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Default Extension="png" ContentType="image/png"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
  <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
  <Override PartName="/word/settings.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"/>
  <Override PartName="/word/footer1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml"/>
  <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
  <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
</Types>
'@
    Write-Utf8NoBom -Path (Join-Path $packageRoot '[Content_Types].xml') -Value $contentTypes

    $createdUtc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    $coreProperties = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><dc:title>$(Escape-Xml ([string]$content.document.title))</dc:title><dc:subject>WinForms AI SQL Optimizer user manual</dc:subject><dc:creator>AutoCallGit project</dc:creator><cp:keywords>AI SQL Optimizer; WinForms; Codex; MCP; Oracle</cp:keywords><dc:description>Bilingual Japanese and Taiwan Mandarin operation manual.</dc:description><cp:lastModifiedBy>AutoCallGit project</cp:lastModifiedBy><dcterms:created xsi:type="dcterms:W3CDTF">$createdUtc</dcterms:created><dcterms:modified xsi:type="dcterms:W3CDTF">$createdUtc</dcterms:modified></cp:coreProperties>
"@
    Write-Utf8NoBom -Path (Join-Path $packageRoot 'docProps\core.xml') -Value $coreProperties

    $appProperties = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes"><Application>AutoCallGit Manual Generator</Application><DocSecurity>0</DocSecurity><ScaleCrop>false</ScaleCrop><Company></Company><LinksUpToDate>false</LinksUpToDate><SharedDoc>false</SharedDoc><HyperlinksChanged>false</HyperlinksChanged><AppVersion>1.0</AppVersion></Properties>
'@
    Write-Utf8NoBom -Path (Join-Path $packageRoot 'docProps\app.xml') -Value $appProperties

    if (Test-Path -LiteralPath $OutputPath) {
        Remove-Item -LiteralPath $OutputPath -Force
    }
    New-ZipFromDirectory -SourceDirectory $packageRoot -DestinationPath $OutputPath

    Invoke-OpenXmlValidation -DocumentPath $OutputPath

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zipArchive = [System.IO.Compression.ZipFile]::OpenRead($OutputPath)
    try {
        $relationshipEntries = @($zipArchive.Entries | Where-Object { $_.FullName -like '*.rels' })
        foreach ($entry in $relationshipEntries) {
            $reader = [System.IO.StreamReader]::new($entry.Open(), [System.Text.Encoding]::UTF8)
            try {
                $relationshipText = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }
            if ($relationshipText -match 'TargetMode\s*=\s*["'']External["'']' -or $relationshipText -match 'Target\s*=\s*["'']https?://') {
                throw "External relationship was detected in $($entry.FullName)."
            }
        }
        $macroEntries = @($zipArchive.Entries | Where-Object { $_.FullName -match '(?i)vba|macro' })
        if ($macroEntries.Count -gt 0) {
            throw 'Macro-related package content was detected.'
        }
    }
    finally {
        $zipArchive.Dispose()
    }

    Write-Output "Generated and validated: $OutputPath"
}
finally {
    if (Test-Path -LiteralPath $manualTempRoot) {
        Remove-Item -LiteralPath $manualTempRoot -Recurse -Force
    }
}
