param([string]$Project = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Demo/Godot'))
$ErrorActionPreference = 'Stop'
$taskModule = Join-Path (Split-Path -Parent $PSScriptRoot) 'src/UIFramework.Mvvm.Godot'
$taskAddon = Join-Path $Project 'addons/ui_framework_mvvm'
New-Item -ItemType Directory -Force $taskAddon | Out-Null
Get-ChildItem -LiteralPath $taskModule -File -Filter '*.cs' | Copy-Item -Destination $taskAddon
Copy-Item -LiteralPath (Join-Path $taskModule 'plugin.cfg') -Destination $taskAddon
New-Item -ItemType Directory -Force (Join-Path $taskAddon 'Editor') | Out-Null
Get-ChildItem -LiteralPath (Join-Path $taskModule 'Editor') -File -Filter '*.cs' | Copy-Item -Destination (Join-Path $taskAddon 'Editor')
