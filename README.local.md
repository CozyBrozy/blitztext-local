# Technischer Einstieg

Aktueller Stand: [README.md](README.md), Einrichtung durch KI: [ANLEITUNG.md](ANLEITUNG.md), GPU und Wiederherstellung: [GPU.md](GPU.md).

- `LocalWhisperTranscriber`: Whisper.net, CUDA, FlashAttention und serialisierte Aufrufe.
- `App.xaml.cs`: lokale Modellauswahl und gemeinsame Engine fuer identische Diktat-/Gespraechsmodelle, damit das Modell nur einmal im GPU-Speicher liegt.
- `ConversationRecorder`: getrennte Mikrofon- und WASAPI-Loopback-Spuren mit gemeinsamer Zeitleiste.
- `TimelineWaveWriter` und `LiveConversationWorker`: laufende Audio-Snapshots, Normalisierung und abschnittsweise Verarbeitung mit Quellen und relativen Zeitstempeln.
- `ConversationTranscriptFile`: atomarer Markdown-Export mit Aufnahme-/Verarbeitungsstatus. Bei Verarbeitungsfehlern behaelt das Fenster Audio zur Wiederherstellung.
- `ConversationRecoveryState` und `tools/RecoverConversation`: atomarer Wiederaufnahmestand mit SHA256-Bindung an Audio und Modell. Abgeschlossene Abschnitte werden nicht doppelt verarbeitet; bestehende fremde Transkripte werden nicht ueberschrieben.

31 Tests bestanden. CUDA wurde mit synthetischer Sprache und unveraendertem large-v3 auf RTX 500 Ada Laptop 4 GB geprueft. Messwerte im README sind Beispiele, keine allgemeine Echtzeitgarantie. Ein echter Hardware-Aufnahmetest bleibt beim Benutzer. Keine individuellen Sprecher-IDs innerhalb des Computertons.
