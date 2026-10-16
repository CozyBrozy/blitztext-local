using System.IO;
using NAudio.Wave;
namespace Blitztext.Windows.Platform;

// Loopback can emit no packets while the output is silent. Preserve those gaps.
public sealed class TimelineWaveWriter : IDisposable
{
    private readonly object sync = new();
    private WaveFileWriter? writer;
    private readonly WaveFormat format;
    private readonly FileStream stream;
    private readonly long dataStart;
    public string Path { get; }
    public TimelineWaveWriter(string path, WaveFormat format)
    {
        Path = path;
        this.format = format;
        stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        writer = new WaveFileWriter(stream, format);
        dataStart = stream.Position;
    }
    public void Snapshot(TimeSpan start, TimeSpan end, string output)
    {
        if (start < TimeSpan.Zero || end < start) throw new ArgumentOutOfRangeException(nameof(start));
        lock (sync)
        {
            long offset = (long)(start.TotalSeconds * format.SampleRate) * format.BlockAlign;
            long length = (long)((end - start).TotalSeconds * format.SampleRate) * format.BlockAlign;
            using var snapshot = new WaveFileWriter(output, format);
            var buffer = new byte[format.AverageBytesPerSecond];
            if (writer is not null)
            {
                long position = stream.Position;
                try
                {
                    stream.Position = dataStart + Math.Min(offset, writer.Length);
                    CopyRange(stream, snapshot, Math.Min(length, Math.Max(0, writer.Length - offset)), buffer);
                }
                finally { stream.Position = position; }
            }
            else
            {
                using var source = new WaveFileReader(Path);
                source.Position = Math.Min(offset, source.Length);
                CopyRange(source, snapshot, Math.Min(length, source.Length - source.Position), buffer);
            }
            Array.Clear(buffer);
            while (snapshot.Length < length)
                snapshot.Write(buffer, 0, (int)Math.Min(buffer.Length, length - snapshot.Length));
        }
    }
    private static void CopyRange(Stream input, WaveFileWriter output, long remaining, byte[] buffer)
    {
        while (remaining > 0)
        {
            int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0) break;
            output.Write(buffer, 0, read);
            remaining -= read;
        }
    }
    public void Append(byte[] buffer, int count, TimeSpan end)
    {
        lock (sync)
        {
            if (writer is null) return;
            long targetEnd = (long)(end.TotalSeconds * format.AverageBytesPerSecond);
            long gap = targetEnd - count - writer.Length;
            // Ignore callback jitter below 100 ms instead of stretching every packet.
            if (gap > format.AverageBytesPerSecond / 10) PadTo(targetEnd - count);
            writer.Write(buffer, 0, count);
        }
    }
    private void PadTo(long target)
    {
        if (writer is null) return;
        target -= target % format.BlockAlign;
        var zeros = new byte[format.AverageBytesPerSecond];
        while (writer.Length < target)
            writer.Write(zeros, 0, (int)Math.Min(zeros.Length, target - writer.Length));
    }
    public void Finish(TimeSpan duration)
    {
        lock (sync)
        {
            PadTo((long)(duration.TotalSeconds * format.AverageBytesPerSecond));
            writer?.Dispose();
            writer = null;
        }
    }
    public void Dispose() { lock (sync) { writer?.Dispose(); writer = null; } }
}
