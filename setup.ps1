[CmdletBinding()]
param(
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'Programs/BlitztextLocal'),
    [switch]$FromSource,
    [switch]$NoStart,
    [switch]$NoShortcuts,
    [switch]$InstallPrerequisites,
    [switch]$VerifyOnly
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
if ([Environment]::OSVersion.Platform -ne 'Win32NT' -or -not [Environment]::Is64BitOperatingSystem -or $env:PROCESSOR_ARCHITECTURE -eq 'ARM64') {
    throw 'Dieses Paket ist fuer Windows x64, nicht fuer ARM64 oder andere Betriebssysteme.'
}
if ([Environment]::OSVersion.Version.Build -lt 22000) { throw 'Die verwendete Whisper-Laufzeit benoetigt Windows 11 oder neuer.' }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$packageRoot = $PSScriptRoot
$modelDirectory = Join-Path $env:LOCALAPPDATA 'BlitztextLocal/models'
$buildDirectory = Join-Path $env:LOCALAPPDATA 'BlitztextBuild'
$models = Get-Content -LiteralPath (Join-Path $packageRoot 'models.json') -Raw | ConvertFrom-Json
$InstallDirectory = [IO.Path]::GetFullPath($InstallDirectory)
$appPath = Join-Path $InstallDirectory 'Blitztext.Windows.exe'

function Assert-Hash([string]$Path, [string]$Expected, [string]$Algorithm = 'SHA256') {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Datei fehlt: $Path" }
    if ((Get-FileHash -LiteralPath $Path -Algorithm $Algorithm).Hash -ne $Expected) { throw "Pruefsumme stimmt nicht: $Path" }
}
function Receive-Verified([string]$Url, [string]$Destination, [string]$Hash, [string]$Algorithm = 'SHA256') {
    $partial = $Destination + '.' + [Guid]::NewGuid().ToString('N') + '.download'
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $partial
        Assert-Hash $partial $Hash $Algorithm
        Move-Item -LiteralPath $partial -Destination $Destination
    } finally {
        if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial }
    }
}
function Get-LocalSdk {
    $sdkRoot = Join-Path $buildDirectory 'dotnet'
    $sdk = Join-Path $sdkRoot 'dotnet.exe'
    if (Test-Path -LiteralPath $sdk) {
        $versions = & $sdk --list-sdks
        if ($LASTEXITCODE -eq 0 -and ($versions | Where-Object { $_ -match '^8\.' })) { return $sdk }
    }
    $systemSdk = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($systemSdk) {
        $versions = & $systemSdk.Source --list-sdks
        if ($LASTEXITCODE -eq 0 -and ($versions | Where-Object { $_ -match '^8\.' })) { return $systemSdk.Source }
    }
    $version = '8.0.426'
    $metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json'
    $sdkRelease = $metadata.releases | ForEach-Object { $_.sdks } | Where-Object version -eq $version | Select-Object -First 1
    $download = $sdkRelease.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' } | Select-Object -First 1
    if (-not $download -or $download.url -notlike 'https://builds.dotnet.microsoft.com/*') { throw 'Offizielles .NET-SDK-Paket nicht gefunden.' }
    New-Item -ItemType Directory -Force $sdkRoot | Out-Null
    $archive = Join-Path $buildDirectory ('dotnet-sdk-' + $version + '.zip')
    if (-not (Test-Path -LiteralPath $archive)) { Receive-Verified $download.url $archive $download.hash 'SHA512' }
    Assert-Hash $archive $download.hash 'SHA512'
    Expand-Archive -LiteralPath $archive -DestinationPath $sdkRoot -Force
    if (-not (Test-Path -LiteralPath $sdk)) { throw 'SDK konnte nicht eingerichtet werden.' }
    return $sdk
}
function Assert-NativeRuntime {
    $systemDirectory = [Environment]::GetFolderPath('System')
    $missing = @('vcruntime140.dll', 'msvcp140.dll') | Where-Object { -not (Test-Path -LiteralPath (Join-Path $systemDirectory $_)) }
    if (-not $missing) { return }
    if (-not $InstallPrerequisites) { throw 'Visual C++ x64 Runtime fehlt. Setup mit -InstallPrerequisites ausfuehren; eine Windows-Adminfreigabe kann erforderlich sein.' }
    New-Item -ItemType Directory -Force $buildDirectory | Out-Null
    $installer = Join-Path $buildDirectory 'vc_redist.x64.exe'
    Invoke-WebRequest -UseBasicParsing 'https://aka.ms/vs/17/release/vc_redist.x64.exe' -OutFile $installer
    $signature = Get-AuthenticodeSignature -LiteralPath $installer
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') {
        throw 'Microsoft-Signatur des Visual-C++-Installers konnte nicht bestaetigt werden.'
    }
    $process = Start-Process -FilePath $installer -ArgumentList '/install', '/quiet', '/norestart' -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -notin @(0, 3010, 1638)) { throw "Visual-C++-Installation fehlgeschlagen, Code $($process.ExitCode)." }
    foreach ($file in @('vcruntime140.dll', 'msvcp140.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $systemDirectory $file))) { throw 'Visual C++ ist noch nicht verfuegbar. Installation/Neustart manuell pruefen.' }
    }
}

if ($VerifyOnly) {
    Assert-NativeRuntime
    if (-not (Test-Path -LiteralPath $appPath)) { throw "Installierte App fehlt: $appPath" }
    foreach ($model in $models) { Assert-Hash (Join-Path $modelDirectory $model.file) $model.sha256 }
    Write-Output 'OK: App, beide Modell-Pruefsummen und native Laufzeit vorhanden. Mikrofon-/Call-Test separat durchfuehren.'
    return
}
$running = Get-Process -Name Blitztext.Windows -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $appPath }
if ($running) { throw 'Diese Installation laeuft noch. Aufnahme beenden und App ueber das Tray-Menue beenden, dann Setup erneut starten. Keine Aufnahme wird automatisch beendet.' }
Assert-NativeRuntime
New-Item -ItemType Directory -Force $modelDirectory, $buildDirectory | Out-Null
foreach ($model in $models) {
    $destination = Join-Path $modelDirectory $model.file
    if (Test-Path -LiteralPath $destination) { Assert-Hash $destination $model.sha256; continue }
    $bundled = Join-Path (Join-Path $packageRoot 'models') $model.file
    if (Test-Path -LiteralPath $bundled) {
        Assert-Hash $bundled $model.sha256
        Copy-Item -LiteralPath $bundled -Destination $destination
    } else {
        Write-Output ('Lade lokales Whisper-Modell: ' + $model.file)
        Receive-Verified $model.url $destination $model.sha256
    }
    Assert-Hash $destination $model.sha256
}
$publish = Join-Path $packageRoot 'app'
if ($FromSource -or -not (Test-Path -LiteralPath (Join-Path $publish 'Blitztext.Windows.exe'))) {
    if (-not (Test-Path -LiteralPath (Join-Path $packageRoot 'Blitztext.sln'))) { throw 'Quellcode fehlt. Vollstaendiges Repo oder fertiges Release-ZIP verwenden.' }
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $sdk = Get-LocalSdk
    $publish = Join-Path $buildDirectory ('publish-' + [Guid]::NewGuid().ToString('N'))
    Push-Location $packageRoot
    try {
        & $sdk restore Blitztext.sln --source https://api.nuget.org/v3/index.json
        if ($LASTEXITCODE -ne 0) { throw 'NuGet-Restore fehlgeschlagen.' }
        & $sdk test Blitztext.sln --no-restore --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Tests fehlgeschlagen. Es wird nicht installiert.' }
        & $sdk publish Blitztext.Windows/Blitztext.Windows.csproj -c Release -r win-x64 --self-contained true -o $publish --source https://api.nuget.org/v3/index.json
        if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen.' }
    } finally { Pop-Location }
}
if (-not (Test-Path -LiteralPath (Join-Path $publish 'Blitztext.Windows.exe'))) { throw 'Keine installierbare App vorhanden.' }
New-Item -ItemType Directory -Force $InstallDirectory | Out-Null
Copy-Item -Path (Join-Path $publish '*') -Destination $InstallDirectory -Recurse -Force
if (-not $NoShortcuts) {
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs/Blitztext Lokal.lnk'))
    $link.TargetPath = $appPath
    $link.WorkingDirectory = $InstallDirectory
    $link.Description = 'Lokal diktieren und Live-Gespraeche transkribieren, ohne API-Key.'
    $link.Save()
}
@{
    app = $appPath
    models = $modelDirectory
    conversations = (Join-Path $env:LOCALAPPDATA 'BlitztextLocal/conversations')
    installedAt = [DateTimeOffset]::Now.ToString('O')
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $InstallDirectory 'INSTALLATION.json') -Encoding UTF8
if (-not $NoStart) { Start-Process -FilePath $appPath -ArgumentList '--background' -WindowStyle Hidden }
Write-Output ('Installiert: ' + $appPath)
Write-Output 'Startmenue: Blitztext Lokal. Diktat: Strg+Umschalt+Leertaste. Gespraechsmodus: in den Einstellungen oeffnen.'
Write-Output 'Alles lokal. Fuer Computerton muss das ausgewaehlte Ausgabegeraet mit Teams uebereinstimmen.'
