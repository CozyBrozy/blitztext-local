# Live conversation

User-approved scope: show transcript while recording, timestamps, microphone labelled Du, system output labelled Andere Teilnehmer. Entirely local, no screen automation.

1. Add locked range snapshots to timeline writers without stopping capture, preserving silence and recording position.
2. Extract complete utterances at pauses or eight-second boundaries. Keep unfinished tails for the next pass and map all segment times to the session timeline.
3. Add serial background worker, per-source progress cursors, and atomic ongoing transcript saves. Show durable results immediately. Stop capture, drain worker, process remaining audio once and mark transcript finished.
4. Update conversation UI and source labels. Never paste or copy automatically. Keep prior saved text on errors. Show elapsed capture and transcription backlog.
5. Verify snapshots during ongoing writing, pause/tail handling, persistence before stop, source labels, timestamps and no duplicate processing. Run real local Whisper against synthetic growing tracks; no hardware capture or screen control.
6. Publish and install only when old app is idle, restart --background, update project state.
