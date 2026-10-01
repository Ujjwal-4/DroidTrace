param([string]$Runtime = "win-x64")
$ErrorActionPreference = "Stop"
dotnet restore .\DroidTrace.sln
dotnet build .\DroidTrace.sln --configuration Release
dotnet test .\DroidTrace.sln --configuration Release --no-build
dotnet publish .\src\DroidTrace\DroidTrace.csproj --configuration Release --runtime $Runtime --self-contained true --output .\publish\$Runtime
Write-Host "Published to .\publish\$Runtime"
