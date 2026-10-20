<#
    构建脚本：编译 DeskWidget.exe
    用法：powershell -ExecutionPolicy Bypass -File build.ps1
    产物：DeskWidget.exe
#>
param([switch]$Clean)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out  = Join-Path $root 'DeskWidget.exe'
$ico  = Join-Path $root 'app.ico'
$srcDir = Join-Path $root 'src'

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "找不到 csc.exe：$csc（需要 .NET Framework 4.x）" }

if ($Clean -and (Test-Path $out)) { Remove-Item $out -Force; Write-Host "已清理旧产物" }

# 正在运行的实例会锁定 exe
Get-Process DeskWidget -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "停止正在运行的实例 PID $($_.Id)"
    Stop-Process -Id $_.Id -Force
    Start-Sleep -Milliseconds 500
}

# WPF 程序集在 GAC 中，需给出完整路径
$refs = @(
    'C:\Windows\Microsoft.NET\assembly\GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll',
    'C:\Windows\Microsoft.NET\assembly\GAC_64\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll',
    'C:\Windows\Microsoft.NET\assembly\GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll',
    'C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll'
)
foreach ($r in $refs) { if (-not (Test-Path $r)) { throw "缺少引用程序集：$r" } }

$sources = Get-ChildItem $srcDir -Filter *.cs | ForEach-Object { '"' + $_.FullName + '"' }
if ($sources.Count -eq 0) { throw "src 目录下没有 .cs 文件" }

$args = @('/nologo', '/target:winexe', '/platform:x64', '/utf8output', ('/out:"' + $out + '"'))
if (Test-Path $ico) { $args += ('/win32icon:"' + $ico + '"') }
# 应用清单：声明 asInvoker，避免"无法验证发布者"的提权提示
$manifest = Join-Path $root 'app.manifest'
if (Test-Path $manifest) { $args += ('/win32manifest:"' + $manifest + '"') }
foreach ($r in $refs) { $args += ('/r:"' + $r + '"') }
$args += $sources

Write-Host "编译中…"
& $csc $args 2>&1 | Where-Object { $_ -notmatch 'warning CS' } | ForEach-Object { Write-Host $_ }

if (Test-Path $out) {
    $kb = [math]::Round((Get-Item $out).Length / 1KB, 1)
    Write-Host "构建成功：DeskWidget.exe（$kb KB）" -ForegroundColor Green
} else {
    throw "构建失败"
}
