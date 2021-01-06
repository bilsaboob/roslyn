[CmdletBinding(PositionalBinding=$false)]
param()

Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host ""
Write-Host "-------------------------------------------------------"
Write-Host "// Bump version"
Write-Host "-------------------------------------------------------"
Write-Host ""

# run the script

$versionsFile = $PSScriptRoot + "\Versions.props"

# Create a XML document
[xml]$xmlDoc = New-Object system.Xml.XmlDocument

# Read the existing file
[xml]$xmlDoc = Get-Content $versionsFile

$versionsNode = $xmlDoc.Project.PropertyGroup[0]
$majorVerNode = $versionsNode.MajorVersion
$minorVerNode = $versionsNode.MinorVersion
$patchVerNode = $versionsNode.PatchVersion
$majorVer = "$majorVerNode".Trim()
$minorVer = "$minorVerNode".Trim()
$patchVer = "$patchVerNode".Trim()
$version = "$majorVer.$minorVer.$patchVer"

$newPatchVerValue = [int]$patchVerNode + 1
$newPatchVer = "$newPatchVerValue"
$newVersion = "$majorVer.$minorVer.$newPatchVer"

Write-Host "  bumping version: $version -> $newVersion"

# update the versions node
$versionsNode.PatchVersion = $newPatchVer

# update the parent nodes
$xmlDoc.Project.PropertyGroup[0] = $versionsNode

# save the document
$xmlDoc.Save($versionsFile)
