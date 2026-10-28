$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$publishPath=Join-Path $projectRoot 'dist/app'
dotnet publish (Join-Path $projectRoot 'source/CodexQuotaPet.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -p:UseSharedCompilation=false -p:NuGetAudit=false "-p:PathMap=$projectRoot=/src" -o $publishPath -m:1
if($LASTEXITCODE -ne 0){throw 'Build failed'}
Write-Host "Built: $publishPath"
