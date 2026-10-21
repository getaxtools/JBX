#Requires -Version 5.1
<#
.SYNOPSIS
    Drives JBX through a short tour and saves the README screenshots and animated tour.
.DESCRIPTION
    Creates the synthetic demo folders, points JBX at a copy of samples/jbx-sample.yaml through a temporary
    settings file, walks through the tree, the log viewer and the command palette with UI Automation, and
    captures each step. The usual settings file is put back afterwards. Needs Python 3 with Pillow for the
    animated tour.
.EXAMPLE
    pwsh scripts/New-ReadmeMedia.ps1 -Exe artifacts/publish/JBX.exe
#>
[CmdletBinding()]
param(
    [string] $Exe = '',
    [string] $DemoRoot = 'C:\JBX-Demo',
    [string] $Output = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repository = Split-Path -Parent $PSScriptRoot
if (-not $Output) {
    $Output = Join-Path $repository '.github\media'
}
if (-not $Exe) {
    dotnet build (Join-Path $repository 'src\Jbx.Desktop') --configuration Release --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $Exe = Join-Path $repository 'src\Jbx.Desktop\bin\Release\net10.0\JBX.exe'
}
$Exe = (Resolve-Path -LiteralPath $Exe).Path

& python -c 'import PIL' 2>$null
if ($LASTEXITCODE -ne 0) {
    throw 'Python 3 with Pillow is needed for the animated tour: python -m pip install pillow'
}
if (Get-Process -Name JBX -ErrorAction SilentlyContinue) {
    throw 'Close every JBX window first: the tour replaces the settings file for a minute.'
}

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class JbxMediaWindow {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr window, IntPtr hdc, uint flags);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect rect, int size);
}
'@
[JbxMediaWindow]::SetProcessDpiAwarenessContext([IntPtr]::new(-4)) | Out-Null

$automation = [System.Windows.Automation.AutomationElement]
$scope = [System.Windows.Automation.TreeScope]

function Find-Element($root, $property, $value) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($property, $value)
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        $found = $root.FindFirst($scope::Descendants, $condition)
        if ($found) { return $found }
        Start-Sleep -Milliseconds 250
    }
    throw "Not found in the JBX window: $value"
}
function Find-ByName($root, $name) { Find-Element $root $automation::NameProperty $name }
function Find-ById($root, $id) { Find-Element $root $automation::AutomationIdProperty $id }
function Invoke-Element($element) { $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Select-Element($element) { $element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
function Set-Text($element, $text) { $element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($text) }
function Switch-Element($element) { $element.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle() }

function Save-Window($window, $name) {
    [JbxMediaWindow]::SetForegroundWindow($window) | Out-Null
    Start-Sleep -Milliseconds 700
    $outer = New-Object JbxMediaWindow+Rect
    $visible = New-Object JbxMediaWindow+Rect
    [JbxMediaWindow]::GetWindowRect($window, [ref] $outer) | Out-Null
    [JbxMediaWindow]::DwmGetWindowAttribute($window, 9, [ref] $visible, 16) | Out-Null
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point ($visible.Right + 40), ($visible.Bottom + 40)
    Start-Sleep -Milliseconds 300
    $whole = New-Object System.Drawing.Bitmap ($outer.Right - $outer.Left), ($outer.Bottom - $outer.Top)
    $graphics = [System.Drawing.Graphics]::FromImage($whole)
    $hdc = $graphics.GetHdc()
    [JbxMediaWindow]::PrintWindow($window, $hdc, 2) | Out-Null
    $graphics.ReleaseHdc($hdc)
    $graphics.Dispose()
    $area = New-Object System.Drawing.Rectangle ($visible.Left - $outer.Left), ($visible.Top - $outer.Top), ($visible.Right - $visible.Left), ($visible.Bottom - $visible.Top)
    $frame = $whole.Clone($area, $whole.PixelFormat)
    $frame.Save((Join-Path $frames "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $frame.Dispose()
    $whole.Dispose()
    Write-Host "Captured $name"
}

& (Join-Path $PSScriptRoot 'New-JbxDemo.ps1') -Root $DemoRoot

$escapedRoot = $DemoRoot.Replace('\', '\\')
$config = Join-Path $DemoRoot 'jbx-demo.yaml'
$sample = [System.IO.File]::ReadAllText((Join-Path $repository 'samples\jbx-sample.yaml'))
[System.IO.File]::WriteAllText($config, $sample.Replace('{{envvar:USERPROFILE}}\\JBX-Demo', $escapedRoot))

$frames = Join-Path ([System.IO.Path]::GetTempPath()) "jbx-media-$PID"
New-Item -ItemType Directory -Force -Path $frames, $Output | Out-Null

$settingsFolder = Join-Path $env:LOCALAPPDATA 'getaxtools\JBX'
$settingsFile = Join-Path $settingsFolder 'settings.json'
$backup = Join-Path $settingsFolder "settings.before-media-$PID.json"
New-Item -ItemType Directory -Force -Path $settingsFolder | Out-Null
if (Test-Path -LiteralPath $settingsFile) {
    Copy-Item -LiteralPath $settingsFile -Destination $backup
}

$settings = [ordered]@{
    schemaVersion     = 1
    lastConfigPath    = $config
    recentConfigPaths = @($config)
    favourites        = @(
        [ordered]@{ path = Join-Path $DemoRoot 'prod\BillingPortal\Api\logs'; label = 'Billing API - Production'; kind = 'Folder' },
        [ordered]@{ path = Join-Path $DemoRoot 'prod\Orders\Scheduler\logs'; label = 'Order Scheduler - Production'; kind = 'Folder' }
    )
    lastEnvironmentId = 'prod'
    window            = [ordered]@{ width = 1280; height = 760; isMaximized = $false; isFavouritesPaneVisible = $true; favouritesPaneWidth = 250; detailsPaneWidth = 360 }
    session           = [ordered]@{
        configPath  = $config
        tabs        = @(
            [ordered]@{ environmentId = 'dev'; files = @(); selectedFile = -1 },
            [ordered]@{ environmentId = 'uat'; files = @(); selectedFile = -1 },
            [ordered]@{ environmentId = 'prod'; files = @(); selectedFile = -1 }
        )
        selectedTab = 2
    }
}
[System.IO.File]::WriteAllText($settingsFile, ($settings | ConvertTo-Json -Depth 6))

$process = $null
try {
    $process = Start-Process -FilePath $Exe -PassThru
    for ($attempt = 0; $attempt -lt 60 -and $process.MainWindowHandle -eq [IntPtr]::Zero; $attempt++) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
    }
    $handle = $process.MainWindowHandle
    $window = $automation::FromHandle($handle)

    Invoke-Element (Find-ByName $window 'Expand Billing Portal')
    Invoke-Element (Find-ByName $window 'Expand Orders Service')
    Invoke-Element (Find-ByName $window 'Expand Payments Gateway')
    Select-Element (Find-ByName $window 'Billing API')
    Start-Sleep -Seconds 5
    Save-Window $handle '01-environments'

    Invoke-Element (Find-ByName $window 'Expand Billing API')
    $today = 'api-' + (Get-Date).ToString('yyyy-MM-dd') + '.log'
    Select-Element (Find-ByName $window $today)
    Save-Window $handle '02-files'

    Invoke-Element (Find-ById $window 'CommandBar.Open')
    Start-Sleep -Seconds 2
    Save-Window $handle '03-log-viewer'

    Set-Text (Find-ById $window 'Viewer.Search') 'failed'
    Start-Sleep -Seconds 1
    Save-Window $handle '04-search'

    Switch-Element (Find-ById $window 'Viewer.OnlyMatches')
    Start-Sleep -Seconds 1
    Save-Window $handle '05-only-matches'

    Invoke-Element (Find-ById $window 'Shell.OpenPalette')
    Set-Text (Find-ById $window 'Palette.Filter') 'open'
    Start-Sleep -Milliseconds 500
    Save-Window $handle '06-palette'
}
finally {
    if ($process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(5000)) { $process.Kill() }
    }
    if (Test-Path -LiteralPath $backup) {
        Move-Item -LiteralPath $backup -Destination $settingsFile -Force
    }
    else {
        Remove-Item -LiteralPath $settingsFile -ErrorAction SilentlyContinue
    }
}

& python (Join-Path $PSScriptRoot 'readme_media.py') $frames $Output
if ($LASTEXITCODE -ne 0) { throw 'Composing the README media failed.' }
Remove-Item -LiteralPath $frames -Recurse -Force
Write-Host "README media written to $Output"
