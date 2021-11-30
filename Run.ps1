param 
(
    $RunProject  # what to run (Orange, Tangerine, Games)
)

$RequiredMajorVersion = "5"
$SdkVersion = "5.0.400"
$RuntimeVersion = "5.0.2"

function Start-Citrus
{
    param
    (
        $RunProject
    )

    $LauncherPath = Join-Path -Path (Split-Path -Path ($MyInvocation.ScriptName)) -ChildPath "Orange\Launcher\bin\Win\Release\Launcher.exe"

    if ($LauncherPath)
    {
        switch ($RunProject) 
        {
            "Orange" 
            {
                & $LauncherPath
            }
            "Tangerine"
            {
                & $LauncherPath -b Tangerine\Tangerine.Win.sln -r Tangerine\Tangerine\bin\Release\Tangerine.exe
            }
        }
    }
    else
    {
        Write-Error "Launcher.exe not found, please checkout repo!"
    }
}

function Test-IfDotnetCorrectVersion
{
    if ([Bool](Get-Command -Name "dotnet" -ErrorAction SilentlyContinue))
    {
        $DotnetMajorVersion = dotnet --list-sdks | ForEach-Object -Process { $_.Split(" ")[0] } | ForEach-Object {[Version]$_} | Sort-Object | Select-Object -Last 1 | Select-Object -ExpandProperty Major
        $RuntimeMajorVersion = dotnet --list-runtimes | ForEach-Object -Process { $_.Split(" ")[1] } | ForEach-Object {[Version]$_} | Sort-Object | Select-Object -Last 1 | Select-Object -ExpandProperty Major

        return (($DotnetMajorVersion -ge $RequiredMajorVersion) -and ($RuntimeMajorVersion -ge $RequiredMajorVersion))
    }
    else 
    {
        return $False
    }
}

if (Test-IfDotnetCorrectVersion)
{
    Start-Citrus -RunProject $RunProject
}
else
{
    $DotnetPath = Join-Path -Path (Split-Path -Path ($MyInvocation.ScriptName)) -ChildPath "dotnet"
    Invoke-WebRequest -UseBasicParsing https://dot.net/v1/dotnet-install.ps1 -Outfile dotnet-install.ps1
    & './dotnet-install.ps1' -Architecture x64 -Version $SdkVersion -InstallDir $DotnetPath
    & './dotnet-install.ps1' -Architecture x64 -Runtime dotnet -Version $RuntimeVersion -InstallDir $DotnetPath
    Start-Citrus -RunProject $RunProject
}
