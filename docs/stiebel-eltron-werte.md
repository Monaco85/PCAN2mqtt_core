# Stiebel Eltron: Werte lesen und schreiben

Diese Seite fasst zusammen, was an einer Stiebel-Eltron-Wärmepumpe mit FEK im CAN-Trace geprüft wurde. Alle Angaben stammen aus Mitschnitten mit PCAN-View an der laufenden Anlage. Was nicht geprüft ist, steht ausdrücklich dabei.

Die Datenpunkte selbst stehen in `can2mqtt_core/translator/stiebel_eltron/StiebelEltron.json`.

## Schreiben, geprüft

Die Topics beziehen sich auf den `MqttTopic` aus der `config.json`, im Beispiel `heating`. In der Config muss `MqttAcceptSet` auf `true` stehen.

| Was | MQTT-Topic (`/set`) | Beispiel | CAN-Frame | Ziel |
|---|---|---|---|---|
| Warmwasser Komfort | `<MqttTopic>/dwh/temperature/day/set` | `49` | `30 00 FA 00 13 01 EA` | Wärmepumpe 180, Index `0013` |
| Warmwasser ECO | `<MqttTopic>/dwh/temperature/eco/set` | `41` | `30 00 FA 0A 06 01 9A` | Wärmepumpe 180, Index `0A06` |
| Raum-Soll Tag | `<MqttTopic>/room/hc1/temperature/day/set` | `22` | `60 01 FA 00 05 00 DC` | FEK 301, Index `0005` |

Die Anlage sendet **keine Schreibbestätigung**. Ob ein Wert angekommen ist, siehst du nur durch Zurücklesen (`.../read`) oder am Gerät.

Werte am besten als ganze Zahl oder mit Punkt senden. `ConvertValueBack` rechnet mit `Convert.ToDouble`, bei deutscher Ländereinstellung kann ein Wert wie `47.5` falsch gelesen werden.

### Was nicht funktioniert

`/dwh/temperature/desired` (Index `0003`) ist der aktuell wirksame Warmwasser-Sollwert. Er wird von der Anlage laufend gesetzt. Ein Schreiben darauf blieb über ein Jahr wirkungslos. Zum Ändern den Komfortwert (`0013`) schreiben. Im JSON ist `0003` deshalb als `ReadOnly` markiert. Das Flag wird im Code allerdings nicht ausgewertet und verhindert kein Senden.

### Nicht getestet

Die ECO-Raumtemperatur (`/room/hc1/temperature/night`, Index `0008`) wurde nicht geschrieben. Der FEK meldet den wirksamen Raumsollwert als `0012` an `601`. Vermutlich sendet er den ECO-Wert erst im ECO-Modus.

## Relaisstatus, Index `0A20`

Topic `/status/relaystatus`, nur lesen. Die Anlage sendet den Wert selbst als Broadcast, man kann ihn auch abfragen. Der Wert kommt als Dezimalzahl.

| Bit | Bedeutung |
|---|---|
| `0x0C` (8 + 4) | Verdichter läuft |
| `0x20` | Heizbetrieb |
| `0x10` | Warmwasserbetrieb |
| `0x100` | Wärmeanforderung |
| `0x40` | immer gesetzt |

Beobachtete Werte:

| Wert | Situation |
|---|---|
| `0060` | Ruhe |
| `0160` | Wärmeanforderung, Verdichter aus |
| `016C` | Verdichter läuft, Heizen |
| `015C` | Verdichter läuft, Warmwasser |

Das ist aus fünf Beobachtungen abgeleitet. Ob die Bits `0x08` und `0x04` einzeln etwas bedeuten, ist offen.

Beispiel für Home Assistant (nicht getestet):

```yaml
mqtt:
  binary_sensor:
    - name: "Verdichter läuft"
      state_topic: "heating/status/relaystatus"
      value_template: "{{ 'ON' if (value | int) | bitwise_and(12) == 12 else 'OFF' }}"
```

## Teilnehmer am Bus

| CAN-ID | Rolle |
|---|---|
| `180` | Wärmepumpe, antwortet auf Anfragen und sendet Messwerte an die Sammeladresse `379` |
| `301` | FEK, liefert Raumtemperatur, Feuchte und Raumsollwert |
| `100` | fragt `180` zyklisch ab (vermutlich ISG oder ein Gateway) |
| `480` / `700` | fragen sich gegenseitig Register ab, für die Werte hier unwichtig |
| `680` | **Du:** dein PC mit PCAN-Adapter und can2mqtt. Auch Testframes aus PCAN-View gehen mit dieser ID raus. `CanSenderId` in der Beispielkonfiguration ist `69F`. |

## Aufbau eines Frames

```
60 01 FA 00 05 00 DC
|  |  |  |     |
|  |  |  |     +-- Wert, 16 Bit (hier 0x00DC = 220 = 22,0 °C)
|  |  |  +-------- Index (hier 0005)
|  |  +----------- FA = erweiterter Index
|  +-------------- Rest der Empfängeradresse
+----------------- oben Empfänger-Bereich, unten Typ
```

Typ im unteren Halbbyte: `0` schreiben, `1` lesen, `2` Antwort, `6` und `7` System. Der Empfänger ergibt sich aus Bereich und Byte 1: Bereich `3` plus `00` ist `180`, Bereich `6` plus `01` ist `301`. Temperaturen stehen als Zehntelgrad (`220` für 22,0 °C).

## Mitschneiden und selbst herausfinden

Eine neue Adresse findest du mit PCAN-View:

1. Trace starten.
2. Genau **eine** Einstellung am FEK oder im ISG ändern und die Uhrzeit notieren. Der neue Wert sollte sich deutlich vom alten unterscheiden.
3. 10 Sekunden warten, damit das Zurücklesen im Trace landet, und speichern.
4. Im Trace das Schreib-Frame (Typ `0`) suchen. Es zeigt Empfänger, Index und Wert.
5. Das gleiche Frame mit einem anderen Wert aus PCAN-View nachsenden und zurücklesen.

Hinweise:

- Die Sendeliste von PCAN-View (`.xmt`) enthält keinen Mitschnitt. Ein Mitschnitt ist eine `.trc`-Datei.
- Wird ein Frame zyklisch gesendet, schreibt PCAN-View jedes Mal erneut. Zyklus nach dem Test abschalten.
- Die Absender-ID in PCAN-View und die `CanSenderId` von can2mqtt sollten verschieden sein, wenn can2mqtt gleichzeitig läuft.
- can2mqtt liest die JSON beim Start aus `./translator/stiebel_eltron/` im Ausgabeordner. Ein Build kopiert die Datei nicht unbedingt dorthin. Nach Änderungen die Datei prüfen und can2mqtt neu starten.
