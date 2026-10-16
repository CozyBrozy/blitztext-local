[CmdletBinding()]
param(
    [string]$PublishDirectory = (Join-Path $env:LOCALAPPDATA 'BlitztextBuild/live-conversation-publish'),
    [string]$ModelDirectory = (Join-Path $env:LOCALAPPDATA 'BlitztextLocal/models'),
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'dist')
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path -LiteralPath (Join-Path $PublishDirectory 'Blitztext.Windows.exe'))) { throw 'Verifiziertes self-contained Publish-Verzeichnis fehlt.' }
$models = Get-Content -LiteralPath (Join-Path $root 'models.json') -Raw | ConvertFrom-Json
foreach ($model in $models) {
    if ((Get-FileHash -LiteralPath (Join-Path $ModelDirectory $model.file) -Algorithm SHA256).Hash -ne $model.sha256) { throw ('Modellhash falsch: ' + $model.file) }
}
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$stage = Join-Path $OutputDirectory ('package-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $stage, (Join-Path $stage 'app'), (Join-Path $stage 'models') | Out-Null
Copy-Item -Path (Join-Path $PublishDirectory '*') -Destination (Join-Path $stage 'app') -Recurse
Get-ChildItem -LiteralPath (Join-Path $stage 'app') -Recurse -File -Filter '*.pdb' | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
foreach ($binary in @('Blitztext.Core.dll','Blitztext.Windows.dll')) {
    $binaryText = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes((Join-Path (Join-Path $stage 'app') $binary)))
    if ($binaryText.IndexOf($env:USERNAME, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
        $binaryText -match '(?i)[A-Z]:[\\/]+Users[\\/]+') {
        throw ('Lokaler Benutzer-/Buildpfad in Release-Binaerdatei gefunden: ' + $binary)
    }
}
foreach ($model in $models) { Copy-Item -LiteralPath (Join-Path $ModelDirectory $model.file) -Destination (Join-Path $stage 'models') }
foreach ($file in @('setup.ps1','models.json','README.md','ANLEITUNG.md','THIRD_PARTY_NOTICES.md','LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination $stage
}
Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination $stage -Recurse
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $OutputDirectory 'Blitztext-Lokal-Windows-x64.zip'
if (Test-Path -LiteralPath $zip) { throw 'Release-ZIP existiert bereits. Nicht stillschweigend ueberschreiben.' }
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
($hash + '  Blitztext-Lokal-Windows-x64.zip') | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ASCII
Write-Output $zip
Write-Output $stage
Write-Output $hash
