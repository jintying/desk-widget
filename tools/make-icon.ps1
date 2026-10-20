<#
    生成 app.ico
    设计：圆角方块 + 蓝色渐变底 + 白色日历/清单图形
    图形用 Segoe Fluent Icons（Win11）/ Segoe MDL2 Assets（Win10）字体绘制
    尺寸：16 / 24 / 32 / 48 / 64 / 128 / 256，全部使用 BMP 帧（GDI+ 兼容性最好）
#>
param(
    [string]$OutFile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'app.ico'),
    [string]$Glyph = '&#xE787;'    # Segoe 日历图标；换图形改这里
)

Add-Type -AssemblyName System.Drawing

# ---- 选字体 ----
$installed = (New-Object System.Drawing.Text.InstalledFontCollection).Families | Select-Object -ExpandProperty Name
$fontName = 'Segoe MDL2 Assets'
if ($installed -contains 'Segoe Fluent Icons') { $fontName = 'Segoe Fluent Icons' }

# ---- 候选字形（可在 README 里换） ----
# E787 日历 | E8FD 清单 | EA3B 任务 | E9D5 清单勾选 | E73A 收藏
$glyphChar = [char]([Convert]::ToInt32('E787', 16))

$sizes = @(16, 24, 32, 48, 64, 128)
$frames = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    # 圆角底：蓝色渐变
    $r = [Math]::Max(3, [int]($s * 0.22))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $r * 2, $r * 2, 180, 90)
    $path.AddArc($s - $r * 2, 0, $r * 2, $r * 2, 270, 90)
    $path.AddArc($s - $r * 2, $s - $r * 2, $r * 2, $r * 2, 0, 90)
    $path.AddArc(0, $s - $r * 2, $r * 2, $r * 2, 90, 90)
    $path.CloseFigure()

    $c1 = [System.Drawing.Color]::FromArgb(255, 0x4F, 0x9C, 0xF7)
    $c2 = [System.Drawing.Color]::FromArgb(255, 0x25, 0x63, 0xEB)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point 0, 0),
        (New-Object System.Drawing.Point $s, $s),
        $c1, $c2)
    $g.FillPath($brush, $path)
    $brush.Dispose()

    # 白色字形
    $fsize = [single]($s * 0.62)
    if ($s -le 24) { $fsize = [single]($s * 0.70) }
    $font = New-Object System.Drawing.Font($fontName, $fsize, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $txt = [string]$glyphChar
    $sz = $g.MeasureString($txt, $font, 1000, [System.Drawing.StringFormat]::GenericTypographic)
    $x = [single](($s - $sz.Width) / 2)
    $y = [single](($s - $sz.Height) / 2 + $s * 0.02)
    $fmt = [System.Drawing.StringFormat]::GenericTypographic
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $g.DrawString($txt, $font, $white, $x, $y, $fmt)
    $white.Dispose()
    $font.Dispose()
    $g.Dispose()
    $path.Dispose()

    # 转成 32bppBGRA 的 BMP 帧数据（ICO 内嵌 DIB）
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms

    $bw.Write([UInt32]40)                 # biSize
    $bw.Write([Int32]$s)                  # biWidth
    $bw.Write([Int32]($s * 2))            # biHeight（含 XOR + AND 掩码）
    $bw.Write([UInt16]1)                  # biPlanes
    $bw.Write([UInt16]32)                 # biBitCount
    $bw.Write([UInt32]0)                  # biCompression = BI_RGB
    $bw.Write([UInt32]($s * $s * 4))      # biSizeImage
    $bw.Write([Int32]0); $bw.Write([Int32]0)
    $bw.Write([UInt32]0); $bw.Write([UInt32]0)

    # XOR 位图：自下而上，BGRA
    for ($y2 = $s - 1; $y2 -ge 0; $y2--) {
        for ($x2 = 0; $x2 -lt $s; $x2++) {
            $px = $bmp.GetPixel($x2, $y2)
            $bw.Write([Byte]$px.B); $bw.Write([Byte]$px.G); $bw.Write([Byte]$px.R); $bw.Write([Byte]$px.A)
        }
    }
    # AND 掩码：每行按 4 字节对齐，全 0（透明由 alpha 决定）
    $rowBytes = [int][Math]::Ceiling($s / 32.0) * 4
    $mask = New-Object byte[] ($rowBytes * $s)
    $bw.Write($mask)

    $bw.Flush()
    $frames += ,@($s, $ms.ToArray())
    $bw.Dispose(); $ms.Dispose(); $bmp.Dispose()
}

# ---- 写 ICO 文件 ----
$fs = [System.IO.File]::Create($OutFile)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$frames.Count)

$offset = 6 + 16 * $frames.Count
$entries = @()
foreach ($f in $frames) {
    $s = $f[0]; $data = $f[1]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $entries += ,@($dim, $data.Length, $offset)
    $offset += $data.Length
}
foreach ($e in $entries) {
    $bw.Write([Byte]$e[0]); $bw.Write([Byte]$e[0])
    $bw.Write([Byte]0); $bw.Write([Byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$e[1]); $bw.Write([UInt32]$e[2])
}
foreach ($f in $frames) { $bw.Write($f[1]) }
$bw.Close(); $fs.Close()

# ---- 校验 ----
Add-Type -AssemblyName System.Drawing
$check = New-Object System.Drawing.Icon $OutFile
Write-Host ("生成完成：" + $OutFile)
Write-Host ("文件大小：" + [math]::Round((Get-Item $OutFile).Length / 1KB, 1) + " KB")
Write-Host ("默认帧尺寸：" + $check.Width + "x" + $check.Height + "，句柄有效：" + ($check.Handle -ne [IntPtr]::Zero))
$check.Dispose()
