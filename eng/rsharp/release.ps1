. $PSScriptRoot"\version.ps1"
. $PSScriptRoot"\package.ps1"
. $PSScriptRoot"\publish.ps1"

function Release
{
  Param
  (
    [Parameter(Mandatory=$false)]
    [switch]$publish
  )

  $result = BuildAndPackage
  if($result -eq 0) {
    return
  }

  if($publish -eq $true) {
    $oldVersion = $result[0]
    $newVersion = $result[1]
    $rsharpVsixPublishFile = $result[2]
    $rsharpCompilerToolsetPublishFile = $result[3]

    $result = Publish -version $newVersion -vsixFile $rsharpVsixPublishFile -compilerToolsFile $rsharpCompilerToolsetPublishFile    
    if($result -eq 1) {
      Write-Host ""
      Write-Host "Bumpin version: [$oldVersion] -> [$newVersion]"
      UpdateVersion -version $newVersion
    }
  }
}
