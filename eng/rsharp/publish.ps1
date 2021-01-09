. $PSScriptRoot"\version.ps1"

function Publish
{
  param(
    # version
    [string]$version = "",
    [string]$vsixFile = "",
    [string]$compilerToolsFile = ""
  )

  Set-StrictMode -version 2.0
  $ErrorActionPreference = "Stop"

  Write-Host ""
  Write-Host ""
  Write-Host "-------------------------------------------------------"
  Write-Host "// Publish artifacts"
  Write-Host "-------------------------------------------------------"
  Write-Host ""

  # run the script

  if(!$version) {
    # get the next expected version
    $versions = GetNewReleaseVersion
    $version = $versions[1][0]
  }

  # get the artifact paths for the version
  $publishRootFolder = [System.IO.Path]::GetFullPath($PSScriptRoot + "\..\..\.publish")
  $publishFolder = $publishRootFolder + "\" + $version
  if(!$vsixFile) {
    $vsixFile = [System.IO.Path]::GetFullPath($publishFolder + "\rsharp." + $version + ".vsix")
  }
  if(!$compilerToolsFile) {
    $compilerToolsFile = [System.IO.Path]::GetFullPath($publishFolder + "\rsharp-compiler-tools." + $version + ".nupkg")
  }

  $repo = "bilsaboob/roslyn-sharp"
  $releaseTitle = "Release v$version"
  $releaseNotes = "Release v$version"
  $releaseTag = "$version"

  $existingReleases = iex "gh release list -R $repo"
  Foreach ($r in $existingReleases)
  {
    $releaseStr = "$r"
    if($releaseStr -like "*v$version*") {
      Write-Host "Version v$version is already published!"
      return 0
    }
  }

  Write-Host "Publishing version: v$version"
  Write-Host ""

  $cmd = "gh release create -R $repo -t '$releaseTitle' -n '$releaseNotes' $releaseTag '$vsixFile#Visual Studio extension' '$compilerToolsFile#Compiler Tools'"
  Write-Host "[$cmd]"
  Write-Host ""

  iex $cmd
  if(-not ($LASTEXITCODE -eq 0)) {
    return 0
  }

  return 1
}