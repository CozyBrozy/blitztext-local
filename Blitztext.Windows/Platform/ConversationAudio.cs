using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;
using Blitztext.Core.Conversation;
namespace Blitztext.Windows.Platform;
public static class ConversationAudio
{
    // Energy-based pause detection. Whisper timestamps alone often include leading silence.
    public static IReadOnlyList<ConversationTrack> SplitOnPauses(string normalizedPath, string source, double maxChunkSeconds = 28)
    {
        using var reader = new WaveFileReader(normalizedPath);
        if (reader.WaveFormat.SampleRate != 16000 || reader.WaveFormat.Channels != 1 || reader.WaveFormat.BitsPerSample != 16)
            throw new InvalidDataException("Für die Zeitzuordnung wird 16-kHz-Mono-PCM benötigt.");
        var ranges = new List<(double Start, double End)>();
        var frame = new byte[640]; // 20 ms PCM16
        double? start = null;
        double lastSignal = 0;
        double earliestStart = 0;
        double position = 0;
        int count;
        while ((count = reader.Read(frame, 0, frame.Length)) > 0)
        {
            double energy = 0;
            for (int i = 0; i + 1 < count; i += 2)
            {
                double sample = BitConverter.ToInt16(frame, i) / 32768.0;
                energy += sample * sample;
            }
            double end = position + count / 32000.0;
            bool signal = Math.Sqrt(energy / Math.Max(1, count / 2)) > 0.003;
            if (signal)
            {
                start ??= Math.Max(earliestStart, position - 0.2);
                lastSignal = end;
            }
            if (start is not null && ((!signal && end - lastSignal > 0.7) || end - start.Value >= maxChunkSeconds))
            {
                ranges.Add((start.Value, signal ? end : lastSignal + 0.25));
                earliestStart = ranges[^1].End;
                start = null;
            }
            position = end;
        }
        if (start is not null) ranges.Add((start.Value, Math.Min(position, lastSignal + 0.25)));
        var result = new List<ConversationTrack>();
        foreach (var range in ranges)
        {
            if (range.End - range.Start < 0.3) continue;
            var path = normalizedPath + $".chunk-{result.Count}.wav";
            using var writer = new WaveFileWriter(path, reader.WaveFormat);
            reader.Position = (long)(range.Start * 16000) * 2;
            long remaining = Math.Min(reader.Length - reader.Position, (long)((range.End - range.Start) * 16000) * 2);
            var buffer = new byte[32000];
            while (remaining > 0)
            {
                int read = reader.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read == 0) break;
                writer.Write(buffer, 0, read);
                remaining -= read;
            }
            result.Add(new(source, path, true, TimeSpan.FromSeconds(range.Start), TimeSpan.FromSeconds(range.End - range.Start)));
        }
        return result;
    }
    public static (IReadOnlyList<ConversationTrack> Tracks, TimeSpan Consumed) PrepareLiveTracks(
        string normalizedPath, string source, TimeSpan offset, bool final)
    {
        using var reader = new WaveFileReader(normalizedPath);
        var duration = reader.TotalTime;
        var chunks = SplitOnPauses(normalizedPath, source, 8);
        var ready = chunks.Where(c => final || c.Offset + c.Duration <= duration - TimeSpan.FromSeconds(0.4)).ToArray();
        TimeSpan consumed = final ? duration : ready.Length > 0 ? ready.Max(c => c.Offset + c.Duration)
            : chunks.Count > 0 ? chunks.Min(c => c.Offset) : TimeSpan.FromSeconds(Math.Max(0, duration.TotalSeconds - 0.25));
        return (ready.Select(c => c with { Offset = c.Offset + offset }).ToArray(), consumed);
    }
    public static bool Normalize(string input, string output)
    {
        using var reader = new AudioFileReader(input);
        ISampleProvider samples = reader;
        if (samples.WaveFormat.Channels > 1) samples = new AverageMono(samples);
        if (samples.WaveFormat.SampleRate != 16000) samples = new WdlResamplingSampleProvider(samples, 16000);
        using var writer = new WaveFileWriter(output, new WaveFormat(16000, 16, 1));
        var buffer = new float[16000];
        double sum = 0;
        long count = 0;
        int read;
        while ((read = samples.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++) sum += buffer[i] * buffer[i];
            count += read;
            writer.WriteSamples(buffer, 0, read);
        }
        return count > 0 && Math.Sqrt(sum / count) > 0.0005;
    }
    private sealed class AverageMono(ISampleProvider source) : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
        public int Read(float[] buffer, int offset, int count)
        {
            int channels = source.WaveFormat.Channels;
            var interleaved = new float[count * channels];
            int frames = source.Read(interleaved, 0, interleaved.Length) / channels;
            for (int frame = 0; frame < frames; frame++)
            {
                float sum = 0;
                for (int channel = 0; channel < channels; channel++) sum += interleaved[frame * channels + channel];
                buffer[offset + frame] = sum / channels;
            }
            return frames;
        }
    }
}
