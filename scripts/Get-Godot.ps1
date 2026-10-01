$ErrorActionPreference = 'Stop'
$taskWorkspace = Split-Path -Parent $PSScriptRoot
$taskToolRoot = Join-Path $taskWorkspace '.tools'
New-Item -ItemType Directory -Force -Path $taskToolRoot | Out-Null
$taskArchive = Join-Path $taskToolRoot 'godot.zip'
Invoke-WebRequest -Uri 'https://github.com/godotengine/godot-builds/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_win64.zip' -OutFile $taskArchive
Expand-Archive -LiteralPath $taskArchive -DestinationPath (Join-Path $taskToolRoot 'Godot') -Force
Write-Output 'Godot 4.7.2 .NET is ready in .tools/Godot. No system installation was modified.'
