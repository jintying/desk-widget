<#
    构建 DeskWidget.exe
    用法：powershell -ExecutionPolicy Bypass -File build.ps1 [-Clean]

    设计要点：
    - 自动查找 csc.exe 与 WPF 引用程序集（不同环境 GAC 布局不同，不能写死）
    - 失败时打印完整的编译器输出，便于在 CI 日志里定位
#>
param([switch]$Clean)

$ErrorActionPreference = 'Stop'
$root   = $PSScriptRoot
$out    = Join-Path $root 'DeskWidget.exe'
$ico    = Join-Path $root 'app.ico'
$manifest = Join-Path $root 'app.manifest'
$srcDir = Join-Path $root 'src'

Write-Host "=== 环境 ==="
Write-Host ("PowerShell : " + $PSVersionTable.PSVersion)
Write-Host ("OS         : " + [System.Environment]::OSVersion.VersionString)
Write-Host ("64 位进程  : " + [Environment]::Is64BitProcess)

# ---------- 1) 定位 csc.exe ----------
$cscCandidates = @(
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
)
$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw "找不到 csc.exe（需要 .NET Framework 4.x）" }
Write-Host ("csc        : " + $csc)

# ---------- 2) 解析引用程序集 ----------
function Resolve-Assembly([string]$name) {
    $candidates = New-Object System.Collections.Generic.List[string]

    # a) Reference Assemblies（最优先，稳定）
    foreach ($ver in @('v4.8.1','v4.8','v4.7.2','v4.7.1','v4.7','v4.6.2','v4.6.1','v4.6','v4.5.2','v4.5.1','v4.5')) {
        $candidates.Add(("C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\{0}\{1}.dll" -f $ver, $name))
        $candidates.Add(("C:\Program Files\Reference Assemblies\Microsoft\Framework\.NETFramework\{0}\{1}.dll" -f $ver, $name))
    }

    # b) GAC（各架构都试）
    foreach ($gac in @('GAC_MSIL','GAC_64','GAC_32')) {
        $base = "C:\Windows\Microsoft.NET\assembly\$gac\$name"
        if (Test-Path $base) {
            $dll = Get-ChildItem $base -Recurse -Filter "$name.dll" -ErrorAction SilentlyContinue |
                   Where-Object { $_.FullName -match 'v4\.0' } | Select-Object -First 1
            if ($dll) { $candidates.Add($dll.FullName) }
        }
    }

    # c) Framework 目录及其 WPF 子目录
    foreach ($fw in @('Framework64','Framework')) {
        $candidates.Add("C:\Windows\Microsoft.NET\$fw\v4.0.30319\$name.dll")
        $candidates.Add("C:\Windows\Microsoft.NET\$fw\v4.0.30319\WPF\$name.dll")
    }

    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    return $null
}

$need = @('PresentationFramework','PresentationCore','WindowsBase','System.Xaml')
$refs = @()
foreach ($n in $need) {
    $p = Resolve-Assembly $n
    if (-not $p) { throw "找不到引用程序集：$n（已尝试 Reference Assemblies / GAC / Framework 目录）" }
    Write-Host ("ref        : " + $n + " -> " + $p)
    $refs += $p
}

# ---------- 3) 准备源文件 ----------
if ($Clean -and (Test-Path $out)) { Remove-Item $out -Force; Write-Host "已清理旧产物" }

Get-Process DeskWidget -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "停止正在运行的实例 PID $($_.Id)"
    Stop-Process -Id $_.Id -Force
    Start-Sleep -Milliseconds 500
}

$sources = @(Get-ChildItem $srcDir -Filter *.cs -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) { throw "src 目录下没有 .cs 文件" }
Write-Host ("源文件     : " + $sources.Count + " 个")

# ---------- 4) 编译 ----------
$cscArgs = New-Object System.Collections.Generic.List[string]
$cscArgs.Add('/nologo')
$cscArgs.Add('/target:winexe')
$cscArgs.Add('/platform:x64')
$cscArgs.Add('/utf8output')
$cscArgs.Add('/out:' + $out)
if (Test-Path $ico)      { $cscArgs.Add('/win32icon:' + $ico) }
if (Test-Path $manifest) { $cscArgs.Add('/win32manifest:' + $manifest) }
foreach ($r in $refs) { $cscArgs.Add('/r:' + $r) }
foreach ($s in $sources) { $cscArgs.Add($s) }

Write-Host "=== 编译 ==="
Write-Host ("命令：" + $csc + " " + ($cscArgs -join ' '))

$output = & $csc $cscArgs.ToArray() 2>&1
$code = $LASTEXITCODE
if ($output) { $output | ForEach-Object { Write-Host $_ } }
Write-Host ("csc 退出码：" + $code)

if ($code -ne 0 -or -not (Test-Path $out)) {
    throw "构建失败（退出码 $code）"
}

$kb = [math]::Round((Get-Item $out).Length / 1KB, 1)
Write-Host ("构建成功：DeskWidget.exe（$kb KB）") -ForegroundColor Green