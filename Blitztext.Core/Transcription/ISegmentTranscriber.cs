namespace Blitztext.Core.Transcription;
public sealed record TranscriptionSegment(TimeSpan Start, TimeSpan End, string Text);
public interface ISegmentTranscriber
{
    Task<IReadOnlyList<TranscriptionSegment>> TranscribeSegmentsAsync(
        string audioPath, string language, CancellationToken cancellationToken);
}
