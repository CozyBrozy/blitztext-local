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
        ConversationEntry? turn = null;
        foreach (var entry in entries.OrderBy(e => e.Start).ThenBy(e => e.Source, StringComparer.Ordinal))
        {
            if (turn is not null && string.Equals(turn.Source, entry.Source, StringComparison.Ordinal))
                turn = turn with { End = entry.End > turn.End ? entry.End : turn.End, Text = turn.Text + " " + entry.Text };
            else
            {
                if (turn is not null) AppendTurn(text, turn);
                turn = entry;
            }
        }
        if (turn is not null) AppendTurn(text, turn);
        return text.ToString();
    }
    private static void AppendTurn(StringBuilder text, ConversationEntry turn)
        => text.AppendLine($"[{Stamp(turn.Start)} - {Stamp(turn.End)}] {turn.Source}: {turn.Text}");
    private static string Stamp(TimeSpan time)
        => $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
}
