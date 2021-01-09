. $PSScriptRoot"\version.ps1"
. $PSScriptRoot"\..\build.ps1"

function BuildAndPackage
{
  Set-StrictMode -version 2.0
  $ErrorActionPreference = "Stop"

  Write-Host ""
  Write-Host ""
  Write-Host "-------------------------------------------------------"
  Write-Host "// Build & Package artifacts"
  Write-Host "-------------------------------------------------------"

  # run the script

  # get the new version to be packaged
  $versions = GetNewReleaseVersion
  $oldVersion = $versions[0][0]
  $newVersion = $versions[1][0]
  Write-Host ""
  Write-Host "Packaging new version:" "[$newVersion]"

  # prepare paths
  $artifactsFolder = [System.IO.Path]::GetFullPath($PSScriptRoot + "\..\..\artifacts")
  $releaseNugetPackagesFoler = [System.IO.Path]::GetFullPath($artifactsFolder + "\packages\Release")
  $releaseNugetPackagedFolder = [System.IO.Path]::GetFullPath($releaseNugetPackagesFoler + "\Release")
  $rsharpVsixArtifactFile = [System.IO.Path]::GetFullPath($artifactsFolder + "\VSSetup\Release\RoslynDeployment.vsix")
  $rsharpCompilerToolsetArtifactFile = [System.IO.Path]::GetFullPath($releaseNugetPackagedFolder + "\Microsoft.Net.Compilers.Toolset." + $newVersion + ".nupkg")

  $publishRootFolder = [System.IO.Path]::GetFullPath($PSScriptRoot + "\..\..\.publish")
  $publishFolder = $publishRootFolder + "\" + $newVersion

  $rsharpVsixPublishFile = $publishFolder + "\rsharp." + $newVersion + ".vsix"
  $rsharpCompilerToolsetPublishFile = $publishFolder + "\rsharp-compiler-tools." + $newVersion + ".nupkg"

  # delete the release package folders before releasing
  if (Test-Path -path $releaseNugetPackagedFolder) {
    Write-Host ""
    Write-Host "Cleaning release package folders:" 
    Write-Host "  $releaseNugetPackagedFolder"
    Remove-Item -Recurse -Force $releaseNugetPackagedFolder
  }

  # now run the build
  Write-Host ""
  Write-Host ""
  Write-Host "#### BUILD SOLUTION ############################################"
  Write-Host ""
  Write-Host "[RunBuild -c Release]"
  Write-Host ""

  $buildResult = RunBuild -build -pack -c Release -majorVer $versions[1][1] -minorVer $versions[1][2] -patchVer $versions[1][3]
  if(-not ($buildResult -eq 0)) {
    return @(0)
  }

  Write-Host ""
  Write-Host "#### COPY ARTIFACTS TO PUBLISH FOLDER ##########################"
  Write-Host ""


  Write-Host "packaging artifacts to:"
  Write-Host "  " $publishFolder

  # vs extension vsix artifact must exist
  if (!(Test-Path -path $rsharpVsixArtifactFile -PathType Leaf)) {
    Write-Host ""
    Write-Host "missing artifact: " $rsharpVsixArtifactFile
    return @(0)
  }

  # compiler toolset nuget artifact must exist
  if (!(Test-Path -path $rsharpCompilerToolsetArtifactFile -PathType Leaf)) {
    Write-Host ""
    Write-Host "missing artifact: " $rsharpCompilerToolsetArtifactFile
    return @(0)
  }

  # create publish folders
  if (!(Test-Path -path $publishRootFolder)) {New-Item $publishRootFolder -Type Directory | Out-Null}
  if (!(Test-Path -path $publishFolder)) {New-Item $publishFolder -Type Directory | Out-Null}

  # copy the artifacts
  Copy-Item -Path $rsharpVsixArtifactFile $rsharpVsixPublishFile -Force
  Copy-Item -Path $rsharpCompilerToolsetArtifactFile $rsharpCompilerToolsetPublishFile -Force

  Write-Host ""
  Write-Host "packaged artifacts: "
  Write-Host "  " $rsharpVsixPublishFile
  Write-Host "  " $rsharpCompilerToolsetPublishFile

  return @($oldVersion, $newVersion, $rsharpVsixPublishFile, $rsharpCompilerToolsetPublishFile)
}