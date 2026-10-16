using System.IO;
using Blitztext.Core.Conversation;
namespace Blitztext.Windows.Platform;

public interface ILiveConversationAudio
{
    TimeSpan Elapsed { get; }
    string DirectoryPath { get; }
    void Snapshot(string source, TimeSpan start, TimeSpan end, string output);
}
public sealed class LiveConversationWorker(ILiveConversationAudio audio, ConversationProcessor processor,
    ConversationTranscriptFile transcript, string language)
{
    public static readonly string[] Sources = ["Du", "Andere Teilnehmer"];
    private readonly Dictionary<string, TimeSpan> cursors = Sources.ToDictionary(s => s, _ => TimeSpan.Zero);
    private long processedTicks;
    public TimeSpan Backlog => TimeSpan.FromSeconds(Math.Max(0, (audio.Elapsed - TimeSpan.FromTicks(Interlocked.Read(ref processedTicks))).TotalSeconds));
    public event Action<string>? Updated;

    // Only one caller/worker processes batches. No queued parallel Whisper requests.
    public async Task ProcessAsync(bool final)
    {
        foreach (var source in Sources)
        {
            var start = cursors[source];
            var available = audio.Elapsed - (final ? TimeSpan.Zero : TimeSpan.FromSeconds(0.35));
            var end = available < start + TimeSpan.FromSeconds(60) ? available : start + TimeSpan.FromSeconds(60);
            if (end <= start || (!final && end - start < TimeSpan.FromSeconds(1))) continue;
            var folder = System.IO.Path.Combine(audio.DirectoryPath, "live-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var snapshot = System.IO.Path.Combine(folder, "capture.wav");
                var normalized = System.IO.Path.Combine(folder, "normalized.wav");
                audio.Snapshot(source, start, end, snapshot);
                ConversationAudio.Normalize(snapshot, normalized);
                var batch = ConversationAudio.PrepareLiveTracks(normalized, source, start, final);
                var entries = await processor.ProcessAsync(batch.Tracks, language, null, CancellationToken.None);
                // Persist before advancing the cursor or displaying new text.
                if (entries.Count > 0)
                {
                    await transcript.SaveAsync(entries);
                    Updated?.Invoke(transcript.Text);
                }
                cursors[source] = start + batch.Consumed;
                Interlocked.Exchange(ref processedTicks, cursors.Values.Min().Ticks);
            }
            finally
            {
                foreach (var file in Directory.EnumerateFiles(folder)) File.Delete(file);
                Directory.Delete(folder);
            }
        }
    }
    public async Task FinishAsync()
    {
        while (cursors.Values.Any(t => audio.Elapsed - t > TimeSpan.FromMilliseconds(1)))
            await ProcessAsync(true);
        await transcript.SaveAsync([], "Beendet");
        Updated?.Invoke(transcript.Text);
    }
}
