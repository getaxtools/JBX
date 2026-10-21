#Requires -Version 5.1
<#
.SYNOPSIS
    Creates synthetic log folders for samples/jbx-sample.yaml.
.DESCRIPTION
    Every host, request and message is made up. By default the folders go under %USERPROFILE%\JBX-Demo,
    where the sample config looks for them. Remove the demo with -Remove.
.EXAMPLE
    pwsh scripts/New-JbxDemo.ps1
.EXAMPLE
    pwsh scripts/New-JbxDemo.ps1 -Root C:\JBX-Demo
#>
[CmdletBinding()]
param(
    [string] $Root = (Join-Path $env:USERPROFILE 'JBX-Demo'),
    [switch] $Remove
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Remove) {
    if (Test-Path -LiteralPath $Root) {
        Remove-Item -LiteralPath $Root -Recurse -Force
    }
    Write-Host "Removed $Root"
    return
}

$folders = @(
    @{ Path = 'BillingPortal\Api\logs'; Prefix = 'api-'; Extension = '.log'; Count = 40; Lines = 1200 },
    @{ Path = 'BillingPortal\Web\W3SVC1'; Prefix = 'u_ex'; Extension = '.log'; Count = 14; Lines = 300 },
    @{ Path = 'BillingPortal\Etl'; Prefix = 'etl-run-'; Extension = '.txt'; Count = 7; Lines = 200 },
    @{ Path = 'Orders\Scheduler\logs'; Prefix = 'scheduler-'; Extension = '.log'; Count = 21; Lines = 400 },
    @{ Path = 'Orders\FulfilmentSync'; Prefix = 'sync-'; Extension = '.log'; Count = 10; Lines = 300 }
)

$messages = @(
    @{ Level = 'INFO'; Text = 'GET /api/invoices/{0} 200 in {1} ms' },
    @{ Level = 'INFO'; Text = 'POST /api/payments/{0}/capture 202 in {1} ms' },
    @{ Level = 'INFO'; Text = 'Statement run {0} queued for customer batch {1}' },
    @{ Level = 'DEBUG'; Text = 'Cache hit for tariff {0} ({1} entries)' },
    @{ Level = 'DEBUG'; Text = 'Opened connection {0} to ledger-db.contoso.test in {1} ms' },
    @{ Level = 'WARN'; Text = 'Slow response from tax-rates.contoso.test: {1} ms for invoice {0}' },
    @{ Level = 'WARN'; Text = 'Retrying payment {0} after timeout (attempt {1} of 3)' },
    @{ Level = 'ERROR'; Text = 'Payment {0} failed: gateway returned 503 after {1} ms' }
)

function New-LogLines([datetime] $start, [int] $count, [string] $environment, [System.Random] $random) {
    $lines = New-Object System.Collections.Generic.List[string]
    $time = $start
    for ($line = 0; $line -lt $count; $line++) {
        $time = $time.AddMilliseconds($random.Next(200, 9000))
        $roll = $random.Next(100)
        $message = if ($roll -lt 2) { $messages[7] } elseif ($roll -lt 8) { $messages[5 + $random.Next(2)] } elseif ($roll -lt 25) { $messages[3 + $random.Next(2)] } else { $messages[$random.Next(3)] }
        $text = $message.Text -f $random.Next(10000, 99999), $random.Next(4, 4000)
        $lines.Add(('{0:yyyy-MM-dd HH:mm:ss.fff} {1,-5} [{2}] {3}' -f $time, $message.Level, $environment, $text))
        if ($message.Level -eq 'ERROR') {
            $lines.Add('   at Contoso.Billing.Payments.GatewayClient.CaptureAsync(PaymentId id)')
            $lines.Add('   at Contoso.Billing.Payments.CaptureHandler.HandleAsync(CaptureCommand command)')
        }
    }
    return $lines
}

$now = Get-Date
$random = New-Object System.Random 20261021
foreach ($environment in @('dev', 'uat', 'prod')) {
    foreach ($folder in $folders) {
        $target = Join-Path $Root (Join-Path $environment $folder.Path)
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        for ($day = 0; $day -lt $folder.Count; $day++) {
            $stamp = $now.AddDays(-$day)
            $file = Join-Path $target ("{0}{1}{2}" -f $folder.Prefix, $stamp.ToString('yyyy-MM-dd'), $folder.Extension)
            $lineCount = if ($day -eq 0) { $folder.Lines } else { 20 + $day % 7 }
            $lines = New-LogLines $stamp.Date $lineCount $environment $random
            Set-Content -LiteralPath $file -Value $lines -Encoding UTF8
            (Get-Item -LiteralPath $file).LastWriteTime = $stamp.AddMinutes(-($day * 37 % 600))
        }
    }
}

Write-Host "Demo folders created under $Root"
Write-Host "Open samples\jbx-sample.yaml in JBX (Ctrl+O)."
