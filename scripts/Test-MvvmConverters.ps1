param([string]$UnityPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$taskWorkspace = Split-Path -Parent $PSScriptRoot
& dotnet test (Join-Path $taskWorkspace 'tests/UIFramework.Mvvm.Tests') --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'MVVM tests failed.' }
& (Join-Path $PSScriptRoot 'Prepare-Mvvm.ps1')
Copy-Item -LiteralPath (Join-Path $taskWorkspace 'src/UIFramework.Core/bin/Release/netstandard2.1/UIFramework.Core.dll') -Destination (Join-Path $taskWorkspace 'src/UIFramework.Unity/Runtime/Plugins/UIFramework.Core.dll')
$taskUnityProject = Join-Path $taskWorkspace 'artifacts/unity-converter-tests'
$taskChecks = Join-Path $taskUnityProject 'Assets/ConverterTests'
New-Item -ItemType Directory -Force $taskChecks, (Join-Path $taskUnityProject 'Packages'), (Join-Path $taskUnityProject 'ProjectSettings') | Out-Null
Get-ChildItem -LiteralPath (Join-Path $taskWorkspace 'tests/UIFramework.Mvvm.Unity.Tests') -Filter '*.cs' | Copy-Item -Destination $taskChecks
@{ name = 'NativeMvvm.EditorChecks'; references = @('UIFramework.Unity','UIFramework.Mvvm.Unity','UIFramework.Mvvm.Unity.Editor','Unity.ugui'); includePlatforms = @('Editor') } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskChecks 'Checks.asmdef') -Encoding utf8
@{ dependencies = @{ 'com.ui-framework.unity' = 'file:../../../src/UIFramework.Unity'; 'com.ui-framework.mvvm.unity' = 'file:../../../src/UIFramework.Mvvm.Unity'; 'com.unity.ugui' = '2.0.0' } } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskUnityProject 'Packages/manifest.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $taskWorkspace 'Demo/Unity/ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $taskUnityProject 'ProjectSettings/ProjectVersion.txt')
$taskUnityResult = Join-Path $taskUnityProject 'converter-checks.txt'
if (Test-Path -LiteralPath $taskUnityResult) { Remove-Item -LiteralPath $taskUnityResult }
& unity run $taskUnityProject --editor-path $UnityPath --timeout 180 --format json -- -nographics -executeMethod NativeMvvm.Tests.ConverterEditorChecks.Run -logFile (Join-Path $taskWorkspace 'artifacts/unity-converter-editor.log')
if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $taskUnityResult)) { throw 'Unity converter editor checks failed.' }
Get-Content -LiteralPath $taskUnityResult
$taskGodotProject = Join-Path $taskWorkspace 'artifacts/godot-converter-tests'
New-Item -ItemType Directory -Force $taskGodotProject | Out-Null
Get-ChildItem -LiteralPath (Join-Path $taskWorkspace 'tests/UIFramework.Mvvm.Godot.Tests') -Filter '*.cs' | Copy-Item -Destination $taskGodotProject
@'
<Project Sdk="Godot.NET.Sdk/4.7.2"><PropertyGroup><TargetFramework>net10.0</TargetFramework><EnableDynamicLoading>true</EnableDynamicLoading></PropertyGroup><ItemGroup><ProjectReference Include="../../src/UIFramework.Mvvm/UIFramework.Mvvm.csproj"/><ProjectReference Include="../../src/UIFramework.Godot/UIFramework.Godot.csproj"/></ItemGroup><Import Project="../../build/UIFramework.Mvvm.targets"/></Project>
'@ | Set-Content -LiteralPath (Join-Path $taskGodotProject 'ConverterChecks.csproj') -Encoding utf8
@'
config_version=5
[application]
config/name="Converter Checks"
run/main_scene="res://Main.tscn"
[dotnet]
project/assembly_name="ConverterChecks"
[rendering]
renderer/rendering_method="gl_compatibility"
'@ | Set-Content -LiteralPath (Join-Path $taskGodotProject 'project.godot') -Encoding utf8
@'
[gd_scene load_steps=2 format=3]
[ext_resource type="Script" path="res://ConverterChecks.cs" id="1"]
[node name="Main" type="Node"]
script = ExtResource("1")
'@ | Set-Content -LiteralPath (Join-Path $taskGodotProject 'Main.tscn') -Encoding utf8
& (Join-Path $PSScriptRoot 'Prepare-GodotMvvm.ps1') -Project $taskGodotProject
& dotnet build $taskGodotProject --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Godot converter test build failed.' }
$taskGodot = Join-Path $taskWorkspace '.tools/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
& $taskGodot --headless --path $taskGodotProject --editor --import
if ($LASTEXITCODE -ne 0) { throw 'Godot test import failed.' }
$taskGodotResult = Join-Path $taskGodotProject 'converter-checks.txt'
if (Test-Path -LiteralPath $taskGodotResult) { Remove-Item -LiteralPath $taskGodotResult }
& $taskGodot --headless --path $taskGodotProject --quit-after 300
if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $taskGodotResult)) { throw 'Godot converter runtime checks failed.' }
Get-Content -LiteralPath $taskGodotResult
$taskPlugin = Join-Path $taskGodotProject 'addons/converter_checks'
New-Item -ItemType Directory -Force $taskPlugin | Out-Null
@'
[plugin]
name="Converter Checks"
description="Isolated editor tests"
author="UIFramework"
version="1"
script="../../ConverterEditorChecks.cs"
'@ | Set-Content -LiteralPath (Join-Path $taskPlugin 'plugin.cfg') -Encoding utf8
@'
[editor_plugins]
enabled=PackedStringArray("res://addons/converter_checks/plugin.cfg")
'@ | Add-Content -LiteralPath (Join-Path $taskGodotProject 'project.godot') -Encoding utf8
$taskEditorResult = Join-Path $taskGodotProject 'converter-editor-checks.txt'
if (Test-Path -LiteralPath $taskEditorResult) { Remove-Item -LiteralPath $taskEditorResult }
& $taskGodot --headless --path $taskGodotProject --editor --quit-after 300
if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $taskEditorResult)) { throw 'Godot converter GUI checks failed.' }
Get-Content -LiteralPath $taskEditorResult
