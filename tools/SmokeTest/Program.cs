using Blitztext.Core.Transcription;
using System.Diagnostics;
using Whisper.net.LibraryLoader;
using Whisper.net.Logger;
using var logs = LogProvider.AddConsoleLogging(WhisperLogLevel.Info);
var requireCuda = args.Contains("--gpu-only");
using var engine = new LocalWhisperTranscriber(args[0], requireCuda);
var watch = Stopwatch.StartNew();
for (var run = 0; run < (requireCuda ? 2 : 1); run++)
{
    var text = await engine.TranscribeAsync(args[1], args.Length > 2 ? args[2] : "de", default);
    Console.WriteLine(text);
    if (!text.Contains("Computer", StringComparison.OrdinalIgnoreCase) || !text.Contains("lokal", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Expected German test sentence was not recognized.");
}
Console.WriteLine($"Elapsed: {watch.Elapsed.TotalSeconds:F1}s");
Console.WriteLine($"Runtime: {RuntimeOptions.LoadedLibrary}");
