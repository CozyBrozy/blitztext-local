using Blitztext.Core.Transcription;
using System.Diagnostics;
using Whisper.net.LibraryLoader;
using Whisper.net.Logger;
using var logs = LogProvider.AddConsoleLogging(WhisperLogLevel.Info);
if (args.Contains("--gpu-only")) RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Cuda];
using var engine = new LocalWhisperTranscriber(args[0]);
var watch = Stopwatch.StartNew();
var text = await engine.TranscribeAsync(args[1], args.Length > 2 ? args[2] : "de", default);
Console.WriteLine(text);
Console.WriteLine($"Elapsed: {watch.Elapsed.TotalSeconds:F1}s");
Console.WriteLine($"Runtime: {RuntimeOptions.LoadedLibrary}");
if (!text.Contains("Computer", StringComparison.OrdinalIgnoreCase) || !text.Contains("lokal", StringComparison.OrdinalIgnoreCase))
    throw new Exception("Expected German test sentence was not recognized.");
