param(
    [Parameter(Mandatory = $true)]
    [string]$Godot,
    [ValidateSet('MenuTests', 'SidePanelTests')]
    [string]$TestScene = 'MenuTests'
)

$ErrorActionPreference = 'Stop'
$workspacePath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$gamePath = Join-Path $workspacePath 'chess-game'

dotnet build (Join-Path $gamePath 'Chess.Game.csproj') --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Reuse the production assembly and scenes with a separate Godot project name.
# All generated resources stay under .godot and are ignored by Git.
$sandboxPath = Join-Path $PSScriptRoot '.godot/menu-sandbox'
[System.IO.Directory]::CreateDirectory($sandboxPath) | Out-Null
foreach ($directory in @('adapters', 'scenes', 'tests', 'assets')) {
    Copy-Item -LiteralPath (Join-Path $gamePath $directory) -Destination $sandboxPath -Recurse -Force
}

$binPath = Join-Path $gamePath '.godot/mono/temp/bin/Debug'
$sandboxBin = Join-Path $sandboxPath '.godot/mono/temp/bin/Debug'
[System.IO.Directory]::CreateDirectory($sandboxBin) | Out-Null
Copy-Item -Path (Join-Path $binPath '*') -Destination $sandboxBin -Recurse -Force

$config = [System.IO.File]::ReadAllText((Join-Path $gamePath 'project.godot'))
$projectName = if ($TestScene -eq 'MenuTests') { 'Cheesse Menu Tests' } else { 'Cheesse SidePanel Tests' }
$config = $config.Replace('config/name="Cheesse"', ('config/name="' + $projectName + '"'))
$config = $config.Replace('run/main_scene="res://scenes/Main.tscn"', ('run/main_scene="res://tests/' + $TestScene + '.tscn"'))
$config = $config.Replace('config/icon="res://icon.svg"', '')
[System.IO.File]::WriteAllText((Join-Path $sandboxPath 'project.godot'), $config)

# Import the SVG atlas using Godot's real resource pipeline.
& $Godot --headless --editor --path $sandboxPath --import --quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$testOutput = & $Godot --headless --path $sandboxPath --quit-after 600
$testExitCode = $LASTEXITCODE
$testOutput | Write-Output
if ($testExitCode -ne 0) { exit $testExitCode }
if (-not ($testOutput -match '^PASS: (menu|sidepanel)')) { throw 'UI tests did not complete.' }
exit 0
