using Whisper.net;
using Whisper.net.LibraryLoader;
using Whisper.net.Logger;
namespace Blitztext.Core.Transcription;
public sealed class LocalWhisperTranscriber(string modelPath, bool requireCuda = false) : ILocalTranscriber, ISegmentTranscriber, IDisposable
{
    private const string CudaUnavailable =
        "NVIDIA CUDA ist nicht verfügbar. Prüfe die NVIDIA-GPU und den Treiber und starte Blitztext erneut.";
    private const string CudaBackendFailed =
        "Whisper konnte die NVIDIA-GPU nicht für das Modell initialisieren.";

    public static string Backend => RuntimeOptions.LoadedLibrary switch
    {
        RuntimeLibrary.Cuda => "NVIDIA GPU (CUDA)",
        RuntimeLibrary.Cuda12 => "NVIDIA GPU (CUDA 12)",
        null => "GPU/CPU wird beim ersten Text geladen",
        var runtime => "CPU/" + runtime
    };
    private WhisperFactory? factory;
    private readonly SemaphoreSlim gate = new(1, 1);

    public Task PreflightCudaAsync(CancellationToken cancellationToken = default) =>
        Task.Run(EnsureCudaRuntime, cancellationToken);

    private static void EnsureCudaRuntime()
    {
        // A CUDA-only runtime order in Whisper.net 1.9.1 can allow CPU fallback.
        // The default order probes for a usable CUDA device before selecting CUDA.
        _ = WhisperFactory.GetRuntimeInfo();
        if (RuntimeOptions.LoadedLibrary is not (RuntimeLibrary.Cuda or RuntimeLibrary.Cuda12))
            throw new InvalidOperationException(CudaUnavailable);
    }

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
                LocalWhisperModels.ValidateModel(modelPath);
                if (requireCuda) EnsureCudaRuntime();
                factory ??= WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions
                {
                    UseGpu = true,
                    UseFlashAttention = true
                });
                var builder = factory.CreateBuilder()
                    .WithLanguage(string.IsNullOrWhiteSpace(language) ? "auto" : language);
                var cudaBackend = 0;
                using var backendLog = requireCuda ? LogProvider.AddLogger((_, message) =>
                {
                    // Whisper.cpp in Whisper.net 1.9.1 initializes its GPU backend before the encoder.
                    if (message?.StartsWith("whisper_backend_init_gpu: using CUDA", StringComparison.Ordinal) == true)
                        Interlocked.CompareExchange(ref cudaBackend, 1, 0);
                    else if (message?.StartsWith("whisper_backend_init_gpu: failed to initialize CUDA", StringComparison.Ordinal) == true)
                        Interlocked.Exchange(ref cudaBackend, -1);
                }) : null;
                if (requireCuda)
                    builder.WithEncoderBeginHandler(_ => Volatile.Read(ref cudaBackend) == 1);

                using var processor = builder.Build();
                await using var audio = File.OpenRead(audioPath);
                var segments = new List<TranscriptionSegment>();
                try
                {
                    await foreach (var segment in processor.ProcessAsync(audio, cancellationToken))
                        if (!string.IsNullOrWhiteSpace(segment.Text))
                            segments.Add(new(segment.Start, segment.End, segment.Text.Trim()));
                }
                catch (WhisperProcessingException ex) when (requireCuda && Volatile.Read(ref cudaBackend) != 1)
                {
                    throw new InvalidOperationException(CudaBackendFailed, ex);
                }
                if (requireCuda && Volatile.Read(ref cudaBackend) != 1)
                    throw new InvalidOperationException(CudaBackendFailed);
                return (IReadOnlyList<TranscriptionSegment>)segments;
            }, cancellationToken);
        }
        finally { gate.Release(); }
    }
    public void Dispose() { factory?.Dispose(); gate.Dispose(); }
}
