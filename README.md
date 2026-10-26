# Blitztext Lokal fuer Windows

Lokal diktieren und Gespraeche mit Mikrofon und Computerton live transkribieren. Ohne API-Key.

**Fuer Codex: [ANLEITUNG.md](ANLEITUNG.md) lesen und die Einrichtung ausfuehren.**

Dem Kollegen reicht dieser Auftrag:

> Richte https://github.com/CozyBrozy/blitztext-local auf meinem Windows-PC ein. Lies ANLEITUNG.md. Pruefe meine Hardware und verwende bei geeigneter NVIDIA-GPU das vollstaendige Whisper large-v3 mit CUDA nach GPU.md. Installiere App und Modelle selbst, pruefe die Einrichtung und erhalte bestehende Gespraeche. Arbeite im Hintergrund ohne Bildschirmsteuerung. Keine Audioaufnahme zum Testen starten.

Das Repository ist oeffentlich. Fuer Download und Einrichtung ist kein GitHub-Konto erforderlich.

## Installation

- **Aktueller GPU-Stand vom 26.10.2026:** Aus dem Quellcode mit `pwsh -NoProfile -File .\setup.ps1 -FromSource -InstallPrerequisites -NoStart` installieren. Danach [GPU.md](GPU.md) fuer NVIDIA/CUDA und large-v3 ausfuehren.
- **Aelteres Paket:** [Release v0.1.1](https://github.com/CozyBrozy/blitztext-local/releases/tag/v0.1.1) enthaelt die fruehere CPU-Version mit Small/Base, ohne die neue GPU-Unterstuetzung und Wiederherstellung.
- **Einrichtung durch Codex:** Vollstaendige Befehle, Erfolgskriterien und Fehlerbehandlung in ANLEITUNG.md.

Windows 11 x64 mit AVX2, Mikrofon; Visual C++ x64 Runtime wird benoetigt. Bei der ersten Einrichtung werden bei Bedarf Komponenten heruntergeladen. Die fertige Transkription laeuft offline.

## Bedienung

**Diktieren:** Blitztext Lokal ueber Startmenue starten, Einstellungen schliessen, Textfeld fokussieren. Strg+Umschalt+Leertaste startet und stoppt die Mikrofonaufnahme. Das Ergebnis wird eingefuegt.

**Gespraech:** In den Einstellungen Mikrofon und passende Computer-Audioausgabe waehlen, Gespraechsmodus oeffnen. Du bezeichnet das Mikrofon, Andere Teilnehmer die gemeinsame Computerquelle. Neue Textabschnitte erscheinen mit naeherungsweisen Zeitstempeln und werden laufend in einer Markdown-Datei gespeichert. Stoppen ergaenzt den Rest. Keine individuelle Sprechererkennung, kein automatisches Einfuegen des Gespraechs.

Teams kann ein anderes Ausgabegeraet als Windows verwenden. Das ausgewaehlte Geraet muss die Stimmen wirklich wiedergeben. Kopfhörer verhindern moeglichst, dass der Computerton auch im Mikrofon landet.

## Daten und Modellwahl

Gespraeche und Modelle liegen unter %LOCALAPPDATA%/BlitztextLocal, die App unter %LOCALAPPDATA%/Programs/BlitztextLocal. Keine Audio-Uploads und kein Cloud-Fallback. Erfolgreich verarbeitete Gespraechsaudios werden bestmoeglich geloescht. Bei Verarbeitungsfehlern bleiben Audio und gespeicherter Text zur Wiederherstellung erhalten; ein erzwungener Prozessabbruch kann temporaere Dateien hinterlassen. Vor einem Eingriff beide Audios und das bisherige Transkript separat sichern. [GPU.md](GPU.md) beschreibt die fortsetzbare Wiederherstellung.

Ohne explizite Auswahl: Small fuer Diktate und Base fuer Gespraeche. Fuer hohe Qualitaet mit passender NVIDIA-GPU: full large-v3 fuer beide Funktionen, CUDA und FlashAttention. Nur eine gemeinsame serialisierte Modellinstanz wird geladen. Full large-v3 auf CPU ist fuer laengere Gespraeche sehr langsam und wird nicht automatisch ausgewaehlt. Ein explizit ausgewaehltes fehlendes Modell wird nicht still ersetzt.

Verarbeitung erfolgt abschnittsweise bei Pausen oder nach ca. acht Sekunden Dauersprache plus Rechenzeit. Das Fenster zeigt Backend und Rueckstand, auch nach dem Stoppen. **Stoppen beendet die Aufnahme; offene Verarbeitung laeuft bis Fertig weiter.**

Beispielmessung: RTX 500 Ada Laptop 4 GB, full large-v3 mit ca. 3571 MiB GPU-Belegung. Ein ca. 27-minuetiger Zweikanallauf wurde in ca. 4:45 Minuten verarbeitet. Synthetischer Satz: ca. 8,6 statt 38,5 Sekunden auf CPU, einschliesslich Modellladen. Das sind Werte eines Rechners, keine allgemeine Echtzeit- oder Qualitaetsgarantie.

Persoenliche Einstellungen, Audios, Transkripte und Wiederaufnahmestaende gehoeren ausschliesslich in den lokalen Datenordner. [AGENTS.md](AGENTS.md) enthaelt die Regeln fuer KI-Agenten.

## Herkunft

Eigenstaendige Anpassung von [echobrain-de/blitztext](https://github.com/echobrain-de/blitztext), inspiriert von [Christoph Magnussen / Blackboat](https://github.com/cmagnussen/blitztext-app). Keine offizielle Blackboat-Windows-Version. MIT-Lizenz und Herkunftshinweise bleiben erhalten: [LICENSE](LICENSE), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

31 automatisierte Tests sowie synthetische lokale CPU-/CUDA-Tests bestanden. Wiederaufnahme eines abgeschlossenen Verarbeitungslaufs war ohne Dopplungen und mit identischem Ausgabehash erfolgreich. Reale Mikrofon- und Meetingtests muss der jeweilige Benutzer mit seinen Geraeten durchfuehren.
