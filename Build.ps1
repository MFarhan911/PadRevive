$ErrorActionPreference = 'Stop'
$padReviveDotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $padReviveDotnet) { $padReviveDotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
if (-not (Test-Path -LiteralPath $padReviveDotnet)) { throw 'Install the .NET 10 SDK to build PadRevive.' }
Push-Location $PSScriptRoot
try {
    & $padReviveDotnet run --project tests/Bridge.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Mapping regression checks failed.' }
    & $padReviveDotnet run --project workflow-tests/Workflow.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Stick sensitivity and calibration workflow checks failed.' }
    & $padReviveDotnet run --project renderer-tests/Renderer.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Native WPF geometry, lighting and Test UI checks failed.' }
    & $padReviveDotnet publish GenericPadBridge/GenericPadBridge.csproj -c Release -r win-x64 --self-contained true -o single-exe
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Write-Host 'Ready: single-exe/GenericPadBridge.exe'
} finally {
    Pop-Location
}
