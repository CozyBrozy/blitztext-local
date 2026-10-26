using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Blitztext.Core.Conversation;
using Blitztext.Core.Transcription;
using Blitztext.Windows.Platform;
using Whisper.net.LibraryLoader;

if (args.Length != 3)
    throw new ArgumentException("RecoverConversation <recovery-directory> <model-path> <new-output.md>");
var directory = Path.GetFullPath(args[0]);
var model = Path.GetFullPath(args[1]);
var output = Path.GetFullPath(args[2]);
var statePath = output + ".recovery.json";
var work = Path.Combine(directory, "gpu-work");
Directory.CreateDirectory(work);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var inputs = new[] { (Source: "Du", Name: "microphone.wav"), (Source: "Andere Teilnehmer", Name: "computer.wav") };
static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
var fingerprint = string.Join("/", inputs.Select(i => Hash(Path.Combine(directory, i.Name))));
var modelHash = Hash(model);
var state = File.Exists(statePath)
    ? JsonSerializer.Deserialize<ConversationRecoveryState>(await File.ReadAllTextAsync(statePath))!
    : new ConversationRecoveryState { InputFingerprint = fingerprint, ModelFingerprint = modelHash };
state.Validate(fingerprint, modelHash);
if (File.Exists(output) && !File.Exists(statePath))
    throw new IOException("Vorhandenes Transkript ohne Wiederaufnahmestand wird nicht ueberschrieben.");
// Fail visibly if CUDA is unavailable. Never silently spend hours on CPU in this recovery tool.
RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Cuda];
using var engine = new LocalWhisperTranscriber(model);
var processor = new ConversationProcessor(engine);
var tracks = new List<ConversationTrack>();
foreach (var input in inputs)
{
    var normalized = Path.Combine(work, input.Name);
    ConversationAudio.Normalize(Path.Combine(directory, input.Name), normalized);
    tracks.AddRange(ConversationAudio.SplitOnPauses(normalized, input.Source));
}
tracks = tracks.OrderBy(t => t.Offset).ThenBy(t => t.Source, StringComparer.Ordinal).ToList();
var watch = Stopwatch.StartNew();
async Task Save(string status)
{
    await File.WriteAllTextAsync(statePath + ".tmp", JsonSerializer.Serialize(state));
    File.Move(statePath + ".tmp", statePath, true);
    var header = $"# Gespraech, lokal mit GPU verarbeitet\n\nDu: Mikrofon\nAndere Teilnehmer: Computerton, mehrere Personen moeglich\nModell: {Path.GetFileName(model)}\nZeitstempel ab Aufnahmebeginn. Keine individuelle Sprechererkennung.\n\nStatus: {status}\nFortschritt: {state.CompletedTracks.Count}/{tracks.Count} Sprachabschnitte\n\n";
    await File.WriteAllTextAsync(output + ".tmp", header + ConversationFormatter.Format(state.Entries));
    File.Move(output + ".tmp", output, true);
}
await Save("GPU-Verarbeitung laeuft");
try
{
    for (var i = 0; i < tracks.Count; i++)
    {
        if (state.CompletedTracks.Contains(i)) continue;
        var entries = await processor.ProcessAsync([tracks[i]], "de", null, CancellationToken.None);
        state.CompleteTrack(i, entries);
        await Save("GPU-Verarbeitung laeuft");
        Console.WriteLine($"{state.CompletedTracks.Count}/{tracks.Count}; {tracks[i].Source}; bis {tracks[i].Offset + tracks[i].Duration:hh\\:mm\\:ss}; {LocalWhisperTranscriber.Backend}; Laufzeit {watch.Elapsed:hh\\:mm\\:ss}");
    }
    await Save("Beendet");
    Console.WriteLine($"Fertig: {state.CompletedTracks.Count}/{tracks.Count}. Audio bleibt im Wiederherstellungsordner erhalten.");
}
catch
{
    await Save("Unterbrochen, mit gleichem Befehl fortsetzbar");
    throw;
}
