#
# This script controls the Roslyn build process. This encompasess everything from build, testing to
# publishing of NuGet packages. The intent is to structure it to allow for a simple flow of logic
# between the following phases:
#
#   - restore
#   - build
#   - sign
#   - pack
#   - test
#   - publish
#
# Each of these phases has a separate command which can be executed independently. For instance
# it's fine to call `build.ps1 -build -testDesktop` followed by repeated calls to
# `.\build.ps1 -testDesktop`.

$_configuration = "Debug"
$_configuration = "m"
$_msbuildEngine = "vs"

# Actions
$_restore = $false
$_build = $false
$_rebuild = $false
$_sign = $false
$_pack = $false
$_publish = $false
$_launch = $false
$_help = $false

# Options
$_bootstrap = $false
$_bootstrapConfiguration = "Release"
$_binaryLog = $false
$_buildServerLog = $false
$_ci = $false
$_procdump = $false
$_runAnalyzers = $false
$_deployExtensions = $false
$_prepareMachine = $false
$_useGlobalNuGetCache = $true
$_warnAsError = $false
$_sourceBuild = $false

# official build settings
$_officialBuildId = ""
$_officialSkipApplyOptimizationData = ""
$_officialSkipTests = ""
$_officialSourceBranchName = ""
$_officialIbcDrop = ""

$_majorVer = ""
$_minorVer = ""
$_patchVer = ""

# Test actions
$_test32 = $false
$_test64 = $false
$_testVsi = $false
$_testDesktop = $false
$_testCoreClr = $false
$_testIOperation = $false
$_sequential = $false

[parameter(ValueFromRemainingArguments=$true)][string[]]$properties

function Print-Usage() {
  Write-Host "Common settings:"
  Write-Host "  -configuration <value>    Build configuration: 'Debug' or 'Release' (short: -c)"
  Write-Host "  -verbosity <value>        Msbuild verbosity: q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic]"
  Write-Host "  -deployExtensions         Deploy built vsixes (short: -d)"
  Write-Host "  -binaryLog                Create MSBuild binary log (short: -bl)"
  Write-Host "  -buildServerLog           Create Roslyn build server log"
  Write-Host ""
  Write-Host "Actions:"
  Write-Host "  -restore                  Restore packages (short: -r)"
  Write-Host "  -build                    Build main solution (short: -b)"
  Write-Host "  -rebuild                  Rebuild main solution"
  Write-Host "  -pack                     Build NuGet packages, VS insertion manifests and installer"
  Write-Host "  -sign                     Sign our binaries"
  Write-Host "  -publish                  Publish build artifacts (e.g. symbols)"
  Write-Host "  -launch                   Launch Visual Studio in developer hive"
  Write-Host "  -help                     Print help and exit"
  Write-Host ""
  Write-Host "Test actions"
  Write-Host "  -test32                   Run unit tests in the 32-bit runner"
  Write-Host "  -test64                   Run units tests in the 64-bit runner"
  Write-Host "  -testDesktop              Run Desktop unit tests (short: -test)"
  Write-Host "  -testCoreClr              Run CoreClr unit tests"
  Write-Host "  -testVsi                  Run all integration tests"
  Write-Host "  -testIOperation           Run extra checks to validate IOperations"
  Write-Host ""
  Write-Host "Advanced settings:"
  Write-Host "  -ci                       Set when running on CI server"
  Write-Host "  -bootstrap                Build using a bootstrap compilers"
  Write-Host "  -bootstrapConfiguration   Build configuration for bootstrap compiler: 'Debug' or 'Release'"
  Write-Host "  -msbuildEngine <value>    Msbuild engine to use to run build ('dotnet', 'vs', or unspecified)."
  Write-Host "  -procdump                 Monitor test runs with procdump"
  Write-Host "  -runAnalyzers             Run analyzers during build operations (short: -a)"
  Write-Host "  -prepareMachine           Prepare machine for CI run, clean up processes after build"
  Write-Host "  -useGlobalNuGetCache      Use global NuGet cache."
  Write-Host "  -warnAsError              Treat all warnings as errors"
  Write-Host "  -sourceBuild              Simulate building source-build"
  Write-Host ""
  Write-Host "Official build settings:"
  Write-Host "  -officialBuildId                            An official build id, e.g. 20190102.3"
  Write-Host "  -officialSkipTests <bool>                   Pass 'true' to not run tests"
  Write-Host "  -officialSkipApplyOptimizationData <bool>   Pass 'true' to not apply optimization data"
  Write-Host "  -officialSourceBranchName <string>          The source branch name"
  Write-Host "  -officialIbcDrop <string>                   IBC data drop to use (e.g. 'ProfilingOutputs/DevDiv/VS/..')."
  Write-Host "                                              'default' for the most recent available for the branch."
  Write-Host ""
  Write-Host "Command line arguments starting with '/p:' are passed through to MSBuild."
}

# Process the command line arguments and establish defaults for the values which are not
# specified.
#
# In this function it's okay to use two arguments to extend the effect of another. For
# example it's okay to look at $_testVsi and infer $_runAnalyzers. It's not okay though to infer
# $_build based on say $_testDesktop. It's possible the developer wanted only for testing
# to execute, not any build.
function Process-Arguments() {
  function OfficialBuildOnly([string]$argName) {
    if ((Get-Variable $argName -Scope Script).Value) {
      if (!$_officialBuildId) {
        Write-Host "$argName can only be specified for official builds"
        exit 1
      }
    } else {
      if ($_officialBuildId) {
        Write-Host "$argName must be specified in official builds"
        exit 1
      }
    }
  }

  if ($_help -or (($properties -ne $null) -and ($properties.Contains("/help") -or $properties.Contains("/?")))) {
       Print-Usage
       exit 0
  }

  OfficialBuildOnly "_officialSkipTests"
  OfficialBuildOnly "_officialSkipApplyOptimizationData"
  OfficialBuildOnly "_officialSourceBranchName"

  if ($_officialBuildId) {
    $script:useGlobalNuGetCache = $false
    $script:procdump = $true
    $script:testDesktop = ![System.Boolean]::Parse($_officialSkipTests)
    $script:applyOptimizationData = ![System.Boolean]::Parse($_officialSkipApplyOptimizationData)
  } else {
    $script:applyOptimizationData = $false
  }

  if ($_ci) {
    $script:binaryLog = $true
    if ($_bootstrap) {
      $script:buildServerLog = $true
    }
  }

  if ($_test32 -and $_test64) {
    Write-Host "Cannot combine -test32 and -test64"
    exit 1
  }

  $anyUnit = $_testDesktop -or $_testCoreClr
  if ($anyUnit -and $_testVsi) {
    Write-Host "Cannot combine unit and VSI testing"
    exit 1
  }

  if ($_testVsi) {
    # Avoid spending time in analyzers when requested, and also in the slowest integration test builds
    $script:runAnalyzers = $false
    $script:bootstrap = $false
  }

  if ($_build -and $_launch -and -not $_deployExtensions) {
    Write-Host -ForegroundColor Red "Cannot combine -build and -launch without -deployExtensions"
    exit 1
  }

  if ($_bootstrap) {
    $script:restore = $true
  }

  $script:test32 = -not $_test64

  foreach ($property in $properties) {
    if (!$property.StartsWith("/p:", "InvariantCultureIgnoreCase")) {
      Write-Host "Invalid argument: $property"
      Print-Usage
      exit 1
    }
  }
}

function BuildSolution() {
  $solution = "Roslyn.sln"

  Write-Host "$($solution):"

  $bl = if ($_binaryLog) { "/bl:" + (Join-Path $LogDir "Build.binlog") } else { "" }

  if ($_buildServerLog) {
    ${env:ROSLYNCOMMANDLINELOGFILE} = Join-Path $LogDir "Build.Server.log"
  }

  $projects = Join-Path $RepoRoot $solution
  $toolsetBuildProj = InitializeToolset

  $testTargetFrameworks = if ($_testCoreClr) { 'net5.0%3Bnetcoreapp3.1' } else { "" }
  
  $ibcDropName = GetIbcDropName

  # Do not set this property to true explicitly, since that would override values set in projects.
  $suppressExtensionDeployment = if (!$_deployExtensions) { "/p:DeployExtension=false" } else { "" } 

  # The warnAsError flag for MSBuild will promote all warnings to errors. This is true for warnings
  # that MSBuild output as well as ones that custom tasks output.
  $msbuildWarnAsError = if ($_warnAsError) { "/warnAsError" } else { "" }

  # Workaround for some machines in the AzDO pool not allowing long paths (%5c is msbuild escaped backslash)
  $ibcDir = Join-Path $RepoRoot ".o%5c"

  # Set DotNetBuildFromSource to 'true' if we're simulating building for source-build.
  $buildFromSource = if ($_sourceBuild) { "/p:DotNetBuildFromSource=true" } else { "" }

  # If we are using msbuild.exe restore using static graph
  # This check can be removed and turned on for all builds once roslyn depends on a .NET Core SDK
  # that has a new enough msbuild for the -graph switch to be present
  $restoreUseStaticGraphEvaluation = if ($_msbuildEngine -ne 'dotnet') { "/p:RestoreUseStaticGraphEvaluation=true" } else { "" }
  
  # copy the custom toolset build file
  $toolsetBuildFolder = [System.IO.Path]::GetDirectoryName($toolsetBuildProj)
  $buildProjFile = [System.IO.Path]::GetFullPath($PSScriptRoot + "\build.proj")
  $toolsetBuildProjFile = [System.IO.Path]::GetFullPath($toolsetBuildFolder + "\RSBuild.proj")
  if (!(Test-Path -path $toolsetBuildProjFile -PathType Leaf)) {
    Copy-Item -Path $buildProjFile $toolsetBuildProjFile -Force
  }

  try {
    MSBuild $toolsetBuildProjFile `
      $bl `
      /p:Configuration=$_configuration `
      /p:Projects=$projects `
      /p:RepoRoot=$RepoRoot `
      /p:Restore=$_restore `
      /p:Build=$_build `
      /p:Test=$_testCoreClr `
      /p:Rebuild=$_rebuild `
      /p:Pack=$_pack `
      /p:Sign=$_sign `
      /p:Publish=$_publish `
      /p:ContinuousIntegrationBuild=$_ci `
      /p:OfficialBuildId=$_officialBuildId `
      /p:UseRoslynAnalyzers=$_runAnalyzers `
      /p:BootstrapBuildPath=$bootstrapDir `
      /p:TestTargetFrameworks=$testTargetFrameworks `
      /p:TreatWarningsAsErrors=$_warnAsError `
      /p:EnableNgenOptimization=$applyOptimizationData `
      /p:IbcOptimizationDataDir=$ibcDir `
      /p:RSMajorVersion=$_majorVer `
      /p:RSMinorVersion=$_minorVer `
      /p:RSPatchVersion=$_patchVer `
      $restoreUseStaticGraphEvaluation `
      /p:VisualStudioIbcDrop=$ibcDropName `
      $suppressExtensionDeployment `
      $msbuildWarnAsError `
      $buildFromSource `
      @properties
  }
  finally {
    ${env:ROSLYNCOMMANDLINELOGFILE} = $null
  }
}


# Get the branch that produced the IBC data this build is going to consume.
# IBC data are only merged in official built, but we want to test some of the logic in CI builds as well.
function GetIbcSourceBranchName() {
  if (Test-Path variable:global:_IbcSourceBranchName) {
      return $global:_IbcSourceBranchName
  }

  function calculate {
    $fallback = "master"

    $branchData = GetBranchPublishData $_officialSourceBranchName
    if ($branchData -eq $null) {
      Write-Host "Warning: Branch $_officialSourceBranchName is not listed in PublishData.json. Using IBC data from '$fallback'." -ForegroundColor Yellow
      Write-Host "Override by setting IbcDrop build variable." -ForegroundColor Yellow
      return $fallback
    }

    return $branchData.vsBranch
  }

  return $global:_IbcSourceBranchName = calculate
}

function GetIbcDropName() {

    if ($_officialIbcDrop -and $_officialIbcDrop -ne "default"){
        return $_officialIbcDrop
    }

    # Don't try and get the ibc drop if we're not in an official build as it won't be used anyway
    if (!$_officialBuildId) {
        return ""
    }

    # Bring in the ibc tools
    $packagePath = Join-Path (Get-PackageDir "Microsoft.DevDiv.Optimization.Data.PowerShell") "lib\net461"
    Import-Module (Join-Path $packagePath "Optimization.Data.PowerShell.dll")
    
    # Find the matching drop
    $branch = GetIbcSourceBranchName
    Write-Host "Optimization data branch name is '$branch'."

    $drop = Find-OptimizationInputsStoreForBranch -ProjectName "DevDiv" -RepositoryName "VS" -BranchName $branch
    return $drop.Name
}

# Set VSO variables used by MicroBuildBuildVSBootstrapper pipeline task
function SetVisualStudioBootstrapperBuildArgs() {
  $fallbackBranch = "master-vs-deps"

  $branchName = if ($_officialSourceBranchName) { $_officialSourceBranchName } else { $fallbackBranch }
  $branchData = GetBranchPublishData $branchName

  if ($branchData -eq $null) {
    Write-Host "Warning: Branch $_officialSourceBranchName is not listed in PublishData.json. Using VS bootstrapper for branch '$fallbackBranch'. " -ForegroundColor Yellow
    $branchData = GetBranchPublishData $fallbackBranch
  }

  # VS branch name is e.g. "lab/d16.0stg", "rel/d15.9", "lab/ml", etc.
  $vsBranchSimpleName = $branchData.vsBranch.Split('/')[-1]
  $vsMajorVersion = $branchData.vsMajorVersion
  $vsChannel = "int.$vsBranchSimpleName"

  Write-Host "##vso[task.setvariable variable=VisualStudio.MajorVersion;]$vsMajorVersion"        
  Write-Host "##vso[task.setvariable variable=VisualStudio.ChannelName;]$vsChannel"

  $insertionDir = Join-Path $VSSetupDir "Insertion"
  $manifestList = [string]::Join(',', (Get-ChildItem "$insertionDir\*.vsman"))
  Write-Host "##vso[task.setvariable variable=VisualStudio.SetupManifestList;]$manifestList"
}

# Core function for running our unit / integration tests tests
function TestUsingOptimizedRunner() {

  # Tests need to locate .NET Core SDK
  $dotnet = InitializeDotNetCli

  if ($_testVsi) {
    Deploy-VsixViaTool

    if ($_ci) {
      # Minimize all windows to avoid interference during integration test runs
      $shell = New-Object -ComObject "Shell.Application"
      $shell.MinimizeAll()

      # Set registry to take dump automatically when test process crashes
      reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps" /f
      reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps" /f /v DumpType /t REG_DWORD /d 2
      reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps" /f /v DumpCount /t REG_DWORD /d 2
      reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps" /f /v DumpFolder /t REG_SZ /d "$LogDir"
    }
  }

  if ($_testIOperation) {
    $env:ROSLYN_TEST_IOPERATION = "true"
  }

  $secondaryLogDir = Join-Path (Join-Path $ArtifactsDir "log2") $_configuration
  Create-Directory $secondaryLogDir
  $testResultsDir = Join-Path $ArtifactsDir "TestResults\$_configuration"
  $binDir = Join-Path $ArtifactsDir "bin" 
  $runTests = GetProjectOutputBinary "RunTests.exe"

  if (!(Test-Path $runTests)) {
    Write-Host "Test runner not found: '$runTests'. Run Build.cmd first." -ForegroundColor Red 
    ExitWithExitCode 1
  }

  $xunitDir = Join-Path (Get-PackageDir "xunit.runner.console") "tools\net472"
  $args = "`"$xunitDir`""
  $args += " `"-out:$testResultsDir`""
  $args += " `"-logs:$LogDir`""
  $args += " `"-secondaryLogs:$secondaryLogDir`""
  $args += " -nocache"
  $args += " -tfm:net472"

  if ($_testDesktop -or $_testIOperation) {
    if ($_test32) {
      $dlls = Get-ChildItem -Recurse -Include "*.UnitTests.dll" $binDir
    } else {
      $dlls = Get-ChildItem -Recurse -Include "*.UnitTests.dll" -Exclude "*InteractiveHost*" $binDir
    }
  } elseif ($_testVsi) {
    # Since they require Visual Studio to be installed, ensure that the MSBuildWorkspace tests run along with our VS
    # integration tests in CI.
    if ($_ci) {
      $dlls += @(Get-Item (GetProjectOutputBinary "Microsoft.CodeAnalysis.Workspaces.MSBuild.UnitTests.dll"))
    }

    $dlls += @(Get-ChildItem -Recurse -Include "*.IntegrationTests.dll" $binDir)
    $args += " -testVsi"
  } else {
    $dlls = Get-ChildItem -Recurse -Include "*.IntegrationTests.dll" $binDir
    $args += " -trait:Feature=NetCore"
  }

  # Exclude out the multi-targetted netcore app projects
  $dlls = $dlls | ?{ -not ($_.FullName -match ".*netcoreapp.*") }
  $dlls = $dlls | ?{ -not ($_.FullName -match ".*net5.0.*") }

  # Exclude out the ref assemblies
  $dlls = $dlls | ?{ -not ($_.FullName -match ".*\\ref\\.*") }
  $dlls = $dlls | ?{ -not ($_.FullName -match ".*/ref/.*") }

  if ($_configuration -eq 'Debug') {
    $excludedConfiguration = 'Release'
  } else {
    $excludedConfiguration = 'Debug'
  }

  $dlls = $dlls | ?{ -not (($_.FullName -match ".*\\$excludedConfiguration\\.*") -or ($_.FullName -match ".*/$excludedConfiguration/.*")) }

  if ($_ci) {
    $args += " -xml"
    if ($_testVsi) {
      $args += " -timeout:110"
    } else {
      $args += " -timeout:90"
    }
  }

  $procdumpPath = Ensure-ProcDump
  $args += " -procdumppath:$procDumpPath"
  if ($_procdump) {
    $args += " -useprocdump";
  }

  if ($_test64) {
    $args += " -test64"
  }

  if ($_sequential) {
    $args += " -sequential"
  }

  foreach ($dll in $dlls) {
    $args += " $dll"
  }

  try {
    Exec-Console $runTests $args
  } finally {
    Get-Process "xunit*" -ErrorAction SilentlyContinue | Stop-Process
    if ($_testIOperation) {
      Remove-Item env:\ROSLYN_TEST_IOPERATION
    }

    if ($_testVsi) {
      Write-Host "Copying ServiceHub logs to $LogDir"
      Copy-Item -Path (Join-Path $TempDir "servicehub\logs") -Destination (Join-Path $LogDir "servicehub") -Recurse
    }
  }
}

function EnablePreviewSdks() {
  $vsInfo = LocateVisualStudio
  if ($vsInfo -eq $null) {
    # Preview SDKs are allowed when no Visual Studio instance is installed
    return
  }

  $vsId = $vsInfo.instanceId
  $vsMajorVersion = $vsInfo.installationVersion.Split('.')[0]

  $instanceDir = Join-Path ${env:USERPROFILE} "AppData\Local\Microsoft\VisualStudio\$vsMajorVersion.0_$vsId"
  Create-Directory $instanceDir
  $sdkFile = Join-Path $instanceDir "sdk.txt"
  'UsePreviews=True' | Set-Content $sdkFile
}

# Deploy our core VSIX libraries to Visual Studio via the Roslyn VSIX tool.  This is an alternative to
# deploying at build time.
function Deploy-VsixViaTool() { 
  $vsixDir = Get-PackageDir "RoslynTools.VSIXExpInstaller"
  $vsixExe = Join-Path $vsixDir "tools\VsixExpInstaller.exe"
  
  $vsInfo = LocateVisualStudio
  if ($vsInfo -eq $null) {
    throw "Unable to locate required Visual Studio installation"
  }

  $vsDir = $vsInfo.installationPath.TrimEnd("\")
  $vsId = $vsInfo.instanceId
  $vsMajorVersion = $vsInfo.installationVersion.Split('.')[0]
  $displayVersion = $vsInfo.catalog.productDisplayVersion

  $hive = "RoslynDev"
  Write-Host "Using VS Instance $vsId ($displayVersion) at `"$vsDir`""
  $baseArgs = "/rootSuffix:$hive /vsInstallDir:`"$vsDir`""

  Write-Host "Uninstalling old Roslyn VSIX"

  # Actual uninstall is failing at the moment using the uninstall options. Temporarily using
  # wildfire to uninstall our VSIX extensions
  $extDir = Join-Path ${env:USERPROFILE} "AppData\Local\Microsoft\VisualStudio\$vsMajorVersion.0_$vsid$hive"
  if (Test-Path $extDir) {
    foreach ($dir in Get-ChildItem -Directory $extDir) {
      $name = Split-Path -leaf $dir
      Write-Host "`tUninstalling $name"
    }
    Remove-Item -re -fo $extDir
  }

  Write-Host "Installing all Roslyn VSIX"

  # VSIX files need to be installed in this specific order:
  $orderedVsixFileNames = @(
    "Roslyn.Compilers.Extension.vsix",
    "Roslyn.VisualStudio.Setup.vsix",
    "Roslyn.VisualStudio.Setup.Dependencies.vsix",
    "ExpressionEvaluatorPackage.vsix",
    "Roslyn.VisualStudio.DiagnosticsWindow.vsix",
    "Microsoft.VisualStudio.IntegrationTest.Setup.vsix")

  foreach ($vsixFileName in $orderedVsixFileNames) {
    $vsixFile = Join-Path $VSSetupDir $vsixFileName
    $fullArg = "$baseArgs $vsixFile"
    Write-Host "`tInstalling $vsixFileName"
    Exec-Console $vsixExe $fullArg
  }
}

# Ensure that procdump is available on the machine.  Returns the path to the directory that contains
# the procdump binaries (both 32 and 64 bit)
function Ensure-ProcDump() {

  # Jenkins images default to having procdump installed in the root.  Use that if available to avoid
  # an unnecessary download.
  if (Test-Path "C:\SysInternals\procdump.exe") {
    return "C:\SysInternals"
  }

  $outDir = Join-Path $ToolsDir "ProcDump"
  $filePath = Join-Path $outDir "procdump.exe"
  if (-not (Test-Path $filePath)) {
    Remove-Item -Re $filePath -ErrorAction SilentlyContinue
    Create-Directory $outDir
    $zipFilePath = Join-Path $toolsDir "procdump.zip"
    Invoke-WebRequest "https://download.sysinternals.com/files/Procdump.zip" -UseBasicParsing -outfile $zipFilePath | Out-Null
    Unzip $zipFilePath $outDir
  }

  return $outDir
}

# Setup the CI machine for running our integration tests.
function Setup-IntegrationTestRun() {
  $processesToStopOnExit += "devenv"
  $screenshotPath = (Join-Path $LogDir "StartingBuild.png")
  try {
    Capture-Screenshot $screenshotPath
  }
  catch {
    Write-Host "Screenshot failed; attempting to connect to the console"

    # Keep the session open so we have a UI to interact with
    $quserItems = ((quser $env:USERNAME | select -Skip 1) -split '\s+')
    $sessionid = $quserItems[2]
    if ($sessionid -eq 'Disc') {
      # When the session isn't connected, the third value is 'Disc' instead of the ID
      $sessionid = $quserItems[1]
    }

    if ($quserItems[1] -eq 'console') {
      Write-Host "Disconnecting from console before attempting reconnection"
      try {
        tsdiscon
      } catch {
        # ignore
      }

      # Disconnection is asynchronous, so wait a few seconds for it to complete
      Start-Sleep -Seconds 3
      query user
    }

    Write-Host "tscon $sessionid /dest:console"
    tscon $sessionid /dest:console

    # Connection is asynchronous, so wait a few seconds for it to complete
    Start-Sleep 3
    query user

    # Make sure we can capture a screenshot. An exception at this point will fail-fast the build.
    Capture-Screenshot $screenshotPath
  }
}

function Prepare-TempDir() {
  $env:TEMP=$TempDir
  $env:TMP=$TempDir

  Copy-Item (Join-Path $RepoRoot "src\Workspaces\MSBuildTest\Resources\.editorconfig") $TempDir
  Copy-Item (Join-Path $RepoRoot "src\Workspaces\MSBuildTest\Resources\Directory.Build.props") $TempDir
  Copy-Item (Join-Path $RepoRoot "src\Workspaces\MSBuildTest\Resources\Directory.Build.targets") $TempDir
  Copy-Item (Join-Path $RepoRoot "src\Workspaces\MSBuildTest\Resources\Directory.Build.rsp") $TempDir
  Copy-Item (Join-Path $RepoRoot "src\Workspaces\MSBuildTest\Resources\NuGet.Config") $TempDir
}

function List-Processes() {
  Write-Host "Listing running build processes..."
  Get-Process -Name "msbuild" -ErrorAction SilentlyContinue | Out-Host
  Get-Process -Name "vbcscompiler" -ErrorAction SilentlyContinue | Out-Host
  Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | where { $_.Modules | select { $_.ModuleName -eq "VBCSCompiler.dll" } } | Out-Host
  Get-Process -Name "devenv" -ErrorAction SilentlyContinue | Out-Host
}

function RunBuild {
  Param (
    [string][Alias('c')]$configuration = "Debug",
    [string][Alias('v')]$verbosity = "m",
    [string]$msbuildEngine = "vs",

    # Actions
    [switch][Alias('r')]$restore,
    [switch][Alias('b')]$build,
    [switch]$rebuild,
    [switch]$sign,
    [switch]$pack,
    [switch]$publish,
    [switch]$launch,
    [switch]$help,

    # Options
    [switch]$bootstrap,
    [string]$bootstrapConfiguration = "Release",
    [switch][Alias('bl')]$binaryLog,
    [switch]$buildServerLog,
    [switch]$ci,
    [switch]$procdump,
    [switch][Alias('a')]$runAnalyzers,
    [switch][Alias('d')]$deployExtensions,
    [switch]$prepareMachine,
    [switch]$useGlobalNuGetCache = $true,
    [switch]$warnAsError = $false,
    [switch]$sourceBuild = $false,

    # official build settings
    [string]$officialBuildId = "",
    [string]$officialSkipApplyOptimizationData = "",
    [string]$officialSkipTests = "",
    [string]$officialSourceBranchName = "",
    [string]$officialIbcDrop = "",

    # version
    [string]$majorVer = "",
    [string]$minorVer = "",
    [string]$patchVer = "",

    # Test actions
    [switch]$test32,
    [switch]$test64,
    [switch]$testVsi,
    [switch][Alias('test')]$testDesktop,
    [switch]$testCoreClr,
    [switch]$testIOperation,
    [switch]$sequential,

    [parameter(ValueFromRemainingArguments=$true)][string[]]$properties
  )

  Set-StrictMode -version 2.0
  $ErrorActionPreference = "Stop"

  $_configuration = $configuration
  $_verbosity = $verbosity
  $_msbuildEngine = $msbuildEngine

  # Actions
  $_restore = $restore
  $_build = $build
  $_rebuild = $rebuild
  $_sign = $sign
  $_pack = $pack
  $_publish = $publish
  $_launch = $launch
  $_help = $help

  # Options
  $_bootstrap = $bootstrap
  $_bootstrapConfiguration = $bootstrapConfiguration
  $_binaryLog = $binaryLog
  $_buildServerLog = $buildServerLog
  $_ci = $ci
  $_procdump = $procdump
  $_runAnalyzers = $runAnalyzers
  $_deployExtensions = $deployExtensions
  $_prepareMachine = $prepareMachine
  $_useGlobalNuGetCache = $useGlobalNuGetCache
  $_warnAsError = $warnAsError
  $_sourceBuild = $sourceBuild

  # official build settings
  $_officialBuildId = $officialBuildId
  $_officialSkipApplyOptimizationData = $officialSkipApplyOptimizationData
  $_officialSkipTests = $officialSkipTests
  $_officialSourceBranchName = $officialSourceBranchName
  $_officialIbcDrop = $officialIbcDrop

  $_majorVer = $majorVer
  $_minorVer = $minorVer
  $_patchVer = $patchVer

  # Test actions
  $_test32 = $test32
  $_test64 = $test64
  $_testVsi = $testVsi
  $_testDesktop = $testDesktop
  $_testCoreClr = $testCoreClr
  $_testIOperation = $testIOperation
  $_sequential = $sequential

  try {
    if ($PSVersionTable.PSVersion.Major -lt "5") {
      Write-Host "PowerShell version must be 5 or greater (version $($PSVersionTable.PSVersion) detected)"
      return 1
    }

    $regKeyProperty = Get-ItemProperty -Path HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem -Name "LongPathsEnabled" -ErrorAction Ignore
    if (($null -eq $regKeyProperty) -or ($regKeyProperty.LongPathsEnabled -ne 1)) {
      Write-Host "LongPath is not enabled, you may experience build errors. You can avoid these by enabling LongPath with `"reg ADD HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem /v LongPathsEnabled /t REG_DWORD /d 1`""
    }

    Process-Arguments

    . (Join-Path $PSScriptRoot "build-utils.ps1")

    if ($_testVsi) {
      . (Join-Path $PSScriptRoot "build-utils-win.ps1")
    }

    Push-Location $RepoRoot

    if ($_ci) {
      List-Processes
      Prepare-TempDir
      EnablePreviewSdks
      if ($_testVsi) {
        Setup-IntegrationTestRun 
      }

      $global:_DotNetInstallDir = Join-Path $RepoRoot ".dotnet"
      InstallDotNetSdk $global:_DotNetInstallDir $GlobalJson.tools.dotnet
    }

    if ($_restore) {
      &(Ensure-DotNetSdk) tool restore
    }

    try
    {
      if ($_bootstrap) {
        $bootstrapDir = Make-BootstrapBuild -force32:$_test32
      }
    }
    catch
    {
      if ($_ci) {
        echo "##vso[task.logissue type=error](NETCORE_ENGINEERING_TELEMETRY=Build) Build failed"
      }
      throw $_
    }

    if ($_restore -or $_build -or $_rebuild -or $_pack -or $_sign -or $_publish -or $_testCoreClr) {
      BuildSolution
    }

    if ($_ci -and $_build -and $_msbuildEngine -eq "vs") {
      SetVisualStudioBootstrapperBuildArgs
    }

    try
    {
      if ($_testDesktop -or $_testVsi -or $_testIOperation) {
        TestUsingOptimizedRunner
      }
    }
    catch
    {
      if ($_ci) {
        echo "##vso[task.logissue type=error](NETCORE_ENGINEERING_TELEMETRY=Test) Tests failed"
      }
      throw $_
    }

    if ($_launch) {
      if (-not $_build) {
        InitializeBuildTool
      }

      $devenvExe = Join-Path $env:VSINSTALLDIR 'Common7\IDE\devenv.exe'
      &$devenvExe /rootSuffix RoslynDev
    }

    return 0
  }
  catch {
    Write-Host $_
    Write-Host $_.Exception
    Write-Host $_.ScriptStackTrace
    return 1
  }
  finally {
    if ($_ci) {
      Stop-Processes
    }
    Pop-Location
  }
}