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
    private static LocalWhisperModels? models;
    public static string ConversationModelPath => ActiveModels.ConversationPath;
    public static string ModelPath => ActiveModels.DictationPath;
    public static string? CudaStartupError { get; private set; }
    private static LocalWhisperModels ActiveModels => models
        ?? throw new InvalidOperationException("Die lokale Modellauswahl wird noch geladen. Bitte kurz warten.");
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppLog.Write($"Startup PID={Environment.ProcessId} app={AppContext.BaseDirectory}");
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
        var showWhenReady = false;
        showRegistration = ThreadPool.RegisterWaitForSingleObject(showEvent,
            (_, _) => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (trayController is null) showWhenReady = true;
                else trayController.ShowSettings();
            })), null, -1, false);
        this.instanceMutex = mutex;
        try
        {
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BlitztextLocal");
            var reserveDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "BlitztextLocalReserve");
            var recovery = new LocalStartupRecovery(reserveDirectory);
            var store = new SettingsStore(Path.Combine(dataDirectory, "settings.json"));
            var (settings, restoredSettings) = await recovery.LoadSettingsAsync(store);
            if (restoredSettings)
                AppLog.Write($"Restored settings PID={Environment.ProcessId} path={store.SettingsPath}");
            models = LocalWhisperModels.Resolve(settings, Path.Combine(dataDirectory, "models"));
            AppLog.Write($"Models PID={Environment.ProcessId} dictation={ModelPath} conversation={ConversationModelPath}");
            var sharedModel = string.Equals(ModelPath, ConversationModelPath, StringComparison.OrdinalIgnoreCase);
            if (await recovery.RestoreOrValidateModelAsync(ModelPath))
                AppLog.Write($"Restored model PID={Environment.ProcessId} path={ModelPath}");
            if (!sharedModel && await recovery.RestoreOrValidateModelAsync(ConversationModelPath))
                AppLog.Write($"Restored model PID={Environment.ProcessId} path={ConversationModelPath}");
            transcriber = new LocalWhisperTranscriber(ModelPath, settings.LocalWhisperRequireCuda);
            conversationTranscriber = sharedModel
                ? transcriber : new LocalWhisperTranscriber(ConversationModelPath, settings.LocalWhisperRequireCuda);
            if (settings.LocalWhisperRequireCuda)
            {
                try
                {
                    await transcriber.PreflightCudaAsync();
                    if (!sharedModel) await conversationTranscriber.PreflightCudaAsync();
                    AppLog.Write($"Backend PID={Environment.ProcessId} {LocalWhisperTranscriber.Backend}");
                }
                catch (Exception ex)
                {
                    CudaStartupError = ex.Message;
                    AppLog.Write($"CUDA preflight failed PID={Environment.ProcessId}: {ex}");
                }
            }
            var runner = new WorkflowRunner(transcriber, () => settings);
            trayController = new TrayController(settings, store, runner, new ConversationProcessor(conversationTranscriber));
            var background = e.Args.Contains("--background");
            trayController.Start(background);
            if (background && showWhenReady) trayController.ShowSettings();
        }
        catch (Exception ex)
        {
            AppLog.Write("Startup failed: " + ex);
            System.Windows.MessageBox.Show("Blitztext konnte nicht starten. " + ex.Message,
                "Blitztext Lokal", MessageBoxButton.OK, MessageBoxImage.Error);
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
