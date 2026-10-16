# Conversation implementation
1. Add timestamped transcription contract and serialized Whisper processing. Preserve dictation interface.
2. Add formatter and conversation processor; test sorting, labels, hours, silence and cancellation.
3. Add dual capture and timeline padding, normalize native loopback audio to 16k mono, finalize both sources before transcription. Test normalization and delayed signal timeline with synthetic files.
4. Add dedicated conversation window with start/stop, progress, transcript, local save and copy. Gate dictation while conversation runs. Select output in settings.
5. Build, test native Whisper with two synthetic source files and timestamps. Do not capture real microphone or inspect the desktop.
6. Stage verified publish, update installation only when idle using application update signal; restart with --background so no window takes focus. Document exact verification limits.
