$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputDir = Join-Path $scriptRoot "..\Resources\Generated"
[System.IO.Directory]::CreateDirectory($outputDir) | Out-Null

function Get-FontFamily {
    param(
        [string[]]$Candidates
    )

    $installed = New-Object System.Drawing.Text.InstalledFontCollection
    foreach ($candidate in $Candidates) {
        foreach ($family in $installed.Families) {
            if ($family.Name -eq $candidate) {
                return $family
            }
        }
    }

    return [System.Drawing.FontFamily]::GenericSansSerif
}

function New-Bitmap {
    param(
        [int]$Width,
        [int]$Height
    )

    return New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
}

function Save-Png {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path
    )

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $Bitmap.Dispose()
}

function Scale-Nearest {
    param(
        [System.Drawing.Bitmap]$Source,
        [int]$Width,
        [int]$Height
    )

    $result = New-Bitmap -Width $Width -Height $Height
    $graphics = [System.Drawing.Graphics]::FromImage($result)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.DrawImage($Source, 0, 0, $Width, $Height)
    $graphics.Dispose()
    $Source.Dispose()
    return $result
}

function Set-HighQualityGraphics {
    param(
        [System.Drawing.Graphics]$Graphics
    )

    $Graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $Graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $Graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $Graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $Graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
}

function Add-Noise {
    param(
        [System.Drawing.Graphics]$Graphics,
        [int]$Width,
        [int]$Height,
        [int]$Count,
        [System.Random]$Random,
        [System.Drawing.Color[]]$Colors,
        [int]$MinSize = 1,
        [int]$MaxSize = 4
    )

    for ($i = 0; $i -lt $Count; $i++) {
        $brush = New-Object System.Drawing.SolidBrush($Colors[$Random.Next(0, $Colors.Length)])
        $size = $Random.Next($MinSize, $MaxSize + 1)
        $x = $Random.Next(0, [Math]::Max(1, $Width - $size))
        $y = $Random.Next(0, [Math]::Max(1, $Height - $size))
        $Graphics.FillRectangle($brush, $x, $y, $size, $size)
        $brush.Dispose()
    }
}

function Draw-InkWisp {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Random]$Random,
        [int]$Width,
        [int]$Height,
        [System.Drawing.Color]$Color,
        [int]$PenWidth
    )

    $pen = New-Object System.Drawing.Pen -ArgumentList $Color, $PenWidth
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $points = @()
    $pointCount = 4 + $Random.Next(0, 3)
    for ($i = 0; $i -lt $pointCount; $i++) {
        $points += New-Object System.Drawing.PointF(
            [float]($Random.NextDouble() * $Width),
            [float]($Random.NextDouble() * $Height))
    }

    $Graphics.DrawCurve($pen, $points, 0.65)
    $pen.Dispose()
}

function Build-TextPath {
    param(
        [string]$Text,
        [System.Drawing.FontFamily]$FontFamily,
        [float]$FontSize,
        [int]$CanvasWidth,
        [int]$CanvasHeight,
        [bool]$Compact,
        [System.Drawing.FontStyle]$FontStyle = [System.Drawing.FontStyle]::Regular
    )

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $path.AddString($Text, $FontFamily, [int]$FontStyle, $FontSize, (New-Object System.Drawing.PointF(0, 0)), $format)

    $bounds = $path.GetBounds()
    $targetWidth = if ($Compact) { $CanvasWidth * 0.78 } else { $CanvasWidth * 0.84 }
    $targetHeight = if ($Compact) { $CanvasHeight * 0.52 } else { $CanvasHeight * 0.62 }
    $fitScale = [Math]::Min($targetWidth / $bounds.Width, $targetHeight / $bounds.Height)

    $scaleMatrix = New-Object System.Drawing.Drawing2D.Matrix
    $scaleMatrix.Scale([float]$fitScale, [float]$fitScale)
    $path.Transform($scaleMatrix)
    $bounds = $path.GetBounds()

    $translateMatrix = New-Object System.Drawing.Drawing2D.Matrix
    $translateMatrix.Translate(
        [float](($CanvasWidth - $bounds.Width) * 0.5 - $bounds.X),
        [float](($CanvasHeight - $bounds.Height) * 0.5 - $bounds.Y))
    $path.Transform($translateMatrix)

    return $path
}

function Write-BarkText {
    param(
        [string]$Text,
        [string]$OutFile,
        [int]$OutWidth,
        [int]$OutHeight,
        [float]$FontSize,
        [System.Random]$Random,
        [System.Drawing.FontFamily]$FontFamily,
        [bool]$Compact = $false,
        [System.Drawing.FontStyle]$FontStyle = [System.Drawing.FontStyle]::Regular
    )

    $scale = 4
    $hiWidth = $OutWidth * $scale
    $hiHeight = $OutHeight * $scale
    $bitmap = New-Bitmap -Width $hiWidth -Height $hiHeight
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    Set-HighQualityGraphics -Graphics $graphics
    $graphics.Clear([System.Drawing.Color]::Transparent)

    for ($i = 0; $i -lt 12; $i++) {
        $inkColor = [System.Drawing.Color]::FromArgb(
            $Random.Next(10, 32),
            18 + $Random.Next(0, 14),
            14 + $Random.Next(0, 12),
            18 + $Random.Next(0, 18))
        Draw-InkWisp -Graphics $graphics -Random $Random -Width $hiWidth -Height $hiHeight -Color $inkColor -PenWidth ($scale * (5 + $Random.Next(0, 8)))
    }

    $path = Build-TextPath -Text $Text -FontFamily $FontFamily -FontSize ($FontSize * $scale) -CanvasWidth $hiWidth -CanvasHeight $hiHeight -Compact $Compact -FontStyle $FontStyle

    $glowBrushA = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(52, 0, 255, 224))
    $glowBrushB = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(40, 255, 0, 174))
    $graphics.FillPath($glowBrushA, $path)
    $graphics.TranslateTransform(6, 2)
    $graphics.FillPath($glowBrushB, $path)
    $graphics.ResetTransform()
    $glowBrushA.Dispose()
    $glowBrushB.Dispose()

    $graphics.SetClip($path)
    $baseBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 62, 45, 31))
    $graphics.FillPath($baseBrush, $path)
    $baseBrush.Dispose()

    for ($x = -$hiWidth; $x -lt ($hiWidth * 2); $x += (24 + $Random.Next(0, 16))) {
        $grainColor = [System.Drawing.Color]::FromArgb(
            110 + $Random.Next(0, 40),
            110 + $Random.Next(0, 30),
            79 + $Random.Next(0, 24),
            50 + $Random.Next(0, 18))
        $pen = New-Object System.Drawing.Pen -ArgumentList $grainColor, (12 + $Random.Next(0, 10))
        $graphics.DrawLine($pen, $x, 0, $x + 110, $hiHeight)
        $pen.Dispose()
    }

    for ($i = 0; $i -lt 9; $i++) {
        $glowColor = if (($i % 2) -eq 0) {
            [System.Drawing.Color]::FromArgb(148, 0, 255, 228)
        }
        else {
            [System.Drawing.Color]::FromArgb(128, 255, 0, 170)
        }

        $startX = [float]($hiWidth * (0.15 + $Random.NextDouble() * 0.7))
        $startY = [float]($hiHeight * (0.25 + $Random.NextDouble() * 0.4))
        $midX = $startX + [float](($Random.NextDouble() - 0.5) * $hiWidth * 0.18)
        $midY = $startY + [float](($Random.NextDouble() - 0.5) * $hiHeight * 0.22)
        $endX = $midX + [float](($Random.NextDouble() - 0.5) * $hiWidth * 0.12)
        $endY = $midY + [float](($Random.NextDouble() - 0.5) * $hiHeight * 0.16)

        $glowPen = New-Object System.Drawing.Pen -ArgumentList $glowColor, (16 + $Random.Next(0, 8))
        $glowPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $glowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $graphics.DrawBezier($glowPen, $startX, $startY, $midX, $midY, $midX, $midY, $endX, $endY)
        $glowPen.Dispose()

        $crackPen = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::FromArgb(210, 20, 18, 18)), (4 + $Random.Next(0, 3))
        $crackPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $crackPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $graphics.DrawBezier($crackPen, $startX, $startY, $midX, $midY, $midX, $midY, $endX, $endY)
        $crackPen.Dispose()
    }

    Add-Noise -Graphics $graphics -Width $hiWidth -Height $hiHeight -Count 420 -Random $Random -Colors @(
        [System.Drawing.Color]::FromArgb(52, 0, 255, 224),
        [System.Drawing.Color]::FromArgb(42, 255, 0, 170),
        [System.Drawing.Color]::FromArgb(36, 255, 255, 255),
        [System.Drawing.Color]::FromArgb(42, 30, 18, 12)
    ) -MinSize 2 -MaxSize 6

    $graphics.ResetClip()

    $outlinePen = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::FromArgb(230, 22, 13, 10)), 18
    $highlightPen = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::FromArgb(210, 170, 128, 76)), 5
    $graphics.DrawPath($outlinePen, $path)
    $graphics.DrawPath($highlightPen, $path)
    $outlinePen.Dispose()
    $highlightPen.Dispose()

    $graphics.Dispose()

    $finalBitmap = Scale-Nearest -Source $bitmap -Width $OutWidth -Height $OutHeight
    Save-Png -Bitmap $finalBitmap -Path (Join-Path $outputDir $OutFile)
}

function Write-Backdrop {
    param(
        [string]$OutFile,
        [System.Random]$Random
    )

    $scale = 4
    $outWidth = 640
    $outHeight = 360
    $hiWidth = $outWidth * $scale
    $hiHeight = $outHeight * $scale

    $bitmap = New-Bitmap -Width $hiWidth -Height $hiHeight
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    Set-HighQualityGraphics -Graphics $graphics

    $rect = New-Object System.Drawing.Rectangle(0, 0, $hiWidth, $hiHeight)
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point(0, 0)),
        (New-Object System.Drawing.Point(0, $hiHeight)),
        [System.Drawing.Color]::FromArgb(255, 18, 18, 22),
        [System.Drawing.Color]::FromArgb(255, 71, 58, 48))

    $gradientBlend = New-Object System.Drawing.Drawing2D.ColorBlend
    $gradientBlend.Colors = @(
        [System.Drawing.Color]::FromArgb(255, 18, 18, 22),
        [System.Drawing.Color]::FromArgb(255, 39, 39, 44),
        [System.Drawing.Color]::FromArgb(255, 65, 56, 50),
        [System.Drawing.Color]::FromArgb(255, 44, 31, 28)
    )
    $gradientBlend.Positions = @(0.0, 0.45, 0.75, 1.0)
    $gradient.InterpolationColors = $gradientBlend
    $graphics.FillRectangle($gradient, $rect)
    $gradient.Dispose()

    $hazeBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point(0, [int]($hiHeight * 0.52))),
        (New-Object System.Drawing.Point(0, [int]($hiHeight * 0.82))),
        [System.Drawing.Color]::FromArgb(0, 0, 255, 221),
        [System.Drawing.Color]::FromArgb(90, 255, 0, 176))
    $graphics.FillRectangle($hazeBrush, 0, [int]($hiHeight * 0.56), $hiWidth, [int]($hiHeight * 0.18))
    $hazeBrush.Dispose()

    for ($i = 0; $i -lt 26; $i++) {
        $wispColor = if (($i % 3) -eq 0) {
            [System.Drawing.Color]::FromArgb(18 + $Random.Next(0, 12), 0, 255, 228)
        }
        else {
            [System.Drawing.Color]::FromArgb(16 + $Random.Next(0, 12), 255, 0, 176)
        }

        Draw-InkWisp -Graphics $graphics -Random $Random -Width $hiWidth -Height $hiHeight -Color $wispColor -PenWidth ($scale * (10 + $Random.Next(0, 12)))
    }

    $groundBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 24, 19, 18))
    $silhouetteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 16, 13, 14))
    $graphics.FillRectangle($groundBrush, 0, [int]($hiHeight * 0.72), $hiWidth, [int]($hiHeight * 0.28))

    for ($i = 0; $i -lt 14; $i++) {
        $x = $Random.Next(0, $hiWidth)
        $w = 28 + $Random.Next(0, 60)
        $h = 60 + $Random.Next(0, 160)
        $graphics.FillRectangle($silhouetteBrush, $x, [int]($hiHeight * 0.72) - $h, $w, $h)
    }

    $treePen = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::FromArgb(255, 12, 10, 9)), 54
    $treePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $treePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $centerX = [int]($hiWidth * 0.62)
    $baseY = [int]($hiHeight * 0.78)
    $graphics.DrawLine($treePen, $centerX, $baseY, $centerX - 28, [int]($hiHeight * 0.32))
    $graphics.DrawLine($treePen, $centerX - 20, [int]($hiHeight * 0.44), $centerX - 240, [int]($hiHeight * 0.26))
    $graphics.DrawLine($treePen, $centerX - 30, [int]($hiHeight * 0.52), $centerX + 190, [int]($hiHeight * 0.31))
    $graphics.DrawLine($treePen, $centerX - 18, [int]($hiHeight * 0.39), $centerX + 120, [int]($hiHeight * 0.18))
    $graphics.DrawLine($treePen, $centerX - 32, [int]($hiHeight * 0.34), $centerX - 180, [int]($hiHeight * 0.12))
    $graphics.DrawLine($treePen, $centerX - 18, [int]($hiHeight * 0.32), $centerX + 32, [int]($hiHeight * 0.08))
    $treePen.Dispose()

    $rootPen = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::FromArgb(255, 12, 10, 9)), 24
    $rootPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $rootPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($rootPen, $centerX - 6, $baseY, $centerX - 120, [int]($hiHeight * 0.88))
    $graphics.DrawLine($rootPen, $centerX + 6, $baseY, $centerX + 124, [int]($hiHeight * 0.86))
    $graphics.DrawLine($rootPen, $centerX - 8, $baseY, $centerX + 34, [int]($hiHeight * 0.93))
    $rootPen.Dispose()

    for ($i = 0; $i -lt 72; $i++) {
        $brush = New-Object System.Drawing.SolidBrush(
            [System.Drawing.Color]::FromArgb(
                22 + $Random.Next(0, 28),
                235 + $Random.Next(0, 20),
                235 + $Random.Next(0, 20),
                235 + $Random.Next(0, 20)))
        $size = 5 + $Random.Next(0, 16)
        $x = [int]($hiWidth * 0.46) + $Random.Next(-220, 220)
        $y = [int]($hiHeight * 0.48) + $Random.Next(-100, 70)
        $graphics.FillRectangle($brush, $x, $y, $size, $size)
        $brush.Dispose()
    }

    Add-Noise -Graphics $graphics -Width $hiWidth -Height $hiHeight -Count 1800 -Random $Random -Colors @(
        [System.Drawing.Color]::FromArgb(22, 255, 255, 255),
        [System.Drawing.Color]::FromArgb(16, 0, 255, 224),
        [System.Drawing.Color]::FromArgb(16, 255, 0, 170),
        [System.Drawing.Color]::FromArgb(22, 24, 24, 28)
    ) -MinSize 2 -MaxSize 7

    $graphics.Dispose()

    $finalBitmap = Scale-Nearest -Source $bitmap -Width $outWidth -Height $outHeight
    Save-Png -Bitmap $finalBitmap -Path (Join-Path $outputDir $OutFile)
}

$random = New-Object System.Random 24681357
$fontFamily = Get-FontFamily -Candidates @(
    "楷体",
    "KaiTi",
    "KaiTi_GB2312",
    "STKaiti",
    "Kaiti SC",
    "Microsoft YaHei UI",
    "Microsoft YaHei",
    "SimHei",
    "Source Han Sans CN",
    "Noto Sans CJK SC"
)

$titleText = ([string][char]0x5EFA) + ([string][char]0x6728) + ([string][char]0x884C) + ([string][char]0x8005)
$startText = ([string][char]0x5F00) + ([string][char]0x59CB) + ([string][char]0x6E38) + ([string][char]0x620F)
$continueText = ([string][char]0x7EE7) + ([string][char]0x7EED) + ([string][char]0x6E38) + ([string][char]0x620F)
$quitText = ([string][char]0x9000) + ([string][char]0x51FA) + ([string][char]0x6E38) + ([string][char]0x620F)

Write-BarkText -Text $titleText -OutFile "JianmuTitleLogo.png" -OutWidth 512 -OutHeight 192 -FontSize 208 -Random $random -FontFamily $fontFamily -FontStyle ([System.Drawing.FontStyle]::Regular)
Write-BarkText -Text $startText -OutFile "StartGameLabel.png" -OutWidth 256 -OutHeight 72 -FontSize 112 -Random $random -FontFamily $fontFamily -Compact $true -FontStyle ([System.Drawing.FontStyle]::Regular)
Write-BarkText -Text $continueText -OutFile "ContinueGameLabel.png" -OutWidth 256 -OutHeight 72 -FontSize 112 -Random $random -FontFamily $fontFamily -Compact $true -FontStyle ([System.Drawing.FontStyle]::Regular)
Write-BarkText -Text $quitText -OutFile "QuitGameLabel.png" -OutWidth 256 -OutHeight 72 -FontSize 112 -Random $random -FontFamily $fontFamily -Compact $true -FontStyle ([System.Drawing.FontStyle]::Regular)
Write-Backdrop -OutFile "JianmuOpeningBackdrop.png" -Random $random

Write-Host "Generated art files in $outputDir"
