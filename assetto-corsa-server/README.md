# Assetto Corsa Dedicated Server (Windows)

Enthält alles, um auf einem Windows-PC einen Server zu starten.

## Schnellstart
1. Ordner auf den Server-PC kopieren (Git-Clone oder ZIP).
2. `cfg\server_cfg.ini` anpassen: `NAME`, `PASSWORD`, `ADMIN_PASSWORD`, `TRACK`, `CARS`, `MAX_CLIENTS`.
3. PowerShell: `PowerShell -ExecutionPolicy Bypass -File .\install.ps1`
   (lädt SteamCMD und den Server, App-ID 302550, anonym).
4. PowerShell **als Administrator**: `.\firewall.ps1` (öffnet UDP/TCP 9600 und TCP 8081).
5. `start.bat` ausführen.

## Strecke und Autos
- `TRACK` ist der Ordnername unter `server\content\tracks`, `CONFIG_TRACK` das Layout (leer = Standard).
- `CARS` listet die Ordnernamen unter `server\content\cars`, getrennt durch `;`.
- Pro Slot in `entry_list.ini` gibt es einen `[CAR_n]`-Block. Die Zahl der Blöcke muss mindestens `MAX_CLIENTS` entsprechen,
  und `MODEL` muss in `CARS` vorkommen.
- Der Dedicated Server bringt nur wenige Inhalte mit. Eigene Strecken und Autos kommen aus deiner AC-Installation
  oder aus Mods und werden in `server\content\...` kopiert (DLC-Strecken wie die Nordschleife ebenfalls).
  Ohne diese Dateien startet der Server nicht.

## Content Manager, CSP, erweiterte Funktionen
`install-assettoserver.ps1` lädt [AssettoServer](https://assettoserver.org) herunter (Content-Manager-Wetter, CSP, Plugins).
Dessen eigene Config (`extra_cfg.yml`) legst du anschließend nach der AssettoServer-Doku an.

## Erreichbarkeit
- LAN: `IP-des-PCs:9600` (im Content Manager unter „LAN“ sichtbar).
- Internet: Router-Portfreigabe für **UDP 9600**, **TCP 9600** und **TCP 8081** auf den PC.
  Dann direkt per `öffentliche-IP:9600` verbinden. `REGISTER_TO_LOBBY=1` listet den Server öffentlich.
- Die Admin-/Rennsteuerung geht im Spiel per Chat: `/admin <ADMIN_PASSWORD>`.

## Hinweise
- Passwörter in `server_cfg.ini` vor dem Einchecken oder Teilen ändern.
- Dieses Setup wurde nicht gegen einen echten Server getestet, weil die Umgebung dafür keinen Windows-PC hat.
