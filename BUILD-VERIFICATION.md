# Build verification

The source was checked for project structure, required files, hard-coded database credentials, and workflow configuration.

The execution environment used to package this archive does not contain the .NET SDK (`dotnet` is unavailable), so a native `dotnet build`/`dotnet test` could not be executed here. The included GitHub Actions workflow runs restore, build, and test on `windows-latest` with .NET 8.

Before first deployment on Windows, run:

```powershell
dotnet restore DroidTrace.sln
dotnet build DroidTrace.sln --configuration Release
dotnet test DroidTrace.sln --configuration Release
```
