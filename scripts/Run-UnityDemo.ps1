param(
    [switch]$PrepareOnly,
    [switch]$Play,
    [switch]$SelfTest,
    [switch]$RebuildAssets,
    [ValidateSet('1280x720', '1600x900', '1920x1080')][string]$Resolution = '1600x900'
)
$ErrorActionPreference = 'Stop'
$taskWorkspace = Split-Path -Parent $PSScriptRoot
$taskUnityProject = Join-Path $taskWorkspace 'Demo/Unity'
& dotnet build (Join-Path $taskWorkspace 'src/UIFramework.Core/UIFramework.Core.csproj') -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Core build failed.' }
$taskPluginDirectory = Join-Path $taskWorkspace 'src/UIFramework.Unity/Runtime/Plugins'
New-Item -ItemType Directory -Force $taskPluginDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $taskWorkspace 'src/UIFramework.Core/bin/Release/netstandard2.1/UIFramework.Core.dll') -Destination (Join-Path $taskPluginDirectory 'UIFramework.Core.dll')
if ($PrepareOnly) { return }

function Invoke-UnityCommand([string]$Name, [string[]]$CommandArguments = @()) {
    $taskResponse = & unity command $Name @CommandArguments --project-path $taskUnityProject --json
    if ($LASTEXITCODE -ne 0) { throw ($taskResponse -join "`n") }
    $taskResult = ($taskResponse -join "`n") | ConvertFrom-Json
    if (!$taskResult.success -or !$taskResult.data.success) { throw ($taskResponse -join "`n") }
    return $taskResult.data.result
}

& unity pipeline install --project-path $taskUnityProject --json
if ($LASTEXITCODE -ne 0) { throw 'Unity Pipeline installation failed.' }
$taskStatus = ((& unity status --json) -join "`n") | ConvertFrom-Json
$taskInstance = $taskStatus.data.instances | Where-Object { [IO.Path]::GetFullPath($_.project) -eq [IO.Path]::GetFullPath($taskUnityProject) }
if (!$taskInstance) {
    & unity open $taskUnityProject --editor-version 6000.3.24f1
    if ($LASTEXITCODE -ne 0) { throw 'Unity Editor failed to open.' }
}
$taskDeadline = [DateTime]::UtcNow.AddMinutes(3)
do {
    $taskStatus = ((& unity status --json) -join "`n") | ConvertFrom-Json
    $taskInstance = $taskStatus.data.instances | Where-Object { [IO.Path]::GetFullPath($_.project) -eq [IO.Path]::GetFullPath($taskUnityProject) }
    if (!$taskInstance) { Start-Sleep -Milliseconds 500 }
    if ([DateTime]::UtcNow -gt $taskDeadline) { throw 'Unity Pipeline did not connect to this project.' }
} until ($taskInstance)

$taskDeadline = [DateTime]::UtcNow.AddMinutes(3)
Invoke-UnityCommand recompile @('--focus', 'false') | Out-Null
do {
    Start-Sleep -Milliseconds 500
    $taskCompilation = Invoke-UnityCommand recompile_status
    if ([DateTime]::UtcNow -gt $taskDeadline) { throw 'Unity compilation did not complete.' }
} until ($taskCompilation.status -in @('completed', 'up_to_date'))
if ($taskCompilation.failed -or $taskCompilation.compilationFailed) { throw ($taskCompilation.errors -join "`n") }
if ($RebuildAssets) {
    $taskAuthored = Invoke-UnityCommand eval @('--code', 'StarCourt.UnityDemo.Editor.ClientSceneAuthoring.Build(); return true;')
    if (!$taskAuthored.success) { throw 'Unity scene authoring failed.' }
}
$taskSize = $Resolution.Split('x')
$taskResolution = Invoke-UnityCommand eval @('--code', "StarCourt.UnityDemo.Editor.ClientSceneAuthoring.SetResolution($($taskSize[0]),$($taskSize[1])); return true;")
if (!$taskResolution.success) { throw 'Game View resolution setup failed.' }

if ($SelfTest) {
    $taskStarted = Invoke-UnityCommand run_tests @('--mode', 'playmode', '--filter', 'StarCourt.UnityDemo.Tests', '--async_tests', 'true')
    if (!$taskStarted.success) { throw 'Unity Play Mode tests failed to start.' }
    $taskDeadline = [DateTime]::UtcNow.AddMinutes(3)
    do {
        Start-Sleep -Milliseconds 500
        $taskTests = Invoke-UnityCommand test_status
        if ([DateTime]::UtcNow -gt $taskDeadline) { throw 'Unity tests are still running; inspect the live test_status before retrying.' }
    } until ($taskTests.status -eq 'completed')
    $taskTests | ConvertTo-Json -Depth 12
    if ($taskTests.summary.total -eq 0 -or $taskTests.summary.failed -ne 0 -or $taskTests.summary.passed -ne $taskTests.summary.total) { throw 'Unity Play Mode tests did not all pass.' }
}
elseif ($Play) { Invoke-UnityCommand editor_play | Out-Null }
