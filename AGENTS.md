# Regeln fuer KI-Agenten

- Aktuellen Einrichtungsablauf in ANLEITUNG.md und GPU.md lesen. Release v0.1.1 ist nicht der aktuelle GPU-Quellstand.
- Lokal ohne API-Key arbeiten. Keine Audios oder Transkripte hochladen. Keine Cloud- oder Rewrite-Funktion ungefragt aktivieren.
- Ohne ausdruecklichen Auftrag keine Bildschirmsteuerung oder echte Mikrofon-/Loopback-Aufnahme verwenden.
- Laufende Aufnahme oder Verarbeitung nicht gewaltsam beenden. Vor einem Reparatureingriff beide vollstaendigen Audios und das bisherige Transkript ausserhalb des Repos sichern und Audio-Pruefsummen vergleichen.
- Modelle, Audios, Transkripte, Recovery-JSONs, persoenliche Einstellungen und Zugangsdaten niemals committen oder in Releases packen. Auch Dateinamen, Buildpfade, PDBs und Git-Diffs auf persoenliche Daten pruefen.
- GPU-Unterstuetzung mit einem echten synthetischen CUDA-Lauf nachweisen. Ein erkannter NVIDIA-Treiber allein reicht nicht. Keine versteckte stundenlange CPU-Verarbeitung oder stiller Wechsel auf ein kleineres Modell.
- Vor Installation Tests ausfuehren. Keine persoenlichen Buildpfade oder Debugsymbole weitergeben. Vorhandene Sicherheitspruefungen nicht umgehen.
- Nur den fuer das Zielrepo autorisierten GitHub-Account nutzen und vor Push die Identitaet pruefen. Kein globaler Accountwechsel.
