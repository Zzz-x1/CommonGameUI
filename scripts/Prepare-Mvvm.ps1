param()
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
dotnet build (Join-Path $taskRoot 'src/UIFramework.Mvvm.Weaver/UIFramework.Mvvm.Weaver.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'MVVM build failed.' }
$taskPackage = Join-Path $taskRoot 'src/UIFramework.Mvvm.Unity'
$taskRuntime = Join-Path $taskPackage 'Runtime/Plugins'
$taskEditor = Join-Path $taskPackage 'Editor/CodeGen/Plugins'
New-Item -ItemType Directory -Force $taskRuntime, $taskEditor | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRoot 'src/UIFramework.Mvvm/bin/Release/netstandard2.1/UIFramework.Mvvm.dll') -Destination $taskRuntime
Copy-Item -LiteralPath (Join-Path $taskRoot 'src/UIFramework.Mvvm.Weaver/bin/Release/netstandard2.1/UIFramework.Mvvm.Weaver.dll') -Destination $taskEditor
