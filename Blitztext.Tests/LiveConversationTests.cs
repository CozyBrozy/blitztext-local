using Blitztext.Core.Conversation;
using Blitztext.Core.Transcription;
using Blitztext.Windows.Platform;
using NAudio.Wave;
namespace Blitztext.Tests;

public sealed class LiveConversationTests
{
    [Fact]
    public void SnapshotDoesNotStopOrCorruptContinuingCapture()
    {
        using var audio = new GrowingAudio();
        audio.Append(0, 3, true);
        var path = Path.Combine(audio.DirectoryPath, "snapshot.wav");
        audio.Snapshot("Du", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), path);
        using (var reader = new WaveFileReader(path)) Assert.InRange(reader.TotalTime.TotalSeconds, 2.999, 3.001);
        audio.Append(3, 2, true);
        audio.Seal();
        using var full = new WaveFileReader(Path.Combine(audio.DirectoryPath, "mic.wav"));
        Assert.InRange(full.TotalTime.TotalSeconds, 4.999, 5.001);
        full.CurrentTime = TimeSpan.FromSeconds(4);
        var signal = new byte[3200];
        full.ReadExactly(signal);
        Assert.Contains(signal, b => b != 0);
    }
    [Fact]
    public async Task BothLabelsAndTimestampsAreSavedAndPublishedBeforeStop()
    {
        using var audio = new GrowingAudio();
        audio.Append(0, 3, true);
        audio.Elapsed = TimeSpan.FromSeconds(5);
        var file = new ConversationTranscriptFile(Path.Combine(audio.DirectoryPath, "live.md"), "# Test\n");
        await file.SaveAsync([]);
        var engine = new FakeSegments();
        var worker = new LiveConversationWorker(audio, new ConversationProcessor(engine), file, "de");
        var updates = new List<string>();
        worker.Updated += updates.Add;
        await worker.ProcessAsync(false);
        var live = await File.ReadAllTextAsync(file.Path);
        Assert.Contains("Status: Aufnahme läuft", live);
        Assert.Contains("[00:00:00", live);
        Assert.Contains("Du: Testabschnitt", live);
        Assert.Contains("Andere Teilnehmer: Testabschnitt", live);
        Assert.NotEmpty(updates);
        int calls = engine.Calls;
        await worker.ProcessAsync(false);
        Assert.Equal(calls, engine.Calls);
        audio.Seal();
        await worker.FinishAsync();
        Assert.Equal(calls, engine.Calls);
        Assert.Contains("Status: Beendet", await File.ReadAllTextAsync(file.Path));
    }
    [Fact]
    public async Task ContinuousSpeechAppearsDuringCaptureAndFinalTailIsProcessedOnce()
    {
        using var audio = new GrowingAudio();
        audio.Append(0, 10, true);
        var file = new ConversationTranscriptFile(Path.Combine(audio.DirectoryPath, "live.md"), "# Test\n");
        await file.SaveAsync([]);
        var engine = new FakeSegments();
        var worker = new LiveConversationWorker(audio, new ConversationProcessor(engine), file, "de");
        await worker.ProcessAsync(false);
        Assert.Equal(2, engine.Calls); // First eight seconds of each source before stop.
        Assert.Equal(2, file.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        audio.Seal();
        await worker.FinishAsync();
        Assert.Equal(4, engine.Calls); // Just the two outstanding tails.
        Assert.Equal(4, file.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("[00:00:08", file.Text);
    }
    [Fact]
    public async Task ShortUnfinishedSpeechIsRetainedForStopInsteadOfBeingLost()
    {
        using var audio = new GrowingAudio();
        audio.Append(0, 2, true);
        var file = new ConversationTranscriptFile(Path.Combine(audio.DirectoryPath, "live.md"), "# Test\n");
        await file.SaveAsync([]);
        var engine = new FakeSegments();
        var worker = new LiveConversationWorker(audio, new ConversationProcessor(engine), file, "de");
        await worker.ProcessAsync(false);
        Assert.Equal(0, engine.Calls);
        audio.Seal();
        await worker.FinishAsync();
        Assert.Equal(2, engine.Calls);
    }
    [Fact]
    public async Task AtomicUpdateRetainsPriorFileIfReplacementIsBlocked()
    {
        using var audio = new GrowingAudio();
        var file = new ConversationTranscriptFile(Path.Combine(audio.DirectoryPath, "live.md"), "# Test\n");
        await file.SaveAsync([new("Du", TimeSpan.Zero, TimeSpan.FromSeconds(1), "Schon gespeichert.")]);
        var prior = await File.ReadAllTextAsync(file.Path);
        using (var locked = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = await Record.ExceptionAsync(() => file.SaveAsync([new("Andere Teilnehmer", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), "Noch nicht gespeichert.")]));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal(prior, await File.ReadAllTextAsync(file.Path));
    }
    private sealed class FakeSegments : ISegmentTranscriber
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<TranscriptionSegment>> TranscribeSegmentsAsync(string path, string language, CancellationToken token)
        {
            Calls++;
            using var audio = new WaveFileReader(path);
            return Task.FromResult<IReadOnlyList<TranscriptionSegment>>([new(TimeSpan.Zero, audio.TotalTime, "Testabschnitt " + Calls + ".")]);
        }
    }
    private sealed class GrowingAudio : ILiveConversationAudio, IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "BlitztextLiveTests-" + Guid.NewGuid().ToString("N"));
        public TimeSpan Elapsed { get; set; }
        private readonly TimelineWaveWriter mic;
        private readonly TimelineWaveWriter other;
        public GrowingAudio()
        {
            Directory.CreateDirectory(DirectoryPath);
            mic = new(Path.Combine(DirectoryPath, "mic.wav"), new WaveFormat(16000, 16, 1));
            other = new(Path.Combine(DirectoryPath, "other.wav"), new WaveFormat(16000, 16, 1));
        }
        public void Append(double start, double seconds, bool signal)
        {
            var bytes = new byte[(int)(seconds * 32000)];
            for (int i = 0; i < bytes.Length / 2; i++)
            {
                short sample = signal ? (short)(Math.Sin(i * 2 * Math.PI * 440 / 16000) * 16000) : (short)0;
                BitConverter.TryWriteBytes(bytes.AsSpan(i * 2, 2), sample);
            }
            Elapsed = TimeSpan.FromSeconds(start + seconds);
            mic.Append(bytes, bytes.Length, Elapsed);
            other.Append(bytes, bytes.Length, Elapsed);
        }
        public void Snapshot(string source, TimeSpan start, TimeSpan end, string output)
            => (source == "Du" ? mic : other).Snapshot(start, end, output);
        public void Seal() { mic.Finish(Elapsed); other.Finish(Elapsed); }
        public void Dispose()
        {
            mic.Dispose(); other.Dispose();
            foreach (var file in Directory.EnumerateFiles(DirectoryPath)) File.Delete(file);
            foreach (var sub in Directory.EnumerateDirectories(DirectoryPath))
            {
                foreach (var file in Directory.EnumerateFiles(sub)) File.Delete(file);
                Directory.Delete(sub);
            }
            Directory.Delete(DirectoryPath);
        }
    }
}
