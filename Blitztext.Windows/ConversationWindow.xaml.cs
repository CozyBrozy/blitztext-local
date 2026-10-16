using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Blitztext.Core.Conversation;
using Blitztext.Core.Settings;
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
    private DateTimeOffset started;
    private string? captureFailure;
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
        timer.Tick += (_, _) => StatusText.Text =
            $"Aufnahme läuft: {(DateTimeOffset.Now - started):hh\\:mm\\:ss}. Live-Text folgt abschnittsweise, Rückstand ca. {worker?.Backlog.TotalSeconds ?? 0:0} s.";
        Closing += (_, e) =>
        {
            if (!busy) return;
            e.Cancel = true;
            StatusText.Text = "Bitte erst stoppen und die letzten Abschnitte abwarten. Minimieren ist möglich.";
        };
        DevicesText.Text = "Du = Mikrofon. Andere Teilnehmer = Computerton. Kopfhörer vermeiden doppelte Aufnahme.";
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
                if (!File.Exists(App.ConversationModelPath)) { StatusText.Text = "Das lokale Whisper-Base-Modell für Live-Gespräche fehlt."; return; }
                if (!acquire()) { StatusText.Text = "Eine andere Aufnahme oder Verarbeitung läuft bereits."; return; }
                busy = true;
                captureFailure = null;
                TranscriptBox.Clear();
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
                    SavedText.Text = "Wird fortlaufend gespeichert: " + file;
                    DevicesText.Text = "Du: " + recorder.MicrophoneName + ". Andere Teilnehmer: " + recorder.OutputName;
                    worker = new LiveConversationWorker(recorder, processor, transcript, settings.Language);
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
                    StatusText.Text = "Aufnahme läuft. Die ersten Textabschnitte erscheinen nach wenigen Sekunden.";
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
        timer.Stop();
        var finished = recorder!;
        stopPolling?.Cancel();
        try
        {
            StatusText.Text = "Aufnahme wird beendet. Die letzten Textabschnitte werden gespeichert ...";
            await finished.StopAsync();
            if (liveTask is not null) await liveTask;
            if (captureFailure is not null) throw new IOException(captureFailure);
            if (worker is not null) await Task.Run(() => worker.FinishAsync());
            StatusText.Text = "Fertig. Das vollständige Transkript ist lokal gespeichert.";
            SavedText.Text = "Lokal gespeichert: " + transcript?.Path;
        }
        catch (Exception ex)
        {
            // Keep everything already saved. Never delete a transcript on failure.
            if (liveTask is not null) { try { await liveTask; } catch { } }
            try { if (transcript is not null) await transcript.SaveAsync([], "Unterbrochen: " + ex.Message); }
            catch { }
            StatusText.Text = "Gespräch unterbrochen: " + ex.Message + ". Bereits gespeicherter Text bleibt erhalten.";
        }
        finally
        {
            recorder = null;
            finished.Dispose();
            Cleanup(finished.DirectoryPath);
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
    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(OutputFolder);
        Process.Start(new ProcessStartInfo(OutputFolder) { UseShellExecute = true });
    }
}
