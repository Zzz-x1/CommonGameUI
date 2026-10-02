param(
    [string]$GodotPath,
    [switch]$Headless,
    [switch]$SelfTest,
    [switch]$VerifySave,
    [switch]$Capture,
    [ValidateSet('1280x720', '1600x900', '1920x1080')][string]$Resolution = '1600x900'
)
$ErrorActionPreference = 'Stop'
$taskWorkspace = Split-Path -Parent $PSScriptRoot
if (-not $GodotPath) {
    if ($env:GODOT_PATH) { $GodotPath = $env:GODOT_PATH }
    else { $GodotPath = Join-Path $taskWorkspace '.tools/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' }
}
if (-not (Test-Path -LiteralPath $GodotPath)) { throw 'Godot .NET executable not found. Pass -GodotPath or run scripts/Get-Godot.ps1.' }
if ($Headless -and $Capture) { throw 'Capture requires the graphical renderer.' }
if ((@($SelfTest, $VerifySave, $Capture) | Where-Object { $_ }).Count -gt 1) { throw 'Choose only one run mode.' }
$taskProject = Join-Path $taskWorkspace 'Demo/Godot'
& dotnet build (Join-Path $taskProject 'HierarchyDemo.csproj') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Demo build failed.' }
& $GodotPath --headless --path $taskProject --editor --import
if ($LASTEXITCODE -ne 0) { throw 'Godot import failed.' }
$taskArguments = @('--path', $taskProject, '--resolution', $Resolution)
if ($Headless) { $taskArguments += '--headless' }
if ($SelfTest) { $taskArguments += @('--', '--self-test') }
elseif ($VerifySave) { $taskArguments += @('--', '--verify-client-save') }
elseif ($Capture) { $taskArguments += @('--', '--capture-client', "--size=$Resolution") }
& $GodotPath @taskArguments
exit $LASTEXITCODE
