using System.Text;
using Blitztext.Core.Transcription;
namespace Blitztext.Core.Conversation;
public sealed record ConversationEntry(string Source, TimeSpan Start, TimeSpan End, string Text);
public sealed record ConversationTrack(string Source, string AudioPath, bool HasSignal, TimeSpan Offset = default, TimeSpan Duration = default);
public sealed class ConversationProcessor(ISegmentTranscriber transcriber)
{
    public async Task<IReadOnlyList<ConversationEntry>> ProcessAsync(
        IReadOnlyList<ConversationTrack> tracks, string language,
        IProgress<string>? progress, CancellationToken token)
    {
        var entries = new List<ConversationEntry>();
        foreach (var track in tracks)
        {
            token.ThrowIfCancellationRequested();
            if (!track.HasSignal) continue;
            progress?.Report(track.Source + " wird lokal transkribiert ...");
            foreach (var segment in await transcriber.TranscribeSegmentsAsync(track.AudioPath, language, token))
            {
                var text = TranscriptionQualityService.CleanedTranscript(segment.Text);
                if (string.IsNullOrWhiteSpace(text) ||
                    TranscriptionQualityService.IsLikelyArtifact(text, segment.End - segment.Start)) continue;
                entries.Add(new(track.Source, segment.Start + track.Offset, segment.End + track.Offset, text));
            }
        }
        return entries.OrderBy(s => s.Start).ThenBy(s => s.Source, StringComparer.Ordinal).ToArray();
    }
}
public static class ConversationFormatter
{
    public static string Format(IEnumerable<ConversationEntry> entries)
    {
        var text = new StringBuilder();
        foreach (var entry in entries.OrderBy(e => e.Start).ThenBy(e => e.Source, StringComparer.Ordinal))
            text.AppendLine($"[{Stamp(entry.Start)} - {Stamp(entry.End)}] {entry.Source}: {entry.Text}");
        return text.ToString();
    }
    private static string Stamp(TimeSpan time)
        => $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
}
