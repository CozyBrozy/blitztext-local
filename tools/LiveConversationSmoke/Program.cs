using System.Diagnostics;
using Blitztext.Core.Conversation;
using Blitztext.Core.Transcription;
using Blitztext.Windows.Platform;
using NAudio.Wave;

using var audio = new GrowingAudio();
using var engine = new LocalWhisperTranscriber(args[0]);
var file = new ConversationTranscriptFile(Path.Combine(audio.DirectoryPath, "live.md"), "# Synthetischer Live-Test\n");
await file.SaveAsync([]);
var worker = new LiveConversationWorker(audio, new ConversationProcessor(engine), file, "de");
int updates = 0;
worker.Updated += _ => updates++;
audio.Append(args[1], args[2], 0);
audio.Elapsed = TimeSpan.FromSeconds(8);
var watch = Stopwatch.StartNew();
await worker.ProcessAsync(false);
var first = await File.ReadAllTextAsync(file.Path);
if (!first.Contains("Du:") || !first.Contains("Andere Teilnehmer:") || !first.Contains("Status: Aufnahme läuft"))
    throw new Exception("Both live sources must be saved before stopping.");
Console.WriteLine($"First two-source update before stop: {watch.Elapsed.TotalSeconds:F1}s processing");
Console.WriteLine(file.Text);
int firstLines = file.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
audio.Append(args[1], args[2], 8);
audio.Elapsed = TimeSpan.FromSeconds(16);
await worker.ProcessAsync(false);
int secondLines = file.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
if (secondLines <= firstLines || updates < 4) throw new Exception("New speech was not published incrementally.");
await worker.ProcessAsync(false);
if (file.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length != secondLines)
    throw new Exception("Completed speech was transcribed twice.");
audio.Seal();
await worker.FinishAsync();
if (!File.ReadAllText(file.Path).Contains("Status: Beendet")) throw new Exception("Final file not completed.");
Console.WriteLine(file.Text);
Console.WriteLine("PASS: live text and timestamps saved before stop, Du/Andere Teilnehmer, continuing audio, no repeated completed speech.");

sealed class GrowingAudio : ILiveConversationAudio, IDisposable
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "BlitztextLiveSmoke-" + Guid.NewGuid().ToString("N"));
    public TimeSpan Elapsed { get; set; }
    private readonly TimelineWaveWriter mic;
    private readonly TimelineWaveWriter other;
    public GrowingAudio()
    {
        Directory.CreateDirectory(DirectoryPath);
        mic = new(Path.Combine(DirectoryPath, "mic.wav"), new WaveFormat(16000, 16, 1));
        other = new(Path.Combine(DirectoryPath, "other.wav"), new WaveFormat(16000, 16, 1));
    }
    public void Append(string microphone, string computer, double start)
    {
        Write(mic, microphone, start);
        Write(other, computer, start + 1);
    }
    private static void Write(TimelineWaveWriter writer, string path, double start)
    {
        using var reader = new WaveFileReader(path);
        var buffer = new byte[(int)reader.Length];
        reader.ReadExactly(buffer);
        writer.Append(buffer, buffer.Length, TimeSpan.FromSeconds(start) + reader.TotalTime);
    }
    public void Snapshot(string source, TimeSpan start, TimeSpan end, string output)
        => (source == "Du" ? mic : other).Snapshot(start, end, output);
    public void Seal() { mic.Finish(Elapsed); other.Finish(Elapsed); }
    public void Dispose()
    {
        mic.Dispose(); other.Dispose();
        foreach (var sub in Directory.EnumerateDirectories(DirectoryPath))
        {
            foreach (var file in Directory.EnumerateFiles(sub)) File.Delete(file);
            Directory.Delete(sub);
        }
        foreach (var file in Directory.EnumerateFiles(DirectoryPath)) File.Delete(file);
        Directory.Delete(DirectoryPath);
    }
}
