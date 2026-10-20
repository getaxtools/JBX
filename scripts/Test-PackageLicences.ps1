#Requires -Version 5.1
<#
.SYNOPSIS
    Fails when any locked NuGet package, direct or transitive, lacks an allowed license expression.
.DESCRIPTION
    Reads every packages.lock.json, then each package's .nuspec from the NuGet global packages
    folder, so run `dotnet restore` first.
.EXAMPLE
    pwsh scripts/Test-PackageLicences.ps1
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

$allowedLicences = @('MIT', 'Apache-2.0', 'BSD-2-Clause', 'BSD-3-Clause', 'ISC', 'Zlib', 'MPL-2.0', 'Unicode-3.0')

# Packages that ship a license file instead of an expression. Each entry was read by a person;
# the hash makes a changed license file fail again until it is re-read.
$reviewedLicenceFiles = @{
    'Avalonia.Angle.Windows.Natives' = @{
        Licence = 'BSD-3-Clause'
        Sha256  = '54aff7276217df9f6b5181613999d208c9e40d2b1d51bf55217837e6871a4a63'
    }
}

function Get-GlobalPackagesFolder {
    $output = & dotnet nuget locals global-packages --list
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet nuget locals failed'
    }
    return ($output -replace '^global-packages:\s*', '').Trim()
}

function Get-LockedPackages {
    param([string] $Root)
    $lockFiles = & git -C $Root ls-files --cached --others --exclude-standard -- '*packages.lock.json'
    $packages = @{}
    foreach ($lockFile in $lockFiles) {
        $lock = Get-Content -Raw -LiteralPath (Join-Path $Root $lockFile) | ConvertFrom-Json
        foreach ($framework in $lock.dependencies.PSObject.Properties) {
            foreach ($dependency in $framework.Value.PSObject.Properties) {
                if ($dependency.Value.type -ne 'Project') {
                    $key = "$($dependency.Name)/$($dependency.Value.resolved)"
                    $packages[$key] = [pscustomobject]@{ Id = $dependency.Name; Version = $dependency.Value.resolved }
                }
            }
        }
    }
    return @($packages.Values | Sort-Object Id)
}

function Test-LicenceExpression {
    param([string] $Expression)
    $alternatives = ($Expression -replace '[()]', ' ') -split '\s+OR\s+'
    foreach ($alternative in $alternatives) {
        $required = $alternative -split '\s+AND\s+' | ForEach-Object { ($_ -split '\s+WITH\s+')[0].Trim() }
        $allAllowed = $true
        foreach ($licence in $required) {
            if ($allowedLicences -notcontains $licence) {
                $allAllowed = $false
            }
        }
        if ($allAllowed) {
            return $true
        }
    }
    return $false
}

function Test-ReviewedLicenceFile {
    param([pscustomobject] $Package, [string] $PackageFolder, [string] $FileName)
    $label = "$($Package.Id) $($Package.Version)"
    if (-not $reviewedLicenceFiles.ContainsKey($Package.Id)) {
        return "${label}: license file '$FileName' has not been reviewed"
    }

    $review = $reviewedLicenceFiles[$Package.Id]
    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $PackageFolder $FileName)).Hash.ToLowerInvariant()
    if ($actualHash -ne $review.Sha256) {
        return "${label}: license file changed since it was reviewed as $($review.Licence); read it and update the hash"
    }
    if ($allowedLicences -notcontains $review.Licence) {
        return "${label}: reviewed license '$($review.Licence)' is not allowed"
    }
    return $null
}

$packagesFolder = Get-GlobalPackagesFolder
$problems = New-Object System.Collections.Generic.List[string]
$packages = Get-LockedPackages -Root $RepositoryRoot

foreach ($package in $packages) {
    $id = $package.Id.ToLowerInvariant()
    $version = $package.Version.ToLowerInvariant()
    $nuspecPath = Join-Path $packagesFolder (Join-Path $id (Join-Path $version "$id.nuspec"))

    if (-not (Test-Path -LiteralPath $nuspecPath)) {
        $problems.Add("$($package.Id) $($package.Version): not in the package cache; run dotnet restore first")
        continue
    }

    [xml] $nuspec = Get-Content -Raw -LiteralPath $nuspecPath
    $licence = $nuspec.package.metadata.license
    if ($null -ne $licence -and $licence.type -eq 'file') {
        $problem = Test-ReviewedLicenceFile -Package $package -PackageFolder (Split-Path -Parent $nuspecPath) -FileName $licence.InnerText.Trim()
        if ($problem) {
            $problems.Add($problem)
        }
        continue
    }

    if ($null -eq $licence -or $licence.type -ne 'expression') {
        $problems.Add("$($package.Id) $($package.Version): no license expression")
        continue
    }

    $expression = $licence.InnerText.Trim()
    if (-not (Test-LicenceExpression $expression)) {
        $problems.Add("$($package.Id) $($package.Version): license '$expression' is not allowed")
    }
}

if ($problems.Count -gt 0) {
    Write-Host "License check failed ($($problems.Count) problems):"
    $problems | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "License check passed ($($packages.Count) packages)."
