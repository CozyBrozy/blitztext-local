# Auftrag fuer Codex: aktuelle lokale Blitztext-Version einrichten

Installiere die App selbst. Arbeite mit Dateien und Terminal im Hintergrund. Keine Bildschirmsteuerung, Screenshots oder heimlichen Mikrofon-/Loopback-Tests. Keine API-Keys, Audio-Uploads oder Cloud-Verarbeitung.

## 1. Bestand und Hardware pruefen

- Windows 11 x64, AVX2 und PowerShell 7 pruefen. Bei Bedarf PowerShell aus dem offiziellen Microsoft-Paket installieren; keine ExecutionPolicy oder Windows-Schutzfunktion umgehen.
- Bestehende App und Datenordner erkennen. Eine laufende Aufnahme oder Verarbeitung nicht beenden. Vor einem Update den Benutzer fertig werden und die App ueber das Tray beenden lassen.
- Bei einer festgefahrenen Verarbeitung zuerst beide vollstaendigen Audios und das bisherige Markdown ausserhalb des Repos sichern und die Audio-Pruefsummen vergleichen. Erst danach einen Ersatzlauf vorbereiten.
- `nvidia-smi` pruefen. Bei passender NVIDIA-GPU CUDA nach GPU.md einrichten. Die CUDA-Versionsanzeige des Treibers beweist noch nicht, dass die benoetigten DLLs installiert sind.

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

Setup prueft Microsoft-Signaturen bzw. offizielle SDK-/Modell-Pruefsummen, installiert bei Bedarf das .NET-8-SDK im Benutzerprofil, stellt NuGet-Pakete wieder her, testet und publiziert die App. Small und Base werden fuer den kompatiblen Grundbetrieb geladen. Der Startmenue-Link wird erstellt. Eine verlangte Adminfreigabe fuer Visual C++ erledigt der Benutzer selbst. Setup beendet keine laufende App.

## 4. Hohe Qualitaet mit NVIDIA

Bei geeigneter NVIDIA-GPU **GPU.md lesen und den dortigen Ablauf ausfuehren**. Er installiert die benoetigten NVIDIA-DLLs lokal, laedt und prueft full large-v3, testet CUDA mit synthetischer Sprache ohne Mikrofon und setzt erst nach erfolgreichem Test `LocalWhisperModelFile` auf `ggml-large-v3.bin`.

Nicht pauschal ein grosses CPU-Modell aktivieren. Falls GPU-Speicher oder Treiber nicht passen, den konkreten Messbefund melden. Keine stundenlange CPU-Verarbeitung als erfolgreiches GPU-Setup ausgeben. Kein stilles Downgrade auf ein kleineres Modell, wenn hohe Qualitaet verlangt ist.

Ohne GPU-Einrichtung bleiben die Small-/Base-Standards erhalten. Dann nach dem Dateitest im Hintergrund starten:

```powershell
pwsh -NoProfile -File .\setup.ps1 -VerifyOnly
if ($LASTEXITCODE -ne 0) { throw 'Installationspruefung fehlgeschlagen.' }
Start-Process -FilePath (Join-Path $env:LOCALAPPDATA 'Programs/BlitztextLocal/Blitztext.Windows.exe') -ArgumentList '--background' -WindowStyle Hidden
```

## 5. Erfolg belegen

- Installierte App, Modelle und Pruefsummen pruefen. `setup.ps1 -VerifyOnly` prueft Small/Base und den Grundbetrieb; zusaetzliche GPU-/large-v3-Nachweise stehen in GPU.md.
- Fuer den GPU-Weg tatsaechlich `Runtime: Cuda` und NVIDIA-Geraet im synthetischen Test nachweisen. Ein vorhandener Treiber allein reicht nicht.
- Keine echte Aufnahme starten. Mikrofon-/Meetingtest macht der Benutzer selbst. Im Gespraech soll das Fenster `NVIDIA GPU (CUDA)` und den Rueckstand zeigen.
- Datenordner, verwendetes Modell, Backend und Grenzen knapp nennen. Taskleiste nicht automatisch manipulieren; Startmenue-Eintrag verwenden.

## Bedienung und Datenschutz

Strg+Umschalt+Leertaste startet/stoppt Diktate. Fuer Gespraeche Mikrofon und das tatsaechlich vom Call verwendete Ausgabegeraet waehlen. **Du** ist Mikrofon, **Andere Teilnehmer** ist Computerton; keine individuelle Sprechererkennung. Das Transkript erscheint abschnittsweise mit Zeitstempeln und wird laufend gespeichert. Nach Stoppen auf die Verarbeitung bis Fertig warten.

Keine persoenlichen Einstellungen, Aufnahmen, Transkripte, Recovery-JSONs, Tokens oder Modelle committen. Bestehende Gespraeche erhalten. Die CLI-Wiederherstellung schreibt in eine neue Datei und behaelt die gesicherten Eingangsaudios.
