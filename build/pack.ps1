param ([string]$configuration = "Release",
	[string]$publishPackages = "false",
	[string]$packageVersion = "")

#Make sure the script runs in the right context, might be wrong if started from e.g. .cmd file
$cwd = Split-Path -parent $PSCommandPath
pushd $cwd

Write-Host "Pack artifacts"
Write-Host $configuration
Write-Host $publishPackages
Write-Host $packageVersion

$artifactsPath = Resolve-Path "$cwd\..\artifacts"

if (!(Test-Path $artifactsPath))
{
	New-Item -ItemType directory -Path $artifactsPath
}

# Creating NuGet packages
#TODO: refactor to Powershell scripts
& "$cwd\generatepackages"
& "$cwd\generatepackagesformessaging"
& "$cwd\generatepackagesfortestpages"
& "$cwd\generatepackagesforkpi"
& "$cwd\generatepackagesforkpicommerce"


# Creating daily site package.
# Copying database file to the site folder:
Write-Host "Pack daily site files"
if (Test-Path $artifactsPath\DailySite.zip)
{
	Remove-Item $artifactsPath\DailySite.zip -Force
}

Copy-Item .\resources\AlloyEPiServerDB.mdf ..\samples\EPiServer.Templates.Alloy\App_Data

.\buildzip.ps1 $cwd\..\samples\EPiServer.Templates.Alloy $artifactsPath\DailySite.zip

Copy-Item .\resources\ConnectionString.xmlupdate $artifactsPath

& "$cwd\resources\nuget\nuget.exe" pack "$cwd\resources\DailySite.nuspec" -Prop Configuration=$configuration -Version $packageVersion -Verbosity detailed -NoDefaultExcludes -NoPackageAnalysis -BasePath $artifactsPath -OutputDirectory $artifactsPath