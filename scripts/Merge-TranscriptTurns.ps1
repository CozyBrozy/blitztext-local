[CmdletBinding()]
param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = 'Stop'
$Path = (Resolve-Path -LiteralPath $Path).Path
$header = [Collections.Generic.List[string]]::new()
$turns = [Collections.Generic.List[object]]::new()
$turn = $null
foreach ($line in [IO.File]::ReadAllLines($Path)) {
    if ($line -match '^\[(\d{2,}:\d{2}:\d{2}) - (\d{2,}:\d{2}:\d{2})\] ([^:]+):\s?(.*)$') {
        $start, $end, $speaker, $text = $Matches[1], $Matches[2], $Matches[3], $Matches[4]
        if ($turn -and $turn.Speaker -ceq $speaker) {
            if ([TimeSpan]::Parse($end) -gt [TimeSpan]::Parse($turn.End)) { $turn.End = $end }
            $turn.Text += ' ' + $text
        } else {
            $turn = [pscustomobject]@{ Start = $start; End = $end; Speaker = $speaker; Text = $text }
            $turns.Add($turn)
        }
    } elseif (!$turn) {
        $header.Add($line)
    } elseif (![string]::IsNullOrWhiteSpace($line)) {
        throw 'Nicht erkannte Zeile im Transkript. Original bleibt unveraendert.'
    }
}
if (!$turns.Count) { throw 'Keine Transkriptabschnitte erkannt. Original bleibt unveraendert.' }
$output = [Collections.Generic.List[string]]::new()
$output.AddRange($header)
foreach ($item in $turns) { $output.Add("[$($item.Start) - $($item.End)] $($item.Speaker): $($item.Text)") }
$backup = $Path + '.before-turns-' + [Guid]::NewGuid().ToString('N') + '.bak'
Copy-Item -LiteralPath $Path -Destination $backup
$temporary = $Path + '.turns.tmp'
try {
    [IO.File]::WriteAllLines($temporary, $output, [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporary -Destination $Path -Force
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
Write-Output ("Zusammengefasst: {0} Sprecherbloecke. Unveraenderte Sicherung: {1}" -f $turns.Count, $backup)
