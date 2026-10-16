using System.Diagnostics;
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;
namespace Blitztext.Windows.Platform;

public sealed record OutputDeviceInfo(string Id, string Name);
public sealed record CapturedTrack(string Source, string Path);
public sealed class ConversationRecorder : IDisposable, ILiveConversationAudio
{
    private readonly Stopwatch clock = new();
    private WaveInEvent? microphone;
    private WasapiLoopbackCapture? loopback;
    private MMDevice? renderDevice;
    private TimelineWaveWriter? micWriter;
    private TimelineWaveWriter? systemWriter;
    private readonly TaskCompletionSource micStopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource systemStopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool stopping;
    private bool disposed;
    public TimeSpan Elapsed => clock.Elapsed;
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "BlitztextConversation", Guid.NewGuid().ToString("N"));
    public string OutputName { get; private set; } = "";
    public string MicrophoneName { get; private set; } = "";
    public event EventHandler<string>? CaptureFailed;
    public void Snapshot(string source, TimeSpan start, TimeSpan end, string output)
    {
        var track = source == "Du" ? micWriter : source == "Andere Teilnehmer" ? systemWriter
            : throw new ArgumentException("Unbekannte Audioquelle.", nameof(source));
        (track ?? throw new InvalidOperationException("Keine Aufnahme gestartet.")).Snapshot(start, end, output);
    }

    public static IReadOnlyList<OutputDeviceInfo> Outputs()
    {
        using var devices = new MMDeviceEnumerator();
        var result = new List<OutputDeviceInfo> { new("", "Windows-Standardausgabe") };
        foreach (var device in devices.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            result.Add(new(device.ID, device.FriendlyName));
            device.Dispose();
        }
        return result;
    }
    public void Start(int microphoneIndex, string outputId)
    {
        Directory.CreateDirectory(DirectoryPath);
        using var devices = new MMDeviceEnumerator();
        renderDevice = string.IsNullOrWhiteSpace(outputId)
            ? devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
            : devices.GetDevice(outputId);
        OutputName = renderDevice.FriendlyName;
        MicrophoneName = WaveInEvent.GetCapabilities(microphoneIndex).ProductName;
        microphone = new WaveInEvent { DeviceNumber = microphoneIndex, WaveFormat = new WaveFormat(16000, 16, 1) };
        loopback = new WasapiLoopbackCapture(renderDevice);
        micWriter = new(Path.Combine(DirectoryPath, "microphone.wav"), microphone.WaveFormat);
        systemWriter = new(Path.Combine(DirectoryPath, "computer.wav"), loopback.WaveFormat);
        microphone.DataAvailable += (_, e) => Write(micWriter, e);
        loopback.DataAvailable += (_, e) => Write(systemWriter, e);
        microphone.RecordingStopped += (_, e) => Finished(micStopped, e, "Mikrofon");
        loopback.RecordingStopped += (_, e) => Finished(systemStopped, e, "Computerton");
        clock.Start();
        try { loopback.StartRecording(); microphone.StartRecording(); }
        catch { Dispose(); throw; }
    }
    private void Write(TimelineWaveWriter writer, WaveInEventArgs e)
    {
        try { writer.Append(e.Buffer, e.BytesRecorded, clock.Elapsed); }
        catch (Exception ex) { CaptureFailed?.Invoke(this, ex.Message); }
    }
    private void Finished(TaskCompletionSource completion, StoppedEventArgs e, string source)
    {
        if (e.Exception is null) completion.TrySetResult(); else completion.TrySetException(e.Exception);
        if (!stopping) CaptureFailed?.Invoke(this, source + " wurde unterbrochen. " + e.Exception?.Message);
    }
    public async Task<IReadOnlyList<CapturedTrack>> StopAsync()
    {
        if (disposed) throw new InvalidOperationException("Die Aufnahme ist bereits geschlossen.");
        stopping = true;
        var duration = clock.Elapsed;
        microphone?.StopRecording();
        loopback?.StopRecording();
        await Task.WhenAll(micStopped.Task, systemStopped.Task).WaitAsync(TimeSpan.FromSeconds(15));
        clock.Stop();
        micWriter!.Finish(duration);
        systemWriter!.Finish(duration);
        return [new("Du", micWriter.Path), new("Andere Teilnehmer", systemWriter.Path)];
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        stopping = true;
        try { microphone?.StopRecording(); } catch { }
        try { loopback?.StopRecording(); } catch { }
        microphone?.Dispose();
        loopback?.Dispose();
        micWriter?.Dispose();
        systemWriter?.Dispose();
        renderDevice?.Dispose();
    }
}
