using Whisper.net;
namespace Blitztext.Core.Transcription;
public sealed class LocalWhisperTranscriber(string modelPath) : ILocalTranscriber, ISegmentTranscriber, IDisposable
{
    private WhisperFactory? factory;
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task<string> TranscribeAsync(string audioPath, string language, CancellationToken cancellationToken)
        => string.Join(" ", (await TranscribeSegmentsAsync(audioPath, language, cancellationToken)).Select(s => s.Text));
    public async Task<IReadOnlyList<TranscriptionSegment>> TranscribeSegmentsAsync(
        string audioPath, string language, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(modelPath))
                    throw new InvalidOperationException("Lokales Whisper-Modell fehlt: " + modelPath);
                factory ??= WhisperFactory.FromPath(modelPath);
                using var processor = factory.CreateBuilder()
                    .WithLanguage(string.IsNullOrWhiteSpace(language) ? "auto" : language).Build();
                await using var audio = File.OpenRead(audioPath);
                var segments = new List<TranscriptionSegment>();
                await foreach (var segment in processor.ProcessAsync(audio, cancellationToken))
                    if (!string.IsNullOrWhiteSpace(segment.Text))
                        segments.Add(new(segment.Start, segment.End, segment.Text.Trim()));
                return (IReadOnlyList<TranscriptionSegment>)segments;
            }, cancellationToken);
        }
        finally { gate.Release(); }
    }
    public void Dispose() { factory?.Dispose(); gate.Dispose(); }
}
