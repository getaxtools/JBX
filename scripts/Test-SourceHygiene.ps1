#Requires -Version 5.1
<#
.SYNOPSIS
    Fails when source files break the comment, naming or blocking-call rules in CODING-STANDARDS.md.
.EXAMPLE
    pwsh scripts/Test-SourceHygiene.ps1
#>
[CmdletBinding()]
param(
    [string] $RepositoryRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $RepositoryRoot) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

$emDash = [string][char]0x2014

$lineRules = [ordered]@{
    'TODO without an issue number' = '//.*\bTODO\b(?!\(#\d+\))'
    'Banner comment'               = '^\s*//+\s*[=\-*#~]{3,}'
    'Region directive'             = '^\s*#(region|endregion)\b'
    'Commented-out code'           = '^\s*//(?!/)\s*\S.*[;{}]\s*$'
    'Thread.Sleep'                 = '\bThread\.Sleep\s*\('
    'Blocking GetResult()'         = '\.GetAwaiter\(\)\s*\.GetResult\(\)'
    'Dumping-ground type name'     = '\b(class|record|struct|interface)\s+(\w*(Utils|Util|Helpers|Helper|Manager)|Common|Misc)\b'
}

$emptyCatch = '\bcatch\s*(\([^)]*\))?\s*(when\s*\([^)]*\)\s*)?\{\s*\}'
$dumpingGroundFile = '(^|[\\/])\w*(Utils|Util|Helpers|Helper|Manager|Common|Misc)\.cs$'

function Get-SourceFiles {
    param([string] $Root, [string[]] $Patterns)
    $paths = & git -C $Root ls-files --cached --others --exclude-standard -- $Patterns
    if ($LASTEXITCODE -ne 0) {
        throw "git ls-files failed in $Root"
    }
    return @($paths | Where-Object { $_ -and (Test-Path -LiteralPath (Join-Path $Root $_)) })
}

function Get-LineNumber {
    param([string] $Text, [int] $Index)
    return ($Text.Substring(0, $Index) -split "`n").Count
}

$problems = New-Object System.Collections.Generic.List[string]

$codeFiles = Get-SourceFiles -Root $RepositoryRoot -Patterns @('*.cs', '*.axaml')
foreach ($relativePath in $codeFiles) {
    $text = [System.IO.File]::ReadAllText((Join-Path $RepositoryRoot $relativePath))
    $lines = $text -split "`r?`n"

    for ($i = 0; $i -lt $lines.Count; $i++) {
        foreach ($rule in $lineRules.GetEnumerator()) {
            if ($lines[$i] -match $rule.Value) {
                $problems.Add("${relativePath}:$($i + 1): $($rule.Key): $($lines[$i].Trim())")
            }
        }
    }

    foreach ($match in [regex]::Matches($text, $emptyCatch)) {
        $problems.Add("${relativePath}:$(Get-LineNumber $text $match.Index): Empty catch block")
    }

    if ($relativePath -match $dumpingGroundFile) {
        $problems.Add("${relativePath}: Dumping-ground file name")
    }
}

$textFiles = Get-SourceFiles -Root $RepositoryRoot -Patterns @('*.cs', '*.axaml', '*.ps1', '*.csproj', '*.props', '*.targets', '*.yml', '*.md')
foreach ($relativePath in $textFiles) {
    $lines = [System.IO.File]::ReadAllLines((Join-Path $RepositoryRoot $relativePath))
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i].Contains($emDash)) {
            $problems.Add("${relativePath}:$($i + 1): Em dash (write - instead)")
        }
    }
}

if ($problems.Count -gt 0) {
    Write-Host "Source hygiene check failed ($($problems.Count) problems):"
    $problems | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "Source hygiene check passed ($($codeFiles.Count) code files, $($textFiles.Count) text files)."
