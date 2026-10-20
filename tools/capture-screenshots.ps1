param(
    [string]$ExePath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'DeskWidget.exe'),
    [string]$OutDir  = (Join-Path (Split-Path $PSScriptRoot -Parent) 'screenshots')
)
Add-Type -AssemblyName System.Drawing
$sig = @"
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class Cap {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int m);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  public struct RECT { public int L, T, R, B; }
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static List<IntPtr> WindowsOf(uint pid) {
    var list = new List<IntPtr>();
    EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid) list.Add(h); return true; }, IntPtr.Zero);
    return list;
  }
  public static string TitleOf(IntPtr h) { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString(); }
}
"@
Add-Type -TypeDefinition $sig -ErrorAction SilentlyContinue
function Get-Rect($h) { $r = New-Object Cap+RECT; [void][Cap]::GetWindowRect($h, [ref]$r); return @{ W = ($r.R - $r.L); H = ($r.B - $r.T) } }
function Capture($h, $path) {
    $rc = Get-Rect $h
    if ($rc.W -le 0 -or $rc.H -le 0) { return }
    $bmp = New-Object System.Drawing.Bitmap $rc.W, $rc.H
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    $ok = [Cap]::PrintWindow($h, $hdc, 2)
    $g.ReleaseHdc($hdc); $g.Dispose()
    if ($ok) { $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); Write-Host ("  保存 " + (Split-Path $path -Leaf) + " $($rc.W)x$($rc.H)") }
    $bmp.Dispose()
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Get-Process DeskWidget -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force }
Start-Sleep -Seconds 1
$dir = Split-Path $ExePath -Parent

"启动挂件（--notray）…"
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $ExePath; $psi.Arguments = '--notray'; $psi.UseShellExecute = $false; $psi.WorkingDirectory = $dir
$proc = [System.Diagnostics.Process]::Start($psi)
Start-Sleep -Seconds 5
$widget = [IntPtr]::Zero
foreach ($h in [Cap]::WindowsOf([uint32]$proc.Id)) { $rc = Get-Rect $h; if ([Cap]::IsWindowVisible($h) -and $rc.W -ge 300 -and $rc.W -le 500 -and $rc.H -ge 400) { $widget = $h; break } }
if ($widget -ne [IntPtr]::Zero) {
    Capture $widget (Join-Path $OutDir 'widget.png')
    "折叠成胶囊…"
    [void][Cap]::PostMessage($widget, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
    Start-Sleep -Seconds 2
    foreach ($h in [Cap]::WindowsOf([uint32]$proc.Id)) { $rc = Get-Rect $h; if ([Cap]::IsWindowVisible($h) -and $rc.W -ge 40 -and $rc.W -le 90 -and $rc.H -ge 40 -and $rc.H -le 90) { Capture $h (Join-Path $OutDir 'pill.png'); break } }
}
Get-Process DeskWidget -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force }
Start-Sleep -Seconds 1

"打开设置页（--notray --settings）…"
$psi2 = New-Object System.Diagnostics.ProcessStartInfo
$psi2.FileName = $ExePath; $psi2.Arguments = '--notray --settings'; $psi2.UseShellExecute = $false; $psi2.WorkingDirectory = $dir
$proc2 = [System.Diagnostics.Process]::Start($psi2)
Start-Sleep -Seconds 6
$got = $false
foreach ($h in [Cap]::WindowsOf([uint32]$proc2.Id)) { if ([Cap]::TitleOf($h) -like '*设置*' -and [Cap]::IsWindowVisible($h)) { Capture $h (Join-Path $OutDir 'settings.png'); $got = $true; break } }
if (-not $got) { Write-Warning "没找到设置窗口" }
Get-Process DeskWidget -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force }
"完成。"