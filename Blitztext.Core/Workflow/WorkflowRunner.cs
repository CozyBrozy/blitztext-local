using Blitztext.Core.Models;
using Blitztext.Core.Settings;
using Blitztext.Core.Transcription;
namespace Blitztext.Core.Workflow;
public sealed class WorkflowRunner(ILocalTranscriber transcriber, Func<AppSettings> settingsProvider)
{
    public async Task<string> ProcessAsync(WorkflowType type, string audioPath,
        TimeSpan recordingDuration, CancellationToken cancellationToken)
    {
        try
        {
            if (type != WorkflowType.Transcription)
                throw new InvalidOperationException("Diese Version unterstützt ausschließlich lokales Diktieren.");
            cancellationToken.ThrowIfCancellationRequested();
            if (TranscriptionQualityService.ShouldRejectRecording(recordingDuration))
                throw new InvalidOperationException("Keine Aufnahme erkannt.");
            var raw = await transcriber.TranscribeAsync(audioPath, settingsProvider().Language, cancellationToken);
            var cleaned = TranscriptionQualityService.CleanedTranscript(raw);
            if (string.IsNullOrWhiteSpace(cleaned) || TranscriptionQualityService.IsLikelyArtifact(cleaned, recordingDuration))
                throw new InvalidOperationException("Keine Sprache erkannt.");
            return cleaned;
        }
        finally
        {
            try { File.Delete(audioPath); } catch { }
        }
    }
}
