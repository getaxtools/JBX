#Requires -Version 5.1
<#
.SYNOPSIS
    Creates synthetic log folders under %USERPROFILE%\JBX-Demo for samples/jbx-sample.yaml.
.DESCRIPTION
    Every file is made up. Remove the demo with -Remove.
.EXAMPLE
    pwsh scripts/New-JbxDemo.ps1
#>
[CmdletBinding()]
param(
    [switch] $Remove
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Join-Path $env:USERPROFILE 'JBX-Demo'
if ($Remove) {
    if (Test-Path -LiteralPath $root) {
        Remove-Item -LiteralPath $root -Recurse -Force
    }
    Write-Host "Removed $root"
    return
}

$folders = @(
    @{ Path = 'BillingPortal\Api\logs'; Prefix = 'api-'; Extension = '.log'; Count = 40 },
    @{ Path = 'BillingPortal\Web\W3SVC1'; Prefix = 'u_ex'; Extension = '.log'; Count = 14 },
    @{ Path = 'BillingPortal\Etl'; Prefix = 'etl-run-'; Extension = '.txt'; Count = 7 },
    @{ Path = 'Orders\Scheduler\logs'; Prefix = 'scheduler-'; Extension = '.log'; Count = 21 },
    @{ Path = 'Orders\FulfilmentSync'; Prefix = 'sync-'; Extension = '.log'; Count = 10 }
)

$now = Get-Date
foreach ($environment in @('dev', 'uat', 'prod')) {
    foreach ($folder in $folders) {
        $target = Join-Path $root (Join-Path $environment $folder.Path)
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        for ($day = 0; $day -lt $folder.Count; $day++) {
            $stamp = $now.AddDays(-$day)
            $file = Join-Path $target ("{0}{1}{2}" -f $folder.Prefix, $stamp.ToString('yyyy-MM-dd'), $folder.Extension)
            $lines = 1..(5 + $day % 7) | ForEach-Object { "{0:o} INFO demo line {1} for {2}" -f $stamp, $_, $environment }
            Set-Content -LiteralPath $file -Value $lines -Encoding UTF8
            (Get-Item -LiteralPath $file).LastWriteTime = $stamp.AddMinutes(-($day * 37 % 600))
        }
    }
}

Write-Host "Demo folders created under $root"
Write-Host "Open samples\jbx-sample.yaml in JBX (Ctrl+O)."
