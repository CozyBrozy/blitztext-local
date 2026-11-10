using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Blitztext.Core.Conversation;
using Blitztext.Core.Settings;
using Blitztext.Core.Transcription;
using Blitztext.Windows.Platform;
namespace Blitztext.Windows;

public partial class ConversationWindow : Window
{
    private readonly AppSettings settings;
    private readonly ConversationProcessor processor;
    private readonly Func<bool> acquire;
    private readonly Action release;
    private ConversationRecorder? recorder;
    private LiveConversationWorker? worker;
    private ConversationTranscriptFile? transcript;
    private CancellationTokenSource? stopPolling;
    private Task? liveTask;
    private bool busy;
    private bool processingOnly;
    private DateTimeOffset started;
    private string? captureFailure;
    private string? savedTranscriptPath;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    public static string OutputFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BlitztextLocal", "conversations");
    public ConversationWindow(AppSettings settings, ConversationProcessor processor, Func<bool> acquire, Action release)
    {
        InitializeComponent();
        this.settings = settings;
        this.processor = processor;
        this.acquire = acquire;
        this.release = release;
        timer.Tick += (_, _) => StatusText.Text = processingOnly
            ? $"Aufnahme beendet. Transkript wird fertiggestellt. Rückstand ca. {worker?.Backlog.TotalSeconds ?? 0:0} s. Bitte bis Fertig warten."
            : $"Aufnahme läuft: {(DateTimeOffset.Now - started):hh\\:mm\\:ss}. Rückstand ca. {worker?.Backlog.TotalSeconds ?? 0:0} s.";
        Closing += (_, e) =>
        {
            if (!busy) return;
            e.Cancel = true;
            StatusText.Text = "Bitte erst stoppen und die letzten Abschnitte abwarten. Minimieren ist möglich.";
        };
        var mic = AudioRecorder.AvailableDevices().FirstOrDefault(d => d.Index == settings.AudioInputDeviceNumber)?.Name;
        var output = ConversationRecorder.Outputs().FirstOrDefault(d => d.Id == settings.ConversationOutputDeviceId)?.Name;
        SetDevices(mic ?? "Nicht verfügbar", output ?? "Nicht verfügbar");
        if (App.CudaStartupError is { } cudaError) StatusText.Text = cudaError;
    }
    private async void Record_Click(object sender, RoutedEventArgs e)
    {
        if (!RecordButton.IsEnabled) return;
        RecordButton.IsEnabled = false;
        try
        {
            if (recorder is null)
            {
                if (busy) return;
                if (App.CudaStartupError is { } cudaError)
                {
                    StatusText.Text = cudaError;
                    return;
                }
                try { LocalWhisperModels.ValidateModel(App.ConversationModelPath); }
                catch (Exception ex)
                {
                    AppLog.Write("Conversation model preflight failed: " + ex);
                    StatusText.Text = ex.Message;
                    return;
                }
                if (!acquire()) { StatusText.Text = "Eine andere Aufnahme oder Verarbeitung läuft bereits."; return; }
                busy = true;
                processingOnly = false;
                captureFailure = null;
                TranscriptBox.Clear();
                SavedText.Text = "";
                SetTranscriptLink(null);
                try
                {
                    recorder = new ConversationRecorder();
                    recorder.CaptureFailed += (_, error) => Dispatcher.BeginInvoke(new Action(() => FailAndStop(error)));
                    started = DateTimeOffset.Now;
                    recorder.Start(settings.AudioInputDeviceNumber, settings.ConversationOutputDeviceId);
                    Directory.CreateDirectory(OutputFolder);
                    var file = Path.Combine(OutputFolder, $"Gespräch-{started:yyyy-MM-dd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}.md");
                    var header = $"# Gespräch {started:dd.MM.yyyy HH:mm:ss zzz}\n\nDu: Mikrofon ({recorder.MicrophoneName})\nAndere Teilnehmer: Computerton ({recorder.OutputName})\nZeitstempel ab Aufnahmebeginn, näherungsweise. Keine individuelle Sprechererkennung.\n";
                    transcript = new ConversationTranscriptFile(file, header);
                    await transcript.SaveAsync([]);
                    SetDevices(recorder.MicrophoneName, recorder.OutputName);
                    var detector = Path.Combine(AppContext.BaseDirectory, "ggml-silero-v5.1.2.bin");
                    worker = new LiveConversationWorker(recorder, processor, transcript, settings.Language,
                        File.Exists(detector) ? detector : null);
                    worker.Updated += text => Dispatcher.InvokeAsync(() =>
                    {
                        TranscriptBox.Text = text;
                        TranscriptBox.ScrollToEnd();
                    });
                    stopPolling = new CancellationTokenSource();
                    var activeWorker = worker;
                    var pollingToken = stopPolling.Token;
                    liveTask = Task.Run(async () =>
                    {
                        try
                        {
                            while (true)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(2), pollingToken);
                                await activeWorker.ProcessAsync(false);
                            }
                        }
                        catch (OperationCanceledException) when (pollingToken.IsCancellationRequested) { }
                        catch (Exception ex)
                        {
                            _ = Dispatcher.BeginInvoke(new Action(() => FailAndStop("Live-Verarbeitung: " + ex.Message)));
                            throw;
                        }
                    });
                    RecordButton.Content = "Gespräch stoppen";
                    StatusText.Text = "Aufnahme läuft";
                    timer.Start();
                }
                catch (Exception ex)
                {
                    await EndFailedStartAsync(ex);
                }
                return;
            }
            await StopAsync();
        }
        finally
        {
            RecordButton.IsEnabled = true;
            if (captureFailure is not null && recorder is not null)
                _ = Dispatcher.BeginInvoke(new Action(() => Record_Click(this, new RoutedEventArgs())));
        }
    }
    private void FailAndStop(string error)
    {
        captureFailure ??= error;
        if (recorder is not null && RecordButton.IsEnabled) Record_Click(this, new RoutedEventArgs());
    }
    private async Task EndFailedStartAsync(Exception error)
    {
        timer.Stop();
        stopPolling?.Cancel();
        if (liveTask is not null) { try { await liveTask; } catch { } }
        var failed = recorder;
        recorder = null;
        if (failed is not null)
        {
            try { await failed.StopAsync(); } catch { }
            failed.Dispose();
            Cleanup(failed.DirectoryPath);
        }
        busy = false;
        release();
        StatusText.Text = "Aufnahme konnte nicht starten: " + error.Message;
    }
    private async Task StopAsync()
    {
        processingOnly = true;
        var finished = recorder!;
        var completed = false;
        if (transcript is not null) transcript.CurrentStatus = "Verarbeitung läuft";
        stopPolling?.Cancel();
        try
        {
            StatusText.Text = "Aufnahme wird beendet. Die letzten Textabschnitte werden gespeichert ...";
            await finished.StopAsync();
            if (liveTask is not null) await liveTask;
            if (captureFailure is not null) throw new IOException(captureFailure);
            if (worker is not null) await Task.Run(() => worker.FinishAsync());
            completed = true;
            StatusText.Text = "Fertig. Transkript gespeichert.";
            SetTranscriptLink(transcript?.Path);
        }
        catch (Exception ex)
        {
            // Keep everything already saved. Never delete a transcript on failure.
            if (liveTask is not null) { try { await liveTask; } catch { } }
            try { if (transcript is not null) await transcript.SaveAsync([], "Unterbrochen: " + ex.Message); }
            catch { }
            StatusText.Text = "Gespräch unterbrochen: " + ex.Message + ". Bereits gespeicherter Text bleibt erhalten.";
            SavedText.Text = "Audio zur Wiederherstellung bleibt erhalten: " + finished.DirectoryPath;
        }
        finally
        {
            recorder = null;
            timer.Stop();
            finished.Dispose();
            if (completed) Cleanup(finished.DirectoryPath);
            stopPolling?.Dispose();
            stopPolling = null;
            liveTask = null;
            busy = false;
            release();
            RecordButton.Content = "Gespräch starten";
        }
    }
    private static void Cleanup(string directory)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
        catch { }
    }
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TranscriptBox.Text)) System.Windows.Clipboard.SetText(TranscriptBox.Text);
    }
    private void SetDevices(string microphone, string output) =>
        DevicesText.Text = $"Mikrofon: {microphone}{Environment.NewLine}Computerton: {output}";

    private void SetTranscriptLink(string? path)
    {
        savedTranscriptPath = path;
        TranscriptName.Text = path is null ? "" : Path.GetFileName(path);
        TranscriptLinks.ToolTip = path;
        TranscriptLinks.Visibility = path is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OpenTranscript_Click(object sender, RoutedEventArgs e) => OpenSavedTranscript(reveal: false);
    private void RevealTranscript_Click(object sender, RoutedEventArgs e) => OpenSavedTranscript(reveal: true);

    private void OpenSavedTranscript(bool reveal)
    {
        if (savedTranscriptPath is not { } path || !File.Exists(path))
        {
            StatusText.Text = "Die Transkriptdatei ist nicht mehr vorhanden.";
            return;
        }

        try
        {
            Process.Start(reveal
                ? new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }
                : new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { StatusText.Text = "Transkript konnte nicht geöffnet werden: " + ex.Message; }
    }

    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(OutputFolder);
        Process.Start(new ProcessStartInfo(OutputFolder) { UseShellExecute = true });
    }
}
