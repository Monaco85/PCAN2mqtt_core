# Installiert SteamCMD und den Assetto Corsa Dedicated Server (Steam App 302550, anonym).
# Aufruf: PowerShell -ExecutionPolicy Bypass -File .\install.ps1
$ErrorActionPreference = "Stop"
$root   = $PSScriptRoot
$steam  = Join-Path $root "steamcmd"
$server = Join-Path $root "server"

if (-not (Test-Path "$steam\steamcmd.exe")) {
    New-Item -ItemType Directory -Force $steam | Out-Null
    Invoke-WebRequest "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip" -OutFile "$steam\steamcmd.zip"
    Expand-Archive "$steam\steamcmd.zip" $steam -Force
}

& "$steam\steamcmd.exe" +force_install_dir $server +login anonymous +app_update 302550 validate +quit

# Eigene Konfiguration einspielen
Copy-Item "$root\cfg\*" "$server\cfg\" -Force
Write-Host "Fertig. Server mit start.bat starten."
