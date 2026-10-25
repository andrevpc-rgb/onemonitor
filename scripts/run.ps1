#!/usr/bin/env pwsh
$projectDir = Join-Path $PSScriptRoot "..\src\desktop-agent\OneMonitor.Agent"
dotnet run --project $projectDir
