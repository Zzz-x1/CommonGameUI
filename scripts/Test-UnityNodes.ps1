param([string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$taskWorkspace = Split-Path -Parent $PSScriptRoot
& dotnet build (Join-Path $taskWorkspace 'src/UIFramework.Core') -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Core build failed.' }
Copy-Item -LiteralPath (Join-Path $taskWorkspace 'src/UIFramework.Core/bin/Release/netstandard2.1/UIFramework.Core.dll') -Destination (Join-Path $taskWorkspace 'src/UIFramework.Unity/Runtime/Plugins/UIFramework.Core.dll')
$taskProject = Join-Path $taskWorkspace 'artifacts/unity-native-tests'
$taskTests = Join-Path $taskProject 'Assets/Tests'
$taskPackages = Join-Path $taskProject 'Packages'
$taskSettings = Join-Path $taskProject 'ProjectSettings'
New-Item -ItemType Directory -Force $taskTests, $taskPackages, $taskSettings | Out-Null
Get-ChildItem -LiteralPath (Join-Path $taskWorkspace 'tests/UIFramework.Unity.Tests') -Filter '*.cs' | Copy-Item -Destination $taskTests
@{ name = 'NativeNode.Tests'; references = @('UIFramework.Unity', 'Unity.ugui'); optionalUnityReferences = @('TestAssemblies'); autoReferenced = $false; overrideReferences = $true; precompiledReferences = @('UIFramework.Core.dll', 'nunit.framework.dll') } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskTests 'NativeNode.Tests.asmdef') -Encoding utf8
@{ dependencies = @{ 'com.ui-framework.unity' = 'file:../../../src/UIFramework.Unity'; 'com.unity.test-framework' = '1.6.0'; 'com.unity.ugui' = '2.0.0' } } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskPackages 'manifest.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $taskWorkspace 'Demo/Unity/ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $taskSettings 'ProjectVersion.txt')
$taskAuthoringLog = Join-Path $taskWorkspace 'artifacts/unity-native-authoring.log'
& unity run $taskProject --editor-path $UnityPath --timeout 180 --format json -- -nographics -executeMethod UIFramework.Tests.Unity.NativeTestAssets.Build -logFile $taskAuthoringLog
if ($LASTEXITCODE -ne 0) { throw "Native asset authoring failed; see $taskAuthoringLog" }
$taskResults = Join-Path $taskWorkspace 'artifacts/unity-native-results.xml'
& unity test $taskProject --editor-path $UnityPath --mode PlayMode --filter UIFramework.Tests.Unity.NativeNodeTests --output $taskResults --timeout 180 --format json
if ($LASTEXITCODE -ne 0) { throw "Native node tests failed; see $taskResults" }
[xml]$taskReport = Get-Content -LiteralPath $taskResults
$taskReport.'test-run' | Select-Object result, total, passed, failed
if ($taskReport.'test-run'.result -ne 'Passed' -or [int]$taskReport.'test-run'.total -ne 7) { throw "Unexpected native test result; see $taskResults" }
