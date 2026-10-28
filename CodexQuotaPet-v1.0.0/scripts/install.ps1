param([switch]$PrepareOnly)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$version=(Get-Content -LiteralPath (Join-Path $projectRoot 'VERSION') -Raw).Trim()
if($version -ne '1.0.0'){throw 'Unexpected package version'}
$exe=Join-Path $projectRoot 'app/CodexQuotaPet.exe'
if(!(Test-Path -LiteralPath $exe)){$exe=Join-Path $projectRoot 'dist/app/CodexQuotaPet.exe'}
if(!(Test-Path -LiteralPath $exe)){throw 'Build first with scripts/build.ps1, or use the Windows release package.'}
$pythonCommand=Get-Command python.exe -ErrorAction SilentlyContinue
if($pythonCommand){$python=& $pythonCommand.Source -I -c 'import sys; print(sys.executable)'}
else{$pyCommand=Get-Command py.exe -ErrorAction SilentlyContinue; if(!$pyCommand){throw 'Python 3 is required'}; $python=& $pyCommand.Source -3 -I -c 'import sys; print(sys.executable)'}
if($LASTEXITCODE -ne 0 -or !$python -or !(Test-Path -LiteralPath $python.Trim())){throw 'A working Python 3 installation is required'}
$python=$python.Trim()
$codexHomePath=$env:CODEX_HOME
if(!$codexHomePath){$codexHomePath=Join-Path ([Environment]::GetFolderPath('UserProfile')) '.codex'}
$codexHomePath=[IO.Path]::GetFullPath($codexHomePath)
$cacheRoot=Join-Path $codexHomePath "plugins/cache/quota-pet-local/codex-quota-pet/$version"
$market=Join-Path $projectRoot 'build/plugin-marketplace'
$plugin=Join-Path $market 'plugins/codex-quota-pet'
New-Item -ItemType Directory -Force -Path (Join-Path $market '.agents/plugins'),(Join-Path $plugin '.codex-plugin'),(Join-Path $plugin 'app') | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'plugin/bridge.py') -Destination $plugin -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'plugin/.codex-plugin/plugin.json') -Destination (Join-Path $plugin '.codex-plugin/plugin.json') -Force
Copy-Item -LiteralPath $exe -Destination (Join-Path $plugin 'app/CodexQuotaPet.exe') -Force
$mcp=@{mcpServers=@{quota_pet=@{command=$python; args=@('-I',(Join-Path $cacheRoot 'bridge.py'));cwd=$cacheRoot;env_vars=@('CODEX_HOME');startup_timeout_sec=15}}}
$marketplace=@{name='quota-pet-local';interface=@{displayName='CodexQuotaPet local'};plugins=@(@{name='codex-quota-pet';source=@{source='local';path='./plugins/codex-quota-pet'};policy=@{installation='AVAILABLE';authentication='ON_INSTALL'};category='Productivity'})}
$encoding=New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $plugin '.mcp.json'),($mcp|ConvertTo-Json -Depth 10),$encoding)
[IO.File]::WriteAllText((Join-Path $market '.agents/plugins/marketplace.json'),($marketplace|ConvertTo-Json -Depth 10),$encoding)
if($PrepareOnly){Write-Host "Prepared local package: $market";return}
if(!(Get-Command codex -ErrorAction SilentlyContinue)){throw 'Codex CLI must be available on PATH'}
codex plugin marketplace add $market --json
if($LASTEXITCODE -ne 0){throw 'Marketplace registration failed'}
codex plugin add codex-quota-pet@quota-pet-local --json
if($LASTEXITCODE -ne 0){throw 'Plugin installation failed'}
Write-Host 'Installed v1.0.0. Restart Codex once, then load a local chat. Keep this package folder for future updates.'
