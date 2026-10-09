param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
dotnet build (Join-Path $taskRoot 'src/UIFramework.Mvvm.Weaver/UIFramework.Mvvm.Weaver.csproj') -c $Configuration -p:DebugType=portable -p:DebugSymbols=true --nologo
if ($LASTEXITCODE -ne 0) { throw 'MVVM build failed.' }
$taskPackage = Join-Path $taskRoot 'src/UIFramework.Mvvm.Unity'
$taskRuntime = Join-Path $taskPackage 'Runtime/Plugins'
$taskEditor = Join-Path $taskPackage 'Editor/CodeGen/Plugins'
$taskCoreRuntime = Join-Path $taskRoot 'src/UIFramework.Unity/Runtime/Plugins'
New-Item -ItemType Directory -Force $taskRuntime, $taskEditor, $taskCoreRuntime | Out-Null
foreach ($taskExtension in 'dll', 'pdb') {
    Copy-Item -LiteralPath (Join-Path $taskRoot "src/UIFramework.Core/bin/$Configuration/netstandard2.1/UIFramework.Core.$taskExtension") -Destination $taskCoreRuntime
    Copy-Item -LiteralPath (Join-Path $taskRoot "src/UIFramework.Mvvm/bin/$Configuration/netstandard2.1/UIFramework.Mvvm.$taskExtension") -Destination $taskRuntime
    Copy-Item -LiteralPath (Join-Path $taskRoot "src/UIFramework.Mvvm.Weaver/bin/$Configuration/netstandard2.1/UIFramework.Mvvm.Weaver.$taskExtension") -Destination $taskEditor
}
Write-Host "Unity packages updated: $Configuration DLLs and matching portable PDBs."
