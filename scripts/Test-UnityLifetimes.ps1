param([string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$taskWorkspace = Split-Path -Parent $PSScriptRoot
if (!(Test-Path -LiteralPath $UnityPath)) { throw 'Pass -UnityPath pointing to the Unity 6000.3 editor.' }
& (Join-Path $PSScriptRoot 'Run-UnityDemo.ps1') -PrepareOnly
$taskProject = Join-Path $taskWorkspace 'artifacts/unity-lifetime-tests'
$taskTests = Join-Path $taskProject 'Assets/Tests'
$taskPackages = Join-Path $taskProject 'Packages'
$taskSettings = Join-Path $taskProject 'ProjectSettings'
New-Item -ItemType Directory -Force $taskTests, $taskPackages, $taskSettings | Out-Null
Copy-Item -LiteralPath (Join-Path $taskWorkspace 'Demo/Unity/Assets/Tests/UnityLifetimeTests.cs') -Destination (Join-Path $taskTests 'UnityLifetimeTests.cs')
@{
    name = 'Lifetime.Tests'; references = @('UIFramework.Unity', 'Unity.ugui')
    optionalUnityReferences = @('TestAssemblies'); autoReferenced = $false; overrideReferences = $true
    precompiledReferences = @('UIFramework.Core.dll', 'nunit.framework.dll')
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskTests 'Lifetime.Tests.asmdef') -Encoding utf8
@{ dependencies = @{
    'com.ui-framework.unity' = 'file:../../../src/UIFramework.Unity'
    'com.unity.test-framework' = '1.6.0'; 'com.unity.ugui' = '2.0.0'; 'com.unity.modules.animation' = '1.0.0'
} } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskPackages 'manifest.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $taskWorkspace 'Demo/Unity/ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $taskSettings 'ProjectVersion.txt')
$taskRun = [Guid]::NewGuid().ToString('N')
$taskResults = Join-Path $taskWorkspace "artifacts/unity-lifetime-$taskRun.xml"
$taskLog = Join-Path $taskWorkspace "artifacts/unity-lifetime-$taskRun.log"
& $UnityPath -batchmode -nographics -projectPath $taskProject -runTests -testPlatform PlayMode -testFilter StarCourt.UnityDemo.Tests.UnityLifetimeTests -testResults $taskResults -logFile $taskLog | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Unity lifetime tests failed; see $taskLog" }
[xml]$taskReport = Get-Content -LiteralPath $taskResults
$taskReport.'test-run' | Select-Object result, total, passed, failed
if ($taskReport.'test-run'.result -ne 'Passed' -or [int]$taskReport.'test-run'.total -ne 7) { throw "Unexpected test result; see $taskResults" }
