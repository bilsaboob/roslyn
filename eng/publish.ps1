[CmdletBinding(PositionalBinding=$false)]
param()

Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host ""
Write-Host "-------------------------------------------------------"
Write-Host "// Publish artifacts"
Write-Host "-------------------------------------------------------"
Write-Host ""

# run the script

# load the current version
$versionsFile = $PSScriptRoot + "\Versions.props"
[xml]$xmlDoc = New-Object system.Xml.XmlDocument
[xml]$xmlDoc = Get-Content $versionsFile
$versionsNode = $xmlDoc.Project.PropertyGroup[0]
$majorVerNode = $versionsNode.MajorVersion
$minorVerNode = $versionsNode.MinorVersion
$patchVerNode = $versionsNode.PatchVersion
$majorVer = "$majorVerNode".Trim()
$minorVer = "$minorVerNode".Trim()
$patchVer = "$patchVerNode".Trim()
$version = "$majorVer.$minorVer.$patchVer"

$repo = "bilsaboob/roslyn-sharp"
$releaseTitle = "Release v$version"
$releaseNotes = "Release v$version"
$releaseTag = "$version"

$root = Split-Path -parent $PSScriptRoot
$rootPath = "$root".Trim()
$vsixFile = $rootPath + "\.publish\$version-dev\rsharp.vsix"
$compilerToolsFile = $rootPath + "\.publish\$version-dev\rsharp-compiler-tools.nupkg"

$existingReleases = iex "gh release list -R $repo"
Foreach ($r in $existingReleases)
{
  $releaseStr = "$r"
  if($releaseStr -like "*v$version*") {
    Write-Host "Version v$version is already published!"
    exit
  }
}

Write-Host "Publishing version: v$version"
Write-Host ""

$cmd = "gh release create -R $repo -t '$releaseTitle' -n '$releaseNotes' $releaseTag '$vsixFile#Visual Studio extension' '$compilerToolsFile#Compiler Tools'"
Write-Host "[$cmd]"
Write-Host ""

iex $cmd
if(-not ($LASTEXITCODE -eq 0)) {
  exit
}