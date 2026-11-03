# Optional: AssettoServer (https://github.com/compujuckel/AssettoServer) statt des Original-acServer.
# Nötig für Content Manager/CSP-Features (Wetter, Custom Shaders Patch, Plugins).
# Voraussetzung: install.ps1 wurde bereits ausgeführt (nutzt dessen Strecken/Autos).
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dest = Join-Path $root "assettoserver"
New-Item -ItemType Directory -Force $dest | Out-Null

$rel   = Invoke-RestMethod "https://api.github.com/repos/compujuckel/AssettoServer/releases/latest"
$asset = $rel.assets | Where-Object { $_.name -match "win-x64" -and $_.name -match "\.zip$" } | Select-Object -First 1
if (-not $asset) { throw "Kein Windows-Asset im neuesten Release gefunden." }
Invoke-WebRequest $asset.browser_download_url -OutFile "$dest\as.zip"
Expand-Archive "$dest\as.zip" $dest -Force
Remove-Item "$dest\as.zip"

# Content und Konfiguration vom Standardserver wiederverwenden
New-Item -ItemType Directory -Force "$dest\cfg" | Out-Null
Copy-Item "$root\cfg\server_cfg.ini","$root\cfg\entry_list.ini" "$dest\cfg\" -Force
foreach ($d in "content") {
    if (-not (Test-Path "$dest\$d")) { cmd /c mklink /J "$dest\$d" "$root\server\$d" | Out-Null }
}
Write-Host "Fertig. $dest\AssettoServer.exe starten (Doku: https://assettoserver.org)."
