# Auftrag fuer Codex: Blitztext Lokal auf diesem Windows-PC einrichten

Fuehre die Einrichtung selbst aus. Liefere keine blosse Anleitung als Ersatz fuer die Installation. Arbeite ueber Dateien und Terminal im Hintergrund, ohne Bildschirmsteuerung, Screenshots oder Fernbedienung. Starte keine Mikrofon- oder Computertonaufnahme zum Testen.

## Ziel

Die App soll auf dem PC des aktuellen Benutzers einsatzbereit sein, inklusive beider lokal installierter Whisper-Modelle und Startmenue-Verknuepfung. Es sind keine API-Keys, kein OpenAI-Konto und kein ChatGPT-Abo fuer die Transkription erforderlich.

- Diktieren: Mikrofon, Strg+Umschalt+Leertaste startet und stoppt, Text ins aktive Feld.
- Gespraech: Mikrofon und gewaehlte Audioausgabe gleichzeitig, mitlaufender Text mit Zeitstempeln. **Du** ist die Mikrofonquelle; **Andere Teilnehmer** ist die gemeinsame Computerquelle. Das ist keine individuelle Sprechererkennung.
- Die Gespraechsdatei entsteht ab Start und wird laufend lokal aktualisiert. Beim Stoppen wird nur der offene Rest verarbeitet.

## 1. Zugriff und Dateien

Repository: `https://github.com/CozyBrozy/blitztext-local`

Das Repository ist oeffentlich. Der Download benoetigt keine Anmeldung. Bei einem Netzwerk- oder Zugriffsfehler diesen konkret melden; keine fremden Zugangsdaten anfordern.

Wenn der Quellcode noch nicht lokal liegt:

```powershell
gh repo clone CozyBrozy/blitztext-local
Set-Location blitztext-local
```

Falls `gh` nicht installiert ist, mit vorhandenem Git und bereits eingerichteter eigener Anmeldung klonen:

```powershell
git clone https://github.com/CozyBrozy/blitztext-local.git
Set-Location blitztext-local
```

## 2. Bevorzugt: fertiges Release mit beiden Modellen

Pruefe zuerst `Get-Command pwsh`. Nutze PowerShell 7. Falls nicht vorhanden, kann es ueber den offiziellen Microsoft-Eintrag installiert werden:

```powershell
winget install --id Microsoft.PowerShell --exact --source winget --silent --accept-package-agreements --accept-source-agreements
```

Danach `pwsh.exe` ueber den tatsaechlichen Installationspfad oder eine neue Shell verwenden. Eine verlangte Windows-Adminfreigabe muss der Benutzer selbst erledigen. Keine ExecutionPolicy aendern und keine Sperre umgehen. Bei einer verbindlichen Unternehmensrichtlinie, die auch PowerShell 7 sperrt, den konkreten Blocker melden.

Das Release `v0.1.1` enthaelt `Blitztext-Lokal-Windows-x64.zip` mit fertiger App, .NET-Laufzeit, Whisper Small und Whisper Base. Modelle liegen absichtlich im Release-Asset statt in der Git-Historie. Keine persoenlichen Einstellungen oder Gespraeche sind enthalten.

Lade und pruefe das oeffentliche Paket ohne GitHub-Anmeldung:

```powershell
$packageDirectory = Join-Path $env:LOCALAPPDATA 'BlitztextBuild/colleague-package'
New-Item -ItemType Directory -Force $packageDirectory | Out-Null
$zip = Join-Path $packageDirectory 'Blitztext-Lokal-Windows-x64.zip'
$releaseBase = 'https://github.com/CozyBrozy/blitztext-local/releases/download/v0.1.1'
Invoke-WebRequest -UseBasicParsing ($releaseBase + '/Blitztext-Lokal-Windows-x64.zip') -OutFile $zip
Invoke-WebRequest -UseBasicParsing ($releaseBase + '/Blitztext-Lokal-Windows-x64.zip.sha256') -OutFile ($zip + '.sha256')
$expected = ((Get-Content -LiteralPath ($zip + '.sha256') -Raw).Trim() -split '\s+')[0]
if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $expected) { throw 'Release-Pruefsumme stimmt nicht.' }
$expanded = Join-Path $packageDirectory 'expanded'
Expand-Archive -LiteralPath $zip -DestinationPath $expanded -Force
pwsh -NoProfile -File (Join-Path $expanded 'setup.ps1') -InstallPrerequisites
if ($LASTEXITCODE -ne 0) { throw 'Einrichtung fehlgeschlagen. Konkreten Fehler pruefen.' }
```

App und Modelle werden aus dem ZIP installiert. Nur wenn Visual C++ x64 fehlt, wird der signierte Microsoft-Installer heruntergeladen; eine Windows-Adminfreigabe kann erforderlich sein. Diese muss der Benutzer selbst bestaetigen. Ist ein Paket bereits manuell entpackt, dessen `setup.ps1` direkt verwenden.

## 3. Alternative: vollstaendig aus diesem Quellcode

Wenn kein Release-Download moeglich ist, aber der Quellcode vorliegt, funktioniert die Einrichtung ohne GitHub-CLI:

```powershell
pwsh -NoProfile -File .\setup.ps1 -FromSource -InstallPrerequisites
if ($LASTEXITCODE -ne 0) { throw 'Einrichtung fehlgeschlagen. Konkreten Fehler pruefen.' }
```

`setup.ps1` installiert bei Bedarf das .NET-8-SDK nur im Benutzerprofil, prueft dessen offizielles SHA512, laedt beide Whisper-Modelle von Hugging Face und prueft deren SHA256 gegen `models.json`. Danach folgen NuGet-Restore, Tests, Release-Build, Installation und Startmenue-Verknuepfung. Die App wird ohne Fenster im Hintergrund gestartet. Bestehende Modelle werden geprueft und wiederverwendet. Ein anderer bereits laufender Installationsstand wird nicht gewaltsam beendet.

## 4. Nachweis

Im Repo oder entpackten Paket ausfuehren:

```powershell
pwsh -NoProfile -File .\setup.ps1 -VerifyOnly
if ($LASTEXITCODE -ne 0) { throw 'Installationspruefung fehlgeschlagen.' }
Get-Process Blitztext.Windows -ErrorAction SilentlyContinue | Select-Object Id, Responding
```

Pruefe, dass die App existiert und beide Modellhashes stimmen. Ein erfolgreicher Dateitest ist noch kein Nachweis einer funktionierenden echten Audioaufnahme. Keine Aufnahme heimlich starten. Erst der Benutzer probiert danach Diktieren und einen Call aus.

Voraussetzungen: Windows 11 x64, CPU mit AVX2-Unterstuetzung, Mikrofon. Kein ARM64-Paket. Die Installation selbst benoetigt Internet, sofern nicht App und Modelle bereits im ZIP vorliegen. Die Transkription benoetigt danach kein Internet.

## 5. Keine unnoetigen Aenderungen

- Keine API-Keys eintragen und keinen Cloud-Fallback hinzufuegen.
- Keine Windows-Schutzfunktionen, Mikrofonrechte oder ExecutionPolicy aendern; keine SmartScreen-Sperre umgehen. Falls eine tatsaechliche Sperre auftritt, den Benutzer die konkrete Freigabe selbst erledigen lassen.
- Keine App-Prozesse automatisch beenden und keine laufende Aufnahme unterbrechen. Bei einem Update den Benutzer zuerst seine Aufnahme und die App beenden lassen.
- Keine persoenlichen Dateien, Transkripte oder Einstellungen aus diesem Repo erwarten oder uebertragen. Bestehende lokale Daten erhalten.
- Nicht automatisch an der Taskleiste manipulieren. Startmenue-Verknuepfung erstellen; Anheften kann der Benutzer manuell vornehmen.

## 6. Abschluss fuer den Benutzer

Sage knapp, wo **Blitztext Lokal** gestartet wird und wie das Diktierkuerzel funktioniert. Fuer Gespraeche: App oeffnen, Mikrofon und das tatsaechlich von Teams verwendete Ausgabegeraet waehlen, **Gespraechsmodus oeffnen**, **Gespraech starten**. Text erscheint abschnittsweise mit Verzoegerung und wird fortlaufend gespeichert. Kopfhörer vermeiden doppelt aufgenommenen Computerton. Mehrere Computerstimmen bleiben unter **Andere Teilnehmer** zusammengefasst.

Installationsorte:

- App: `%LOCALAPPDATA%/Programs/BlitztextLocal/Blitztext.Windows.exe`
- Modelle: `%LOCALAPPDATA%/BlitztextLocal/models`
- Gespraeche: `%LOCALAPPDATA%/BlitztextLocal/conversations`, auch ueber **Ablage oeffnen** erreichbar.
- Normale Diktate werden nicht als Gespraechsdatei abgelegt.

Berichte durchgefuehrte Pruefungen und tatsaechliche Blocker. Behaupte keinen Mikrofon- oder Call-Test, wenn nur Dateien und Prozessstart geprueft wurden.
