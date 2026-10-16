# Blitztext Lokal fuer Windows

Lokal diktieren und Gespraeche mit Mikrofon und Computerton live transkribieren. Ohne API-Key.

**Fuer Codex: [ANLEITUNG.md](ANLEITUNG.md) lesen und die Einrichtung ausfuehren.**

Dem Kollegen reicht dieser Auftrag:

> Richte https://github.com/CozyBrozy/blitztext-local auf meinem Windows-PC ein. Lies ANLEITUNG.md und fuehre die Einrichtung samt Whisper-Modellen und Pruefungen selbst aus. Arbeite im Hintergrund ohne Bildschirmsteuerung. Keine Audioaufnahme zum Testen starten.

Das Repository ist privat: Der Kollege braucht Zugriff mit seinem eigenen GitHub-Konto.

## Installation

- **Fertiges Paket inklusive Whisper:** [Releases](https://github.com/CozyBrozy/blitztext-local/releases), ZIP herunterladen und entpacken, setup.ps1 ausfuehren.
- **Aus Quellcode:** Im Repository pwsh -NoProfile -File .\setup.ps1 -FromSource -InstallPrerequisites ausfuehren.
- **Einrichtung durch Codex:** Vollstaendige Befehle, Erfolgskriterien und Fehlerbehandlung in ANLEITUNG.md.

Windows 11 x64 mit AVX2, Mikrofon; Visual C++ x64 Runtime wird benoetigt. Bei der ersten Einrichtung werden bei Bedarf Komponenten heruntergeladen. Die fertige Transkription laeuft offline.

## Bedienung

**Diktieren:** Blitztext Lokal ueber Startmenue starten, Einstellungen schliessen, Textfeld fokussieren. Strg+Umschalt+Leertaste startet und stoppt die Mikrofonaufnahme. Das Ergebnis wird eingefuegt.

**Gespraech:** In den Einstellungen Mikrofon und passende Computer-Audioausgabe waehlen, Gespraechsmodus oeffnen. Du bezeichnet das Mikrofon, Andere Teilnehmer die gemeinsame Computerquelle. Neue Textabschnitte erscheinen mit naeherungsweisen Zeitstempeln und werden laufend in einer Markdown-Datei gespeichert. Stoppen ergaenzt den Rest. Keine individuelle Sprechererkennung, kein automatisches Einfuegen des Gespraechs.

Teams kann ein anderes Ausgabegeraet als Windows verwenden. Das ausgewaehlte Geraet muss die Stimmen wirklich wiedergeben. Kopfhörer verhindern moeglichst, dass der Computerton auch im Mikrofon landet.

## Daten und Modellwahl

Gespraeche und Modelle liegen unter %LOCALAPPDATA%/BlitztextLocal, die App unter %LOCALAPPDATA%/Programs/BlitztextLocal. Keine Audio-Uploads und kein Cloud-Fallback. Temporaere Audiospuren werden nach Verarbeitung bestmoeglich geloescht; ein erzwungener Prozessabbruch kann temporaere Dateien hinterlassen.

Whisper Small fuer Diktate, schnelleres Whisper Base fuer Live-Gespraeche. Base kann weniger genau sein. Verarbeitung erfolgt abschnittsweise bei Pausen oder nach ca. acht Sekunden Dauersprache plus Rechenzeit. Bei hoher Last kann Rueckstand entstehen.

## Herkunft

Eigenstaendige Anpassung von [echobrain-de/blitztext](https://github.com/echobrain-de/blitztext), inspiriert von [Christoph Magnussen / Blackboat](https://github.com/cmagnussen/blitztext-app). Keine offizielle Blackboat-Windows-Version. MIT-Lizenz und Herkunftshinweise bleiben erhalten: [LICENSE](LICENSE), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

26 automatisierte Tests und synthetische lokale Whisper-Tests bestanden. Reale Mikrofon- und Meetingtests muss der jeweilige Benutzer mit seinen Geraeten durchfuehren.
