# Als Administrator ausführen. Öffnet die Assetto-Corsa-Ports in der Windows-Firewall.
New-NetFirewallRule -DisplayName "AC Server UDP 9600" -Direction Inbound -Protocol UDP -LocalPort 9600 -Action Allow
New-NetFirewallRule -DisplayName "AC Server TCP 9600" -Direction Inbound -Protocol TCP -LocalPort 9600 -Action Allow
New-NetFirewallRule -DisplayName "AC Server HTTP 8081" -Direction Inbound -Protocol TCP -LocalPort 8081 -Action Allow
