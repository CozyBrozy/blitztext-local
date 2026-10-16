using Blitztext.Core.Transcription;
using System.Diagnostics;
using var engine = new LocalWhisperTranscriber(args[0]);
var watch = Stopwatch.StartNew();
var text = await engine.TranscribeAsync(args[1], args.Length > 2 ? args[2] : "de", default);
Console.WriteLine(text);
Console.WriteLine($"Elapsed: {watch.Elapsed.TotalSeconds:F1}s");
if (!text.Contains("Computer", StringComparison.OrdinalIgnoreCase) || !text.Contains("lokal", StringComparison.OrdinalIgnoreCase))
    throw new Exception("Expected German test sentence was not recognized.");
