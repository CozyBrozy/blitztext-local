using Blitztext.Core.Conversation;
using Blitztext.Core.Transcription;
using Blitztext.Windows.Platform;
using NAudio.Wave;

var temp = Path.Combine(Path.GetTempPath(), "blitztext-conversation-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var mic = Path.Combine(temp, "mic.wav");
    var system = Path.Combine(temp, "system.wav");
    using (var reader = new WaveFileReader(args[1]))
    using (var writer = new TimelineWaveWriter(mic, reader.WaveFormat))
    {
        var buffer = new byte[(int)reader.Length];
        reader.ReadExactly(buffer);
        writer.Append(buffer, buffer.Length, TimeSpan.FromSeconds(8) + reader.TotalTime);
        writer.Finish(TimeSpan.FromSeconds(25));
    }
    using (var reader = new WaveFileReader(args[2]))
    using (var writer = new TimelineWaveWriter(system, reader.WaveFormat))
    {
        var buffer = new byte[(int)reader.Length];
        reader.ReadExactly(buffer);
        writer.Append(buffer, buffer.Length, TimeSpan.FromSeconds(1) + reader.TotalTime);
        writer.Finish(TimeSpan.FromSeconds(25));
    }
    var normalizedMic = Path.Combine(temp, "mic-16k.wav");
    var normalizedSystem = Path.Combine(temp, "system-16k.wav");
    ConversationAudio.Normalize(mic, normalizedMic);
    ConversationAudio.Normalize(system, normalizedSystem);
    var tracks = ConversationAudio.SplitOnPauses(normalizedMic, "Mikrofon")
        .Concat(ConversationAudio.SplitOnPauses(normalizedSystem, "Computerton")).ToArray();
    using var engine = new LocalWhisperTranscriber(args[0]);
    var result = await new ConversationProcessor(engine).ProcessAsync(tracks, "de", null, default);
    Console.WriteLine(ConversationFormatter.Format(result));
    if (!result.Any(e => e.Source == "Mikrofon" && e.Text.Contains("Mikrofon", StringComparison.OrdinalIgnoreCase)))
        throw new Exception("Microphone source missing.");
    if (!result.Any(e => e.Source == "Computerton" && e.Text.Contains("Computer", StringComparison.OrdinalIgnoreCase)))
        throw new Exception("System source missing.");
    if (result.Where(e => e.Source == "Mikrofon").Min(e => e.Start) < TimeSpan.FromSeconds(5))
        throw new Exception("Delayed microphone timing was not preserved.");
    Console.WriteLine("PASS: Both sources, real local Whisper, delayed timeline preserved.");
}
finally
{
    foreach (var file in Directory.EnumerateFiles(temp)) File.Delete(file);
    Directory.Delete(temp);
}
