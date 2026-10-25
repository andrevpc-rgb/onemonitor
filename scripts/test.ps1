#!/usr/bin/env pwsh
$solutionDir = Join-Path $PSScriptRoot "..\src\desktop-agent"
dotnet test (Join-Path $solutionDir "OneMonitor.Agent.sln")
