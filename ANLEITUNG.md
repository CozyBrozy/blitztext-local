# Auftrag fuer Codex: aktuelle lokale Blitztext-Version einrichten

**Stand:** 10.11.2026 · **Herkunft:** Benutzeranforderung und lokal gepruefter Installationsstand.

Installiere die App selbst. Arbeite mit Dateien und Terminal im Hintergrund. Keine Bildschirmsteuerung, Screenshots oder heimlichen Mikrofon-/Loopback-Tests. Keine API-Keys, Audio-Uploads oder Cloud-Verarbeitung.

## 1. Bestand und Hardware pruefen

- Windows 11 x64, AVX2 und PowerShell 7 pruefen. Bei Bedarf PowerShell aus dem offiziellen Microsoft-Paket installieren; keine ExecutionPolicy oder Windows-Schutzfunktion umgehen.
- Bestehende App und Datenordner erkennen. Eine laufende Aufnahme oder Verarbeitung nicht beenden. Vor einem Update den Benutzer fertig werden und die App ueber das Tray beenden lassen.
- Bei einer festgefahrenen Verarbeitung zuerst beide vollstaendigen Audios und das bisherige Markdown ausserhalb des Repos sichern und die Audio-Pruefsummen vergleichen. Erst danach einen Ersatzlauf vorbereiten.
- `nvidia-smi` pruefen. Bei passender NVIDIA-GPU den Qualitaetsweg mit [GPU.md](GPU.md) ausfuehren. Die CUDA-Versionsanzeige des Treibers allein genuegt nicht. Fuer App, Downloads und lokale Modellreserve etwa 12 GB freien Speicher einplanen.

## 2. Aktuellen Quellcode beziehen

```powershell
git clone https://github.com/CozyBrozy/blitztext-local.git
Set-Location blitztext-local
```

Bei vorhandenem Klon zuerst lokale Aenderungen pruefen. Kein Reset und keine persoenlichen Daten ueberschreiben. Das Repo ist oeffentlich; keine fremden GitHub-Zugangsdaten anfordern.

**Release v0.1.1 ist die aeltere CPU-Version.** CUDA und Wiederherstellung liegen im aktuellen Quellcode. Fuer den neuen Stand aus dem Quellcode installieren.

## 3. App installieren

```powershell
pwsh -NoProfile -File .\setup.ps1 -FromSource -InstallPrerequisites -NoStart
if ($LASTEXITCODE -ne 0) { throw 'Einrichtung fehlgeschlagen.' }
```

Setup prueft Microsoft-Signaturen und die offiziellen SDK- und Modell-Pruefsummen. Es installiert bei Bedarf das .NET-8-SDK, testet und baut die App, richtet den Startmenue-Eintrag ein und installiert den kleinen Silero-Sprachfilter. Small und Base dienen als CPU-Grundmodelle. Eine verlangte Adminfreigabe fuer Visual C++ erledigt der Benutzer selbst.

## 4. Hohe Qualitaet mit NVIDIA

Bei geeigneter NVIDIA-GPU **[GPU.md](GPU.md) vollstaendig ausfuehren**. Das richtige Transkriptionsmodell fuer Diktat und Gespraeche ist **Full Whisper large-v3**, die Datei `ggml-large-v3.bin` mit rund 3,1 GB. Turbo, quantisierte Varianten und Silero sind andere Modelle. Silero markiert Sprache und Pausen fuer die Live-Verarbeitung. Nach dem erfolgreichen CUDA-Sprachtest setzt die Anleitung `LocalWhisperModelFile` auf `ggml-large-v3.bin` und `LocalWhisperRequireCuda` auf `true`. Sie legt eine lokale Reserve fuer fehlende Dateien an.

Nicht pauschal ein grosses CPU-Modell aktivieren. Falls GPU-Speicher oder Treiber nicht passen, den konkreten Messbefund melden. Keine stundenlange CPU-Verarbeitung als erfolgreiches GPU-Setup ausgeben. Kein stilles Downgrade auf ein kleineres Modell, wenn hohe Qualitaet verlangt ist.

Ohne geeignete GPU den Befund melden. Wenn der Benutzer den CPU-Grundbetrieb waehlt und noch keine explizite GPU-Konfiguration besteht, verwendet Blitztext Small fuer Diktate und Base fuer Gespraeche. Fuer diesen Weg nach dem Dateitest im Hintergrund starten:

```powershell
pwsh -NoProfile -File .\setup.ps1 -VerifyOnly
if ($LASTEXITCODE -ne 0) { throw 'Installationspruefung fehlgeschlagen.' }
Start-Process -FilePath (Join-Path $env:LOCALAPPDATA 'Programs/BlitztextLocal/Blitztext.Windows.exe') -ArgumentList '--background' -WindowStyle Hidden
```

## 5. Erfolg belegen

- `setup.ps1 -VerifyOnly` prueft App, Small/Base und Silero. Full large-v3 und das NVIDIA-Backend separat mit [GPU.md](GPU.md) nachweisen.
- Im CUDA-Test muessen der erwartete deutsche Satz in beiden Durchlaeufen, `using CUDA0 backend` und `Runtime: Cuda` erscheinen. Im App-Startprotokoll fuer dieselbe Prozess-ID beide Modellpfade und `Backend PID=... NVIDIA GPU (CUDA)` pruefen.
- Mikrofon und Computerton testet der Benutzer selbst. Die App zeigt die gewaehlten Geraete und den Verarbeitungsrueckstand.
- Datenordner, verwendetes Modell, Backend und Grenzen knapp nennen. Taskleiste nicht automatisch manipulieren; Startmenue-Eintrag verwenden.

## Bedienung und Datenschutz

Strg+Umschalt+Leertaste startet und stoppt Diktate. Fuer Gespraeche Mikrofon und das tatsaechlich vom Call verwendete Ausgabegeraet waehlen. **Du** ist das Mikrofon, **Andere Teilnehmer** ist der gemeinsame Computerton. Das Transkript erscheint abschnittsweise mit Zeitstempeln und wird laufend gespeichert. Nach dem Stoppen die Verarbeitung abwarten. Dann die Datei direkt oeffnen oder im Explorer anzeigen.

Keine persoenlichen Einstellungen, Aufnahmen, Transkripte, Recovery-JSONs, Tokens oder Modelle committen. Bestehende Gespraeche erhalten. Die CLI-Wiederherstellung schreibt in eine neue Datei und behaelt die gesicherten Eingangsaudios.

Zeit und Quelle erscheinen nur beim Sprecherwechsel: aufeinanderfolgende Segmente derselben Quelle werden mit ihrem gesamten Zeitbereich und Text zu einem Block zusammengefasst. Das ist der Standard fuer Anzeige, Kopieren und Speicherung. Bereits bestehende Dateien koennen mit `scripts/Merge-TranscriptTurns.ps1 -Path <Datei>` angepasst werden; das Script erstellt zuerst eine unveraenderte Sicherung.
