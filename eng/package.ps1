[CmdletBinding(PositionalBinding=$false)]
param()

Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host ""
Write-Host "-------------------------------------------------------"
Write-Host "// Build & Package artifacts"
Write-Host "-------------------------------------------------------"
Write-Host ""

# run the script

Write-Host "[Build.cmd -Configuration Release]"
Write-Host ""
iex ".\Build.cmd -Configuration Release"
Write-Host ""
if(-not ($LASTEXITCODE -eq 0)) {
  exit
}

Write-Host "[dotnet pack -c Release ..\src\NuGet\Microsoft.Net.Compilers.Toolset\Microsoft.Net.Compilers.Toolset.Package.csproj]"
Write-Host ""
iex "dotnet pack -c Release .\src\NuGet\Microsoft.Net.Compilers.Toolset\Microsoft.Net.Compilers.Toolset.Package.csproj"
Write-Host ""
if(-not ($LASTEXITCODE -eq 0)) {
  exit
}

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
$version = "$majorVer.$minorVer.$patchVer-dev"

# prepare paths
$artifactsFolder = $PSScriptRoot + "\..\artifacts"
$rsharpVsixArtifactFile = $artifactsFolder + "\VSSetup\Release\RoslynDeployment.vsix"
$rsharpCompilerToolsetArtifactFile = $artifactsFolder + "\packages\Release\Shipping\Microsoft.Net.Compilers.Toolset." + $version + ".nupkg"

$publishRootFolder = $PSScriptRoot + "\..\.publish"
$publishFolder = $publishRootFolder + "\" + $version

$rsharpVsixPublihFile = $publishFolder + "\rsharp.vsix"
$rsharpCompilerToolsetPublishFile = $publishFolder + "\rsharp-compiler-tools.nupkg"

Write-Host "packaging artifacts to:"
Write-Host "  " $publishFolder

# vs extension vsix artifact must exist
if (!(Test-Path -path $rsharpVsixArtifactFile -PathType Leaf)) {
  Write-Host ""
  Write-Host "missing artifact: " $rsharpVsixArtifactFile
  exit 1
}

# compiler toolset nuget artifact must exist
if (!(Test-Path -path $rsharpCompilerToolsetArtifactFile -PathType Leaf)) {
  Write-Host ""
  Write-Host "missing artifact: " $rsharpCompilerToolsetArtifactFile
  exit 1
}

# create publish folders
if (!(Test-Path -path $publishRootFolder)) {New-Item $publishRootFolder -Type Directory | Out-Null}
if (!(Test-Path -path $publishFolder)) {New-Item $publishFolder -Type Directory | Out-Null}

# copy the artifacts
Copy-Item -Path $rsharpVsixArtifactFile $rsharpVsixPublihFile -Force
Copy-Item -Path $rsharpCompilerToolsetArtifactFile $rsharpCompilerToolsetPublishFile -Force

Write-Host ""
Write-Host "packaged artifacts: "
Write-Host "  " $rsharpVsixPublihFile
Write-Host "  " $rsharpCompilerToolsetPublishFile