param(
    [ValidateRange(1024, 65535)][int]$Port = 4173,
    [switch]$NoBrowser
)
$ErrorActionPreference = 'Stop'
$taskWorkspace = Split-Path -Parent $PSScriptRoot
$taskDocs = Join-Path $taskWorkspace 'docs/site'
$taskUrl = "http://localhost:$Port/"
function Test-DocsPort {
    return [bool]([System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() | Where-Object Port -eq $Port)
}
if (!(Test-DocsPort)) {
    $taskNpm = (Get-Command npm.cmd -ErrorAction Stop).Source
    if (!(Test-Path -LiteralPath (Join-Path $taskDocs 'node_modules/vitepress/bin/vitepress.js'))) {
        Push-Location $taskDocs
        try { & $taskNpm ci; if ($LASTEXITCODE -ne 0) { throw 'Documentation dependencies failed to install.' } }
        finally { Pop-Location }
    }
    $taskLogs = Join-Path $taskWorkspace 'artifacts'
    New-Item -ItemType Directory -Force $taskLogs | Out-Null
    $taskNode = (Get-Command node -ErrorAction Stop).Source
    $taskArguments = @("node_modules/vitepress/bin/vitepress.js", "dev", ".", "--host", "127.0.0.1", "--port", $Port, "--strictPort")
    $taskServer = Start-Process -FilePath $taskNode -ArgumentList $taskArguments -WorkingDirectory $taskDocs -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskLogs "docs-$Port.log") -RedirectStandardError (Join-Path $taskLogs "docs-$Port.error.log")
    $taskDeadline = [DateTime]::UtcNow.AddSeconds(30)
    while (!(Test-DocsPort)) {
        if ($taskServer.HasExited) { throw "Documentation server exited. See artifacts/docs-$Port.error.log." }
        if ([DateTime]::UtcNow -ge $taskDeadline) { throw "Documentation server startup timed out. See artifacts/docs-$Port.log." }
        Start-Sleep -Milliseconds 200
    }
}
$taskPage = Invoke-WebRequest -Uri $taskUrl -UseBasicParsing
if ($taskPage.Content -notmatch 'UIFramework' -and !$taskPage.Content.Contains($taskDocs)) { throw "Port $Port is occupied by another site." }
Write-Host $taskUrl
if (!$NoBrowser) { Start-Process $taskUrl }
