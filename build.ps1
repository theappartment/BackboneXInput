param([switch]$Publish)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet restore BackboneXInput.sln
    if ($LASTEXITCODE -ne 0) { throw 'Restore fallito' }
    dotnet build BackboneXInput.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build fallita' }
    dotnet run --project tests/BackboneXInput.Tests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Test falliti' }
    if ($Publish) {
        dotnet publish src/BackboneXInput -c Release -r win-x64 --self-contained true -o windows-x64
        if ($LASTEXITCODE -ne 0) { throw 'Publish fallito' }
        dotnet publish src/BackboneXInput.Tray -c Release -r win-x64 --self-contained true -o windows-x64
        if ($LASTEXITCODE -ne 0) { throw 'Publish Tray fallito' }
    }
} finally { Pop-Location }
