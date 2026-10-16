namespace Blitztext.Core.Transcription;
public interface ILocalTranscriber
{
    Task<string> TranscribeAsync(string audioPath, string language, CancellationToken cancellationToken);
}
