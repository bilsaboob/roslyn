function GetNewReleaseVersion
{
  # read the version information from "versions.txt"
  $versionsFile = [System.IO.Path]::GetFullPath($PSScriptRoot + "\..\..\versions.txt")

  $version = GetCurrentReleaseVersion
  $releaseVersion = $version[0]
  $releaseMajorVersion = $version[1]
  $releaseMinorVersion = $version[2]
  $releasePatchVersion = $version[3]

  # build the new release version
  $newReleasePatchVerValue = [int]$releasePatchVersion + 1
  $newReleasePatchVer = "$newReleasePatchVerValue"
  $newReleaseVersion = "$releaseMajorVersion.$releaseMinorVersion.$newReleasePatchVer"

  return @(@($releaseVersion, $releaseMajorVersion, $releaseMinorVersion, $releasePatchVersion), @($newReleaseVersion, $releaseMajorVersion, $releaseMinorVersion, $newReleasePatchVer))
}

function GetCurrentReleaseVersion
{
  # read the version information from "versions.txt"
  $versionsFile = [System.IO.Path]::GetFullPath($PSScriptRoot + "\..\..\versions.txt")

  $releaseMajorVersion = ""
  $releaseMinorVersion = ""
  $releasePatchVersion = ""
  $releaseVersion = ""

  foreach($line in [System.IO.File]::ReadLines($versionsFile))
  {
    if($line.StartsWith("release=")) 
    {
      $versionStr = $line.SubString(8)
      $versionParts = $versionStr.Split('.')
      $releaseMajorVersion = $versionParts[0]
      $releaseMinorVersion = $versionParts[1]
      $releasePatchVersion = $versionParts[2]
      $releaseVersion = "$releaseMajorVersion.$releaseMinorVersion.$releasePatchVersion"
    }
  }

  return @($releaseVersion, $releaseMajorVersion, $releaseMinorVersion, $releasePatchVersion)
}

function ParseVersionParts
{
  Param
  (
    [string]$version
  )

  $versionParts = $version.Split('.')
  $releaseMajorVersion = $versionParts[0]
  $releaseMinorVersion = $versionParts[1]
  $releasePatchVersion = $versionParts[2]
  $releaseVersion = "$releaseMajorVersion.$releaseMinorVersion.$releasePatchVersion"
  return @($releaseVersion, $releaseMajorVersion, $releaseMinorVersion, $releasePatchVersion)
}

function UpdateVersion
{
  Param 
  (
    [string]$version
  )
  # read the version information from "versions.txt"
  $versionsFile = [System.IO.Path]::GetFullPath($PSScriptRoot + "\..\..\versions.txt")
  Set-Content -Path $versionsFile -Value "release=$version"
}