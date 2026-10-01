param(
    [string]$GodotPath,
    [switch]$Headless,
    [switch]$SelfTest,
    [switch]$Capture,
    [ValidateSet('Inventory', 'Preferences', 'Settings')][string]$CaptureScene = 'Inventory',
    [ValidateSet('tab', 'duplicate', 'load', 'lifecycle', 'transition', 'entered', 'state', 'cleanup')][string]$Failure
)
$ErrorActionPreference = 'Stop'
$taskWorkspace = Split-Path -Parent $PSScriptRoot
if (-not $GodotPath) {
    if ($env:GODOT_PATH) { $GodotPath = $env:GODOT_PATH }
    else { $GodotPath = Join-Path $taskWorkspace '.tools/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' }
}
if (-not (Test-Path -LiteralPath $GodotPath)) { throw 'Godot .NET executable not found. Pass -GodotPath or run scripts/Get-Godot.ps1.' }
if ($Headless -and $Capture) { throw 'Capture requires the graphical renderer.' }
if (([int]$SelfTest.IsPresent + [int]$Capture.IsPresent + [int][bool]$Failure) -gt 1) { throw 'Choose only one run mode.' }
$taskProject = Join-Path $taskWorkspace 'Demo/Godot'
& dotnet build (Join-Path $taskProject 'HierarchyDemo.csproj') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Demo build failed.' }
& $GodotPath --headless --path $taskProject --editor --import
if ($LASTEXITCODE -ne 0) { throw 'Godot import failed.' }
$taskArguments = @('--path', $taskProject)
if ($Headless) { $taskArguments += '--headless' }
if ($SelfTest) { $taskArguments += @('--', '--self-test') }
elseif ($Capture) { $taskArguments += @('--', $(if ($CaptureScene -eq 'Settings') { '--capture-settings' } elseif ($CaptureScene -eq 'Preferences') { '--capture-preferences' } else { '--capture' })) }
elseif ($Failure) { $taskArguments += @('--', "--failure-$Failure") }
& $GodotPath @taskArguments
exit $LASTEXITCODE
