$Path = "dotnet"
$Arch = "x64"
$SDKVersion = "5.0.400"
$RuntimeVersion = "5.0.2"
$Runtime = "dotnet"
If(!(test-path $Path))
{
    New-Item -ItemType "directory" -Path $Path
}
Invoke-WebRequest -UseBasicParsing https://dot.net/v1/dotnet-install.ps1 -Outfile $Path/dotnet-install.ps1
Invoke-Expression "&'$Path/dotnet-install.ps1' -Architecture $Arch -Version $SDKVersion -InstallDir $Path"
Invoke-Expression "&'$Path/dotnet-install.ps1' -Architecture $Arch -Runtime $Runtime -Version $RuntimeVersion -InstallDir $Path"
dotnet nuget locals all --clear
