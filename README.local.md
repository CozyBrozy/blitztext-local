# Blitztext Lokal

Private local adaptation of echobrain-de/blitztext, inspired by cmagnussen/blitztext-app. Upstream MIT license remains in LICENSE.

## Use

Launch Blitztext Lokal from the taskbar or Start menu. Close settings, focus a text field, press Ctrl+Shift+Space to start, speak, then press the shortcut again to stop. The transcript is copied and optionally pasted into the active app. Change microphone and shortcut in settings. Default: German, Whisper Small multilingual on CPU.

## Conversation mode (2026-10-16)

Open Settings and choose the microphone and computer audio output, then click **Gesprächsmodus öffnen**. Alternatively use the tray menu **Gesprächsmodus**. Click **Gespräch starten** to see ongoing local transcription in the conversation window. Results appear in completed sections at pauses or after about eight seconds of continuous speech plus processing time. Labels are **Du** (microphone) and **Andere Teilnehmer** (system output); the latter can contain multiple people, without individual speaker identification. Approximate relative start/end timestamps are retained. A Markdown file is created immediately under `%LOCALAPPDATA%/BlitztextLocal/conversations` and updated atomically as text arrives, with both sources sorted by time. **Gespräch stoppen** stops capture and transcribes only outstanding audio before marking the file completed. There is no automatic paste or clipboard replacement. The Copy button is explicit. Existing dictation shortcut remains unchanged and is blocked while conversation recording/processing is active.

The computer source uses WASAPI loopback on the selected render endpoint. Select the device that plays the meeting audio; changing the Windows output during capture does not switch the recorded endpoint. Headphones help prevent microphone pickup of the same computer audio. Source labels do not identify individual people. Pause detection uses signal energy and timestamps are estimates. Only the selected microphone and selected output are captured.

Both tracks retain silence gaps. Locked range snapshots allow capture to continue while a serial background worker normalizes and transcribes completed sections. Unfinished tails remain for the next pass. The live model is multilingual Whisper Base on CPU; ordinary dictation continues to use Whisper Small. Processing can lag behind a busy meeting, and the window shows the approximate backlog. Base is faster but can be less accurate than Small. Raw and normalized audio are temporary and cleaned after processing, including error paths where filesystem access allows. Only the Markdown transcript is retained. Closing an active conversation window is blocked until recording and processing finish; minimizing is allowed. On errors, previously saved text is retained and the transcript is marked interrupted where possible.

Verification: 26 automated tests passed, including source ordering, overlap, offsets, silence detection, delayed loopback padding, stereo resampling, snapshots while writing, persistence/publication before stop, continuous speech, final tails, no repeated completed sections and preservation of the prior transcript when replacement is blocked. Real local Whisper was tested with synthetic growing source tracks: both labels and timestamps were saved while capture files remained open, new audio was added, and no completed speech was repeated. Base processed the first two-source test batch in about 4.8 seconds, versus 16.5 seconds for Small. These are synthetic processing timings, not a guaranteed end-to-end meeting latency. No real microphone/system audio capture and no screen automation were used for this update. Live dual-device capture remains to be tried by the user.

Installed update matches the verified publish DLL hash. Restart used `--background` and no window was opened. Previous installation backed up under `%LOCALAPPDATA%/BlitztextBuild/before-live-conversation`.

Live model: `%LOCALAPPDATA%/BlitztextLocal/models/ggml-base.bin`, verified SHA256 `60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe`, downloaded from the same ggerganov/whisper.cpp model repository. No audio is uploaded and no key is required.

## Dictation processing

This build constructs no OpenAI client, needs no API key and makes no downloads during dictation. Missing models produce a local error. Rewrite workflows are disabled. Temporary recordings are deleted after processing. Filesystem errors or forced termination can leave a temporary WAV. No transcript history is stored by this build.

Model: `%LOCALAPPDATA%/BlitztextLocal/models/ggml-small.bin`

Source: https://huggingface.co/ggerganov/whisper.cpp

Verified SHA256: `1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b`

Settings: `%LOCALAPPDATA%/BlitztextLocal/settings.json`

Installation: `%LOCALAPPDATA%/Programs/BlitztextLocal`

Source: `<repository-checkout>`, branch `local-dictation`.

SDK: `%LOCALAPPDATA%/BlitztextBuild/dotnet/dotnet.exe`

## Verification, 2026-10-16

26 automated tests passed. Ordinary dictation was previously tested with a synthetic German sentence and personally confirmed by the initial user. Start menu and taskbar shortcuts target the installed executable. See conversation verification above for current live-transcription evidence and remaining hardware-test limits.

## Build

Restore with explicit NuGet source `https://api.nuget.org/v3/index.json`. Run `dotnet test`, then publish `Blitztext.Windows/Blitztext.Windows.csproj` in Release for win-x64 with self-contained runtime. Stop the running local app before publishing over its installation.
