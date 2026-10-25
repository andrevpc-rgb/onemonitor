#!/usr/bin/env pwsh
$solutionDir = Join-Path $PSScriptRoot "..\src\desktop-agent"
dotnet build (Join-Path $solutionDir "OneMonitor.Agent.sln")
