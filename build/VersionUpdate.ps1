Param([string]$branchName, [Int]$buildCounter)

# Updates the AssemblyInformationalVersion property in the AssemblyVersionAuto.cs file based
# on the AssemblyVersion attribute and the currently built branch

if(!$branchName -or !$buildCounter) {
    Write-Error "`$branchName and `$buildCounter paramteres must be supplied"
    exit 1
}


foreach ($item in Get-ChildItem -Recurse -Filter 'AssemblyVersionAuto.cs')
{
    $assemblyVersionFile = $item.FullName

    $match = (Select-String -Path $assemblyVersionFile -Pattern 'AssemblyVersion[^\d]*([\d+.]+)').Matches[0]
    $assemblyVersion = $match.Groups[1].Value

    if (!$assemblyVersion) {
        Write-Error "Failed to parse version information"
        exit 1
    }

    switch -wildcard ($branchName) {
        "master"   { $preReleaseInfo = "" }
        "master-*" { $preReleaseInfo = "" }
        "release*" { $preReleaseInfo = "-pre-{0:D6}"}
		"hotfix*" { $preReleaseInfo = "-pre-{0:D6}"}
        "dev"  { $preReleaseInfo = "-ci-{0:D6}" }
        default    { $preReleaseInfo = "-feature-{0:D6}" }
    }

    $informationalVersion  = "$assemblyVersion$preReleaseInfo" -f $buildCounter

    "AssemblyVersion: $assemblyVersion"
    "AssemblyInformationalVersion: $informationalVersion"

    # Publish the packageVersion parameter to team city
    "##teamcity[setParameter name='packageVersion' value='$informationalVersion']"

    # Replace the existing AssemblyInformationalVersion with the calculated one
    $fileContent = Get-Content $assemblyVersionFile | ForEach-Object { $_ -replace ".*AssemblyInformationalVersion.*", "" }
    Set-Content $assemblyVersionFile "$fileContent`n[assembly: AssemblyInformationalVersion(`"$informationalVersion`")]"
}