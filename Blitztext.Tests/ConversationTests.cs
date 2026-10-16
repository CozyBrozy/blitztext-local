using Blitztext.Core.Conversation;
using Blitztext.Core.Transcription;
using Blitztext.Windows.Platform;
using NAudio.Wave;
namespace Blitztext.Tests;

public sealed class ConversationTests
{
    [Fact]
    public async Task KeepsOverlapsAndSortsBothSourcesOnOneTimeline()
    {
        var processor = new ConversationProcessor(new Segments());
        var result = await processor.ProcessAsync([
            new("Mikrofon", "mic", true), new("Computerton", "system", true)], "de", null, default);
        Assert.Equal(new[] { "Computerton", "Mikrofon" }, result.Select(s => s.Source));
        var text = ConversationFormatter.Format(result);
        Assert.Contains("[00:00:01 - 00:00:04] Computerton: Antwort vom Computer.", text);
        Assert.Contains("[00:00:02 - 00:00:05] Mikrofon: Frage am Mikrofon.", text);
    }
    [Fact]
    public async Task SilentTrackIsNotSentToWhisper()
    {
        var result = await new ConversationProcessor(new Segments()).ProcessAsync([
            new("Mikrofon", "mic", true), new("Computerton", "must-not-run", false)], "de", null, default);
        Assert.Single(result);
        Assert.Equal("Mikrofon", result[0].Source);
    }
    [Fact]
    public async Task LocalSegmentTimesAreOffsetBackOntoRecordingTimeline()
    {
        var result = await new ConversationProcessor(new Segments()).ProcessAsync([
            new("Mikrofon", "mic", true, TimeSpan.FromSeconds(8))], "de", null, default);
        Assert.Equal(TimeSpan.FromSeconds(10), result[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(13), result[0].End);
    }
    [Fact]
    public async Task CancellationStopsBeforeAnyTranscription()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ConversationProcessor(new Segments()).ProcessAsync([new("Mikrofon", "must-not-run", true)],
                "de", null, new CancellationToken(true)));
    }
    [Fact]
    public void FormattingPreservesHoursAndChronologicalOrder()
    {
        var text = ConversationFormatter.Format([
            new("Mikrofon", TimeSpan.FromSeconds(3661), TimeSpan.FromSeconds(3665), "Später."),
            new("Computerton", TimeSpan.Zero, TimeSpan.FromSeconds(2), "Zuerst.")]);
        Assert.StartsWith("[00:00:00", text);
        Assert.Contains("[01:01:01 - 01:01:05] Mikrofon: Später.", text);
    }
    [Fact]
    public void DelayedLoopbackAndTrailingSilenceSurviveStereoResampling()
    {
        var input = Path.GetTempFileName();
        var output = Path.GetTempFileName();
        try
        {
            var format = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
            var samples = new float[48000 * 2];
            for (int i = 0; i < 48000; i++)
                samples[2 * i] = samples[2 * i + 1] = (float)Math.Sin(i * 2 * Math.PI * 440 / 48000) * 0.4f;
            var bytes = new byte[samples.Length * sizeof(float)];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            using (var writer = new TimelineWaveWriter(input, format))
            {
                writer.Append(bytes, bytes.Length, TimeSpan.FromSeconds(5));
                writer.Finish(TimeSpan.FromSeconds(6));
            }
            Assert.True(ConversationAudio.Normalize(input, output));
            using var audio = new WaveFileReader(output);
            Assert.Equal(16000, audio.WaveFormat.SampleRate);
            Assert.Equal(1, audio.WaveFormat.Channels);
            Assert.Equal(16, audio.WaveFormat.BitsPerSample);
            Assert.InRange(audio.TotalTime.TotalSeconds, 5.99, 6.01);
            var initial = new byte[16000 * 2 * 3];
            Assert.Equal(initial.Length, audio.Read(initial, 0, initial.Length));
            Assert.All(initial, b => Assert.Equal((byte)0, b));
            audio.CurrentTime = TimeSpan.FromSeconds(4.2);
            var signal = new byte[3200];
            audio.Read(signal, 0, signal.Length);
            Assert.Contains(signal, b => b != 0);
            var chunks = ConversationAudio.SplitOnPauses(output, "Computerton");
            try
            {
                Assert.Single(chunks);
                Assert.Equal("Computerton", chunks[0].Source);
                Assert.InRange(chunks[0].Offset.TotalSeconds, 3.7, 4.0);
                using var chunk = new WaveFileReader(chunks[0].AudioPath);
                Assert.InRange(chunk.TotalTime.TotalSeconds, 1.2, 1.6);
            }
            finally { foreach (var track in chunks) File.Delete(track.AudioPath); }
        }
        finally { File.Delete(input); File.Delete(output); }
    }
    [Fact]
    public void EntirelySilentTrackIsDetected()
    {
        var input = Path.GetTempFileName();
        var output = Path.GetTempFileName();
        try
        {
            using (var writer = new TimelineWaveWriter(input, new WaveFormat(16000, 16, 1)))
                writer.Finish(TimeSpan.FromSeconds(2));
            Assert.False(ConversationAudio.Normalize(input, output));
            Assert.Empty(ConversationAudio.SplitOnPauses(output, "Computerton"));
        }
        finally { File.Delete(input); File.Delete(output); }
    }
    private sealed class Segments : ISegmentTranscriber
    {
        public Task<IReadOnlyList<TranscriptionSegment>> TranscribeSegmentsAsync(string path, string language, CancellationToken token)
        {
            Assert.Equal("de", language);
            Assert.NotEqual("must-not-run", path);
            IReadOnlyList<TranscriptionSegment> result = path == "mic"
                ? [new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), "Frage am Mikrofon.")]
                : [new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4), "Antwort vom Computer.")];
            return Task.FromResult(result);
        }
    }
}
