param([string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Run-UnityDemo.ps1') -PrepareOnly
$taskProject = Join-Path $taskRoot '.tools/MvvmWebGL'
New-Item -ItemType Directory -Force (Join-Path $taskProject 'Assets'), (Join-Path $taskProject 'Packages'), (Join-Path $taskProject 'ProjectSettings') | Out-Null
$taskPackages = @{ dependencies = @{ 'com.ui-framework.unity' = ('file:' + (Join-Path $taskRoot 'src/UIFramework.Unity').Replace('\','/')); 'com.ui-framework.mvvm.unity' = ('file:' + (Join-Path $taskRoot 'src/UIFramework.Mvvm.Unity').Replace('\','/')); 'com.unity.ugui' = '2.0.0' } }
$taskPackages | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 (Join-Path $taskProject 'Packages/manifest.json')
'm_EditorVersion: 6000.3.24f1' | Set-Content -Encoding utf8 (Join-Path $taskProject 'ProjectSettings/ProjectVersion.txt')
Copy-Item -LiteralPath (Join-Path $taskRoot 'src/UIFramework.Mvvm.Unity/Samples~/WebGL/Editor') -Destination (Join-Path $taskProject 'Assets') -Recurse -Force
Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src/UIFramework.Mvvm.Unity/Samples~/WebGL') -Filter '*.cs' | Copy-Item -Destination (Join-Path $taskProject 'Assets') -Force
New-Item -ItemType Directory -Force (Join-Path $taskProject 'Assets/Plugins') | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRoot 'src/UIFramework.Mvvm.Unity/Samples~/WebGL/MvvmReport.jslib') -Destination (Join-Path $taskProject 'Assets/Plugins') -Force
& $UnityPath -batchmode -quit -buildTarget WebGL -projectPath $taskProject -executeMethod MvvmWebSample.Editor.MvvmWebBuild.Build -logFile (Join-Path $taskProject 'build.log') | Out-Null
if ($LASTEXITCODE -ne 0) { throw "WebGL validation build failed. See $taskProject/build.log" }
