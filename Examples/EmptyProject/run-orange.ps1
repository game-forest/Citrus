$Path = "dotnet"
$Arch = "x64"
$SDKVersion = "5.0.400"
$RuntimeVersion = "5.0.2"
$Project = "Orange"

function DownloadDotnet 
{
    param 
    (
        $Path,
        $Arch,
        $SDKVersion,
        $RuntimeVersion
    )

    If(!(test-path $Path))
    {
        New-Item -ItemType "directory" -Path $Path
    }
    Invoke-WebRequest -UseBasicParsing https://dot.net/v1/dotnet-install.ps1 -Outfile $Path/dotnet-install.ps1
    Invoke-Expression "&'$Path/dotnet-install.ps1' -NoPath -Architecture $Arch -Version $SDKVersion -InstallDir $Path"
    Invoke-Expression "&'$Path/dotnet-install.ps1' -NoPath -Architecture $Arch -Runtime dotnet -Version $RuntimeVersion -InstallDir $Path"
}

function SetRunEnviroment 
{
    $CurrentPath = Get-Location
    $DotnetPath = Join-Path -Path $CurrentPath -ChildPath "dotnet"
    $Env:DOTNET_ROOT = $DotnetPath
    $Env:Path = $DotnetPath + ";" + $env:Path
    dotnet nuget locals all --clear
}

function Run 
{
    param 
    (
        $SetEnviroment,
        $Project
    )

    if ($SetEnviroment -eq $True)
    {
        SetRunEnviroment
    }

    switch ($Project) 
    {
        "Orange" 
        {
            Invoke-Expression "&'.\..\..\Orange\Launcher\bin\Win\Release\Launcher.exe'"
        }
        "Tangerine" 
        {
            Invoke-Expression "&'.\..\..\Orange\Launcher\bin\Win\Release\Launcher.exe' -b Tangerine\Tangerine.Win.sln -r Tangerine\Tangerine\bin\Release\Tangerine.exe"
        }
    }
}

if (Get-Command -Name "dotnet" -errorAction Continue) 
{
    $CurrentDotnetVersion = Invoke-Expression -Command "dotnet --version"
    if ($CurrentDotnetVersion -ge $SDKVersion)
    {
        Write-Output "dotnet version is $CurrentDotnetVersion and it's OK. Running $Project"
        Run -Project $Project
    } 
    else
    {
        Write-Output "dotnet version is $CurrentDotnetVersion , but $SDKVersion or greater is needed. Downloading."
        DownloadDotnet -Path $Path -Arch $Arch -SDKVersion $SDKVersion -RuntimeVersion $RuntimeVersion
        Run -Project $Project -SetEnviroment True
    }
} 
else
{
    Write-Output "No compatible dotnet version is installed on this system. Downloading."
    DownloadDotnet -Path $Path -Arch $Arch -SDKVersion $SDKVersion -RuntimeVersion $RuntimeVersion
    Run -Project $Project -SetEnviroment True
}
