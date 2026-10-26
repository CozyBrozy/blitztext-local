# Full large-v3 mit NVIDIA CUDA

Gepruefter Ablauf fuer den aktuellen Quellcode auf Windows 11 x64. Befehle aus dem Repo-Root mit PowerShell 7 ausfuehren. Zuerst ANLEITUNG.md und den Grundsetup mit `-FromSource -NoStart` ausfuehren. Vor Aenderungen an einer bestehenden Installation Aufnahme/Verarbeitung fertig werden lassen und die App ueber das Tray beenden. Diese Befehle beenden keine App automatisch.

## 1. Hardware und Pfade

```powershell
$ErrorActionPreference = 'Stop'
nvidia-smi
if ($LASTEXITCODE -ne 0) { throw 'Kein erreichbarer NVIDIA-Treiber.' }
$app = Join-Path $env:LOCALAPPDATA 'Programs/BlitztextLocal'
$data = Join-Path $env:LOCALAPPDATA 'BlitztextLocal'
$cache = Join-Path $env:LOCALAPPDATA 'BlitztextBuild/cuda-quality-setup'
$sdk = Join-Path $env:LOCALAPPDATA 'BlitztextBuild/dotnet/dotnet.exe'
if (Get-Process -Name Blitztext.Windows -ErrorAction SilentlyContinue) { throw 'Blitztext zuerst selbst beenden.' }
New-Item -ItemType Directory -Force $cache, (Join-Path $data 'models') | Out-Null
if (!(Test-Path -LiteralPath (Join-Path $app 'Blitztext.Windows.exe'))) { throw 'Grundsetup fehlt.' }
```

Dieser Ablauf ist fuer CUDA-13-faehige Treiber vorgesehen. Geprueft: RTX 500 Ada Laptop 4 GB, Treiber 597.13. Andere Hardware muss separat getestet werden. Treiber nicht ungefragt austauschen. Die Anzeige `CUDA Version` in nvidia-smi ist die Treiberfaehigkeit, kein Nachweis vorhandener CUDA-DLLs.

## 2. Nur erforderliche NVIDIA-DLLs lokal installieren

Offizielle NVIDIA-Redistributables, fest auf die getesteten Versionen und SHA256 festgelegt. Kein globales CUDA Toolkit erforderlich. Die ZIPs enthalten NVIDIA-Lizenztexte; im Cache erhalten. Keine Weiterverteilung dieser Komponenten als eigenes Release ohne deren Lizenzbedingungen zu pruefen.

```powershell
$components = @(
    @{
        File = 'cuda_cudart/windows-x86_64/cuda_cudart-windows-x86_64-13.0.88-archive.zip'
        Hash = '48b1a445cbc2b2b468f872bc698a751d763ef5c06bc03ce559af1bc7e23f9345'
    },
    @{
        File = 'libcublas/windows-x86_64/libcublas-windows-x86_64-13.0.2.14-archive.zip'
        Hash = 'b03fd06fb14fa33e27fc441433fe74ccee9738b62c62a08d39960cb0ce9f1d14'
    }
)
foreach ($component in $components) {
    $zip = Join-Path $cache ([IO.Path]::GetFileName($component.File))
    if (!(Test-Path -LiteralPath $zip)) {
        Invoke-WebRequest ('https://developer.download.nvidia.com/compute/cuda/redist/' + $component.File) -OutFile $zip
    }
    if ((Get-FileHash -LiteralPath $zip).Hash -ne $component.Hash) { throw 'NVIDIA-Pruefsumme falsch. Nicht installieren.' }
    Expand-Archive -LiteralPath $zip -DestinationPath $cache -Force
}
$cudaDlls = @(Get-ChildItem -LiteralPath $cache -Recurse -Filter '*.dll' |
    Where-Object Name -in @('cudart64_13.dll','cublas64_13.dll','cublasLt64_13.dll'))
if ($cudaDlls.Count -ne 3) { throw 'Erwartete NVIDIA-DLLs fehlen oder sind mehrdeutig.' }
foreach ($dll in $cudaDlls) { Copy-Item -LiteralPath $dll.FullName -Destination $app -Force }
if (!(Test-Path -LiteralPath (Join-Path $app 'runtimes/cuda/win-x64/whisper.dll'))) { throw 'Aktueller CUDA-Build fehlt. Grundsetup aus Quellcode wiederholen.' }
```

## 3. Vollstaendiges Modell laden

Rund 3,1 GB, unveraendertes mehrsprachiges large-v3, keine Quantisierung und kein Turbo-Modell. Modellquelle: [whisper.cpp-Modellrepository](https://huggingface.co/ggerganov/whisper.cpp). Erwarteter SHA256 stammt aus den LFS-Metadaten.

```powershell
$model = Join-Path $data 'models/ggml-large-v3.bin'
$modelHash = '64d182b440b98d5203c4f9bd541544d84c605196c4f7b845dfa11fb23594d1e2'
if (!(Test-Path -LiteralPath $model)) {
    $partial = $model + '.download'
    Invoke-WebRequest 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin' -OutFile $partial
    if ((Get-FileHash -LiteralPath $partial).Hash -ne $modelHash) { throw 'Modell-Pruefsumme falsch.' }
    Move-Item -LiteralPath $partial -Destination $model
}
if ((Get-FileHash -LiteralPath $model).Hash -ne $modelHash) { throw 'Installiertes Modell hat falschen Hash.' }
```

## 4. CUDA ohne Mikrofon testen

Das SDK stammt aus dem Grundsetup. Synthetische Sprache ist kein Mikrofon-/Loopback-Test. System.Speech verwendet eine vorhandene Windows-Stimme; bei fehlender Sprachausgabe den konkreten Fehler melden.

```powershell
$smoke = Join-Path $cache 'smoke'
& $sdk publish tools/SmokeTest/SmokeTest.csproj -c Release -r win-x64 --self-contained true -o $smoke --source https://api.nuget.org/v3/index.json
if ($LASTEXITCODE -ne 0) { throw 'CUDA-Testbuild fehlgeschlagen.' }
foreach ($dll in $cudaDlls) { Copy-Item -LiteralPath $dll.FullName -Destination $smoke -Force }
Add-Type -AssemblyName System.Speech
$wav = Join-Path $cache 'synthetic-gpu-test.wav'
$speaker = [System.Speech.Synthesis.SpeechSynthesizer]::new()
try {
    $format = [System.Speech.AudioFormat.SpeechAudioFormatInfo]::new(16000, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
    $speaker.SetOutputToWaveFile($wav, $format)
    $speaker.Speak('Dieser Computer verarbeitet unsere Sprache lokal.')
} finally { $speaker.Dispose() }
$priorPath = $env:PATH
try {
    $env:PATH = $smoke + [IO.Path]::PathSeparator + $priorPath
    & (Join-Path $smoke 'SmokeTest.exe') $model $wav de --gpu-only
    if ($LASTEXITCODE -ne 0) { throw 'Echter CUDA-Test fehlgeschlagen. Qualitaetsmodell noch nicht aktivieren.' }
} finally { $env:PATH = $priorPath }
```

Ausgabe muss `Runtime: Cuda`, NVIDIA-Geraet und den erkannten Satz zeigen. Zusaetzlich bei Bedarf mit `nvidia-smi` GPU-Speicherbelegung waehrend des Tests pruefen. Bei Speicherfehlern nicht still auf CPU oder ein kleineres Modell wechseln. Erst den Befund klaeren. Keine zweite grosse GPU-Modellinstanz parallel starten.

## 5. Modell aktivieren und starten

Erst nach erfolgreichem CUDA-Test. Bestehende Einstellungen bleiben erhalten; eine Sicherung wird lokal angelegt.

```powershell
$settingsPath = Join-Path $data 'settings.json'
$settings = if (Test-Path -LiteralPath $settingsPath) {
    Copy-Item -LiteralPath $settingsPath -Destination ($settingsPath + '.before-cuda-' + (Get-Date -Format 'yyyyMMddHHmmss'))
    Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
} else { [pscustomobject]@{} }
$settings | Add-Member -NotePropertyName LocalWhisperModelFile -NotePropertyValue 'ggml-large-v3.bin' -Force
$settings | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath ($settingsPath + '.tmp') -Encoding utf8
Move-Item -LiteralPath ($settingsPath + '.tmp') -Destination $settingsPath -Force
Start-Process -FilePath (Join-Path $app 'Blitztext.Windows.exe') -ArgumentList '--background' -WindowStyle Hidden
```

Danach startet der Benutzer selbst ein Gespraech. Der Status nennt `NVIDIA GPU (CUDA)` und zeigt den Rueckstand auch nach Stoppen bis Fertig. Die Standardbeschriftungen bleiben Du und Andere Teilnehmer. Eine spaetere individuelle Umbenennung eines Transkripts ist keine automatische Sprechererkennung.

## Gesichertes Gespraech fortsetzen

Vor Eingriffen zuerst `microphone.wav`, `computer.wav` und das bisherige Markdown aus dem betroffenen Aufnahmeordner in einen privaten Recovery-Ordner ausserhalb des Repos kopieren. Audio-Pruefsummen mit den Originalen vergleichen. Ein Aufnahmeordner liegt unter `%TEMP%/BlitztextConversation/<Aufnahme-ID>`; die zugehoerige Aufnahme anhand Zeit und Dauer bestimmen. Aktive Aufnahmen zuerst beenden lassen. Gesicherte Audios unveraendert behalten.

Das Tool schreibt in eine **neue** Ausgabedatei und erzwingt CUDA. Vorherige Transkripte bleiben erhalten. Denselben Befehl nach einem Abbruch erneut ausfuehren: JSON-Checkpoint wird geladen, fertige Abschnitte werden uebersprungen. Geaenderte Audio-/Modelldateien werden anhand SHA256 zurueckgewiesen. Das Tool erkennt derzeit deutsche Sprache mit festem `de`.

```powershell
$runner = Join-Path $cache 'recovery-tool'
& $sdk publish tools/RecoverConversation/RecoverConversation.csproj -c Release -r win-x64 --self-contained true -o $runner --source https://api.nuget.org/v3/index.json
if ($LASTEXITCODE -ne 0) { throw 'Recovery-Build fehlgeschlagen.' }
foreach ($dll in $cudaDlls) { Copy-Item -LiteralPath $dll.FullName -Destination $runner -Force }
# Vor dem Aufruf konkrete private Pfade waehlen. Platzhalter nicht woertlich ausfuehren.
# & (Join-Path $runner 'RecoverConversation.exe') <gesicherter-audioordner> $model <neues-transkript.md>
```

Keine zweite grosse GPU-Transkription parallel betreiben. Vollstaendigkeit: `Status: Beendet`, alle Sprachabschnitte abgeschlossen und beide Quellen vorhanden. Wiederaufnahme eines bereits abgeschlossenen Laufs muss ohne Dopplungen und ohne Textaenderung enden. Audio und Recovery-JSON bleiben lokal und werden nicht automatisch geloescht oder hochgeladen.

## Quellen

- [Whisper.net 1.9.1 und CUDA-Runtimes](https://github.com/sandrohanea/whisper.net/tree/1.9.1)
- [Offizielle NVIDIA-Redistributable-Metadaten 13.0.1](https://developer.download.nvidia.com/compute/cuda/redist/redistrib_13.0.1.json)
- [Offizielle whisper.cpp-Modellmetadaten](https://huggingface.co/api/models/ggerganov/whisper.cpp/tree/main?limit=100)
