using System.IO;
using System.Windows;
using Blitztext.Core.Settings;
using Blitztext.Core.Transcription;
using Blitztext.Core.Workflow;
using Blitztext.Core.Conversation;
namespace Blitztext.Windows;
public partial class App : System.Windows.Application
{
    private TrayController? trayController;
    private LocalWhisperTranscriber? transcriber;
    private LocalWhisperTranscriber? conversationTranscriber;
    private static string selectedModelFile = "";
    public static string ConversationModelPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BlitztextLocal", "models", string.IsNullOrEmpty(selectedModelFile) ? "ggml-base.bin" : selectedModelFile);
    public static string ModelPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BlitztextLocal", "models", string.IsNullOrEmpty(selectedModelFile) ? "ggml-small.bin" : selectedModelFile);
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var mutex = new System.Threading.Mutex(true, "Local\\BlitztextLocal", out var created);
        if (!created)
        {
            try { using var existing = EventWaitHandle.OpenExisting("Local\\BlitztextLocalShow"); existing.Set(); }
            catch (WaitHandleCannotBeOpenedException) { }
            mutex.Dispose();
            Shutdown();
            return;
        }
        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\BlitztextLocalShow");
        showRegistration = ThreadPool.RegisterWaitForSingleObject(showEvent,
            (_, _) => Dispatcher.BeginInvoke(new Action(() => trayController?.ShowSettings())), null, -1, false);
        this.instanceMutex = mutex;
        try
        {
            var store = new SettingsStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BlitztextLocal", "settings.json"));
            var settings = await store.LoadAsync();
            selectedModelFile = settings.LocalWhisperModelFile?.Trim() ?? "";
            if (selectedModelFile.Length > 0 && selectedModelFile is not
                ("ggml-base.bin" or "ggml-small.bin" or "ggml-medium.bin" or "ggml-large-v3.bin" or "ggml-large-v3-turbo.bin"))
                throw new InvalidOperationException("Unbekannte lokale Whisper-Modellauswahl.");
            transcriber = new LocalWhisperTranscriber(ModelPath);
            var runner = new WorkflowRunner(transcriber, () => settings);
            conversationTranscriber = string.Equals(ModelPath, ConversationModelPath, StringComparison.OrdinalIgnoreCase)
                ? transcriber : new LocalWhisperTranscriber(ConversationModelPath);
            trayController = new TrayController(settings, store, runner, new ConversationProcessor(conversationTranscriber));
            trayController.Start(e.Args.Contains("--background"));
        }
        catch (Exception ex)
        {
            AppLog.Write("Startup failed: " + ex);
            if (!e.Args.Contains("--background")) System.Windows.MessageBox.Show(ex.Message, "Blitztext Lokal");
            Shutdown();
        }
    }
    private System.Threading.Mutex? instanceMutex;
    private EventWaitHandle? showEvent;
    private RegisteredWaitHandle? showRegistration;
    protected override void OnExit(ExitEventArgs e)
    {
        trayController?.Dispose();
        // Native inference can still be cancelling; process exit releases its resources.
        showRegistration?.Unregister(null);
        showEvent?.Dispose();
        instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
