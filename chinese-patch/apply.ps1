# Applies or reverts the Chinese localization patch on an installed HellForge (0.4.0-beta or newer).
# The patch touches only the launcher's own files; the game copy, profiles and mods are never read.
#   powershell -ExecutionPolicy Bypass -File apply.ps1                  # apply (the first run backs up the originals)
#   powershell -ExecutionPolicy Bypass -File apply.ps1 -Revert          # put the backed-up originals back
#   powershell -ExecutionPolicy Bypass -File apply.ps1 -Root D:\Hell    # an installation folder other than the default
param([string]$Root = 'D:\Games\Helldivers.2-InsaneRamZes', [switch]$Revert)

$ErrorActionPreference = 'Stop'
$patch = Split-Path -Parent $MyInvocation.MyCommand.Path
$launcher = Join-Path $Root 'app\launcher'
$backup = Join-Path $Root 'backup-pre-chinese'
$files = @(
    'HellForge.Launcher.dll', 'HellForge.Core.dll', 'HellForge.SteamDownload.dll',
    'HellForge.Launcher.exe', 'HellForge.Launcher.deps.json', 'HellForge.Launcher.runtimeconfig.json'
)

if (-not (Test-Path (Join-Path $launcher 'HellForge.Launcher.dll'))) { throw "No HellForge installation found in $Root (looked for app\launcher)." }

if ($Revert) {
    if (-not (Test-Path $backup)) { throw "No backup found in $backup: nothing to revert (the patch was never applied)." }
    foreach ($name in $files) { Copy-Item (Join-Path $backup $name) $launcher -Force }
    if (Test-Path (Join-Path $backup 'zh-CN')) {
        $target = Join-Path $launcher 'zh-CN'
        if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    } else {
        Remove-Item (Join-Path $launcher 'zh-CN') -Recurse -Force -ErrorAction SilentlyContinue
    }
    Remove-Item (Join-Path $Root 'language.json') -Force -ErrorAction SilentlyContinue
    Write-Host "Reverted: the launcher's originals are back from $backup."
    exit 0
}

$installed = (Get-Item (Join-Path $launcher 'HellForge.Core.dll')).VersionInfo.ProductVersion
$patched = (Get-Item (Join-Path $patch 'HellForge.Core.dll')).VersionInfo.ProductVersion
if ($installed -notlike "$patched*") { throw "The installation's launcher is $installed but this patch is built from $patched: update the installation first (or rebuild the patch from its tag)." }

if (-not (Test-Path $backup)) {
    New-Item -ItemType Directory -Force $backup | Out-Null
    foreach ($name in $files) { Copy-Item (Join-Path $launcher $name) $backup -Force }
    if (Test-Path (Join-Path $launcher 'zh-CN')) { Copy-Item (Join-Path $launcher 'zh-CN') (Join-Path $backup 'zh-CN') -Recurse -Force }
    Write-Host "Backed up the launcher's originals to $backup (apply.ps1 -Revert puts them back)."
} else {
    Write-Host "Backup already present in $backup: keeping the earliest originals."
}
foreach ($name in $files) { Copy-Item (Join-Path $patch $name) $launcher -Force }
New-Item -ItemType Directory -Force (Join-Path $launcher 'zh-CN') | Out-Null
Copy-Item (Join-Path $patch 'zh-CN\*') (Join-Path $launcher 'zh-CN') -Recurse -Force
Set-Content -Path (Join-Path $Root 'language.json') -Value '{"language":"zh-CN"}' -NoNewline
Write-Host "Chinese applied. Start HellForge and pick the language in Settings (LANGUAGE card) if it is not on already."
