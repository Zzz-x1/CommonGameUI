param([string]$GodotPath)
$ErrorActionPreference = 'Stop'
$taskWorkspace = Split-Path -Parent $PSScriptRoot
& dotnet test (Join-Path $taskWorkspace 'tests/UIFramework.Tests/UIFramework.Tests.csproj') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Core / generator tests failed.' }
& dotnet test (Join-Path $taskWorkspace 'tests/UIFramework.Mvvm.Tests/UIFramework.Mvvm.Tests.csproj') --nologo
if ($LASTEXITCODE -ne 0) { throw 'MVVM tests failed.' }
$taskArguments = @{ Headless = $true; SelfTest = $true }
if ($GodotPath) { $taskArguments.GodotPath = $GodotPath }
& (Join-Path $PSScriptRoot 'Run-Demo.ps1') @taskArguments
if ($LASTEXITCODE -ne 0) { throw 'Godot client tests failed.' }
$taskArguments.Remove('SelfTest')
$taskArguments.VerifySave = $true
& (Join-Path $PSScriptRoot 'Run-Demo.ps1') @taskArguments
exit $LASTEXITCODE
