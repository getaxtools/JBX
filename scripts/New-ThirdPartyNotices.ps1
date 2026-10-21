#Requires -Version 5.1
<#
.SYNOPSIS
    Writes THIRD-PARTY-NOTICES.txt for every package the desktop app ships, or checks it is current.
.DESCRIPTION
    Reads src/Jbx.Desktop/packages.lock.json (the shipped closure) and each package's .nuspec from the
    NuGet global packages folder, so run `dotnet restore` first. With -Check, fails when the file on
    disk differs from what would be written.
.EXAMPLE
    pwsh scripts/New-ThirdPartyNotices.ps1
.EXAMPLE
    pwsh scripts/New-ThirdPartyNotices.ps1 -Check
#>
[CmdletBinding()]
param(
    [switch] $Check,
    [string] $RepositoryRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $RepositoryRoot) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

# Used while building only; nothing from them is in the shipped app.
$buildOnlyPackages = @('Avalonia.BuildServices', 'Microsoft.CodeAnalysis.ResxSourceGenerator', 'Microsoft.NET.ILLink.Tasks')

$noticesPath = Join-Path $RepositoryRoot 'THIRD-PARTY-NOTICES.txt'
$lockPath = Join-Path $RepositoryRoot 'src/Jbx.Desktop/packages.lock.json'

$licenceTexts = @{
    'MIT'          = @'
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial
portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN
NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
'@
    'BSD-3-Clause' = @'
Redistribution and use in source and binary forms, with or without modification, are permitted
provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this list of conditions and
   the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions
   and the following disclaimer in the documentation and/or other materials provided with the
   distribution.
3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse
   or promote products derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED
WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR
TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
'@
}

# Windows PowerShell 5.1 reads files without a byte order mark as ANSI; licence texts are UTF-8.
function Read-Utf8 {
    param([string] $Path)
    return [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
}

function Get-GlobalPackagesFolder {
    $output = & dotnet nuget locals global-packages --list
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet nuget locals failed'
    }
    return ($output -replace '^global-packages:\s*', '').Trim()
}

function Get-ShippedPackages {
    $lock = Get-Content -Raw -LiteralPath $lockPath | ConvertFrom-Json
    $packages = @{}
    foreach ($framework in $lock.dependencies.PSObject.Properties) {
        foreach ($dependency in $framework.Value.PSObject.Properties) {
            if ($dependency.Value.type -ne 'Project' -and $buildOnlyPackages -notcontains $dependency.Name) {
                $packages["$($dependency.Name)/$($dependency.Value.resolved)"] = [pscustomobject]@{ Id = $dependency.Name; Version = $dependency.Value.resolved }
            }
        }
    }
    return @($packages.Values | Sort-Object Id, Version)
}

function Get-MetadataText {
    param([System.Xml.XmlNode] $Metadata, [string] $Name)
    $node = $Metadata.SelectSingleNode("*[local-name()='$Name']")
    if ($null -eq $node) {
        return ''
    }
    return $node.InnerText.Trim()
}

function Get-PackageNotice {
    param([pscustomobject] $Package, [string] $GlobalPackages)
    $folder = Join-Path $GlobalPackages (Join-Path $Package.Id.ToLowerInvariant() $Package.Version)
    $nuspecPath = Join-Path $folder "$($Package.Id.ToLowerInvariant()).nuspec"
    if (-not (Test-Path -LiteralPath $nuspecPath)) {
        throw "Missing $nuspecPath; run dotnet restore first."
    }

    [xml] $nuspec = Get-Content -Raw -LiteralPath $nuspecPath
    $metadata = $nuspec.package.metadata
    $licence = $metadata.SelectSingleNode("*[local-name()='license']")
    $notice = [ordered]@{
        Title     = "$($Package.Id) $($Package.Version)"
        Project   = Get-MetadataText -Metadata $metadata -Name 'projectUrl'
        Copyright = Get-MetadataText -Metadata $metadata -Name 'copyright'
        Licence   = ''
        Text      = ''
    }
    if ($null -eq $licence) {
        throw "$($notice.Title) declares no license."
    }

    if ($licence.type -eq 'expression') {
        $notice.Licence = $licence.InnerText
    }
    else {
        $notice.Licence = 'see the license text below'
        $notice.Text = (Read-Utf8 -Path (Join-Path $folder $licence.InnerText)).Trim()
    }

    return [pscustomobject] $notice
}

function Format-Notices {
    param([object[]] $Notices)
    $builder = New-Object System.Text.StringBuilder
    [void] $builder.AppendLine('THIRD-PARTY NOTICES')
    [void] $builder.AppendLine('')
    [void] $builder.AppendLine('JBX - Jump Box Explorer ships the components below. Each is used under the license named.')
    [void] $builder.AppendLine('The .NET runtime it ships with is (c) .NET Foundation and Contributors, under the MIT license.')
    foreach ($notice in $Notices) {
        [void] $builder.AppendLine('')
        [void] $builder.AppendLine('------------------------------------------------------------------------------')
        [void] $builder.AppendLine($notice.Title)
        [void] $builder.AppendLine("License: $($notice.Licence)")
        if ($notice.Copyright) { [void] $builder.AppendLine("Copyright: $($notice.Copyright)") }
        if ($notice.Project) { [void] $builder.AppendLine("Project: $($notice.Project)") }
        if ($notice.Text) {
            [void] $builder.AppendLine('')
            [void] $builder.AppendLine($notice.Text)
        }
    }

    $expressions = @($Notices | Where-Object { -not $_.Text } | ForEach-Object { $_.Licence } | Sort-Object -Unique)
    foreach ($expression in $expressions) {
        foreach ($name in ($expression -split '\s+(?:OR|AND)\s+' | ForEach-Object { $_.Trim('() ') } | Sort-Object -Unique)) {
            [void] $builder.AppendLine('')
            [void] $builder.AppendLine('==============================================================================')
            [void] $builder.AppendLine("$name license")
            [void] $builder.AppendLine('')
            if ($licenceTexts.ContainsKey($name)) {
                [void] $builder.AppendLine($licenceTexts[$name].Trim())
            }
            elseif ($name -eq 'Apache-2.0') {
                [void] $builder.AppendLine((Read-Utf8 -Path (Join-Path $RepositoryRoot 'LICENSE-APACHE')).Trim())
            }
            else {
                throw "No license text known for $name; add it to this script."
            }
        }
    }

    return ($builder.ToString() -replace "`r`n", "`n")
}

$globalPackages = Get-GlobalPackagesFolder
$notices = @(Get-ShippedPackages | ForEach-Object { Get-PackageNotice -Package $_ -GlobalPackages $globalPackages })
$content = Format-Notices -Notices $notices

if ($Check) {
    $current = if (Test-Path -LiteralPath $noticesPath) { (Read-Utf8 -Path $noticesPath) -replace "`r`n", "`n" } else { '' }
    if ($current -ne $content) {
        Write-Error 'THIRD-PARTY-NOTICES.txt is out of date. Run scripts/New-ThirdPartyNotices.ps1 and commit the result.'
        exit 1
    }

    Write-Host "THIRD-PARTY-NOTICES.txt is current ($($notices.Count) packages)."
    exit 0
}

[System.IO.File]::WriteAllText($noticesPath, $content, (New-Object System.Text.UTF8Encoding $false))
Write-Host "Wrote THIRD-PARTY-NOTICES.txt ($($notices.Count) packages)."
