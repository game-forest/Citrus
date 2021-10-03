$CurrentPath = Get-Location
$DotnetPath = Join-Path -Path $CurrentPath -ChildPath "dotnet"
$Env:DOTNET_ROOT = $DotnetPath
$Env:Path += ';' + $DotnetPath
Invoke-Expression "&'.\..\..\Orange\Launcher\bin\Win\Release\Launcher.exe' -b Tangerine\Tangerine.Win.sln -r Tangerine\Tangerine\bin\Release\Tangerine.exe"
