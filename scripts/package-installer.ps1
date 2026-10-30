#!/usr/bin/env pwsh
# Publica o agente desktop (self-contained, single-file, win-x64) e empacota em um .zip
# versionado, pronto para subir no Drive (um arquivo por versão).
#
# O publish roda fora deste repositório (pasta temporária) porque o projeto vive numa
# unidade sincronizada pelo Google Drive e a geração do apphost nativo usa memory-mapped
# files, que o modo stream do Drive bloqueia de forma intermitente (ver comentário em
# OneMonitor.Agent.csproj sobre UseAppHost=false) — publicar fora do Drive evita o problema
# sem precisar mudar a configuração normal de build/run do dia a dia.

$projectPath = Join-Path $PSScriptRoot "..\src\desktop-agent\OneMonitor.Agent\OneMonitor.Agent.csproj"
$version = (Select-String -Path $projectPath -Pattern '<Version>(.*)</Version>').Matches[0].Groups[1].Value

$publishDir = Join-Path $env:TEMP "OneMonitor-publish-$version"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:UseAppHost=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish falhou (código $LASTEXITCODE)."
    exit $LASTEXITCODE
}

$distDir = Join-Path $PSScriptRoot "..\dist"
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
$zipPath = Join-Path $distDir "OneMonitor-Agent-v$version-win-x64.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath

Write-Host "Pacote gerado: $zipPath"
