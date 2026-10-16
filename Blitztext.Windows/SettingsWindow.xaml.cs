using System.IO;
using System.Windows;
using Blitztext.Core.Models;
using Blitztext.Core.Settings;
using Blitztext.Windows.Platform;
namespace Blitztext.Windows;
public partial class SettingsWindow : Window
{
    private readonly AppSettings settings;
    private readonly SettingsStore store;
    public event EventHandler? SettingsSaved;
    public event EventHandler? ConversationRequested;
    public SettingsWindow(AppSettings settings, SettingsStore store)
    {
        InitializeComponent();
        this.settings = settings;
        this.store = store;
        LanguageBox.Text = settings.Language;
        HotkeyBox.Text = settings.WorkflowHotkeys[WorkflowType.Transcription];
        ModeBox.SelectedIndex = settings.HotkeyMode == HotkeyMode.Hold ? 1 : 0;
        AutoPasteBox.IsChecked = settings.AutoPaste;
        ModelStatusText.Text = File.Exists(App.ModelPath)
            ? "Installiert. Lokales Diktieren ist bereit."
            : "Modell fehlt: " + App.ModelPath;
        ReloadMicrophones();
        OutputDeviceBox.ItemsSource = ConversationRecorder.Outputs();
        OutputDeviceBox.SelectedValue = settings.ConversationOutputDeviceId;
    }
    private async void Conversation_Click(object sender, RoutedEventArgs e)
    {
        settings.ConversationOutputDeviceId = OutputDeviceBox.SelectedValue as string ?? "";
        settings.AudioInputDeviceNumber = MicrophoneBox.SelectedValue is int device ? device : 0;
        settings.Language = string.IsNullOrWhiteSpace(LanguageBox.Text) ? "auto" : LanguageBox.Text.Trim();
        try
        {
            await store.SaveAsync(settings);
            ConversationRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }
    private void Reload_Click(object sender, RoutedEventArgs e) => ReloadMicrophones();
    private void ReloadMicrophones()
    {
        var devices = AudioRecorder.AvailableDevices();
        MicrophoneBox.ItemsSource = devices;
        MicrophoneBox.SelectedValue = devices.Any(d => d.Index == settings.AudioInputDeviceNumber)
            ? settings.AudioInputDeviceNumber : devices.FirstOrDefault()?.Index;
        if (devices.Count == 0) StatusText.Text = "Kein Mikrofon gefunden.";
    }
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!HotkeyParser.TryParse(HotkeyBox.Text.Trim(), out _))
        { StatusText.Text = "Ungültige Tastenkombination."; return; }
        settings.ConversationOutputDeviceId = OutputDeviceBox.SelectedValue as string ?? "";
        settings.Language = string.IsNullOrWhiteSpace(LanguageBox.Text) ? "auto" : LanguageBox.Text.Trim();
        settings.WorkflowHotkeys[WorkflowType.Transcription] = HotkeyBox.Text.Trim();
        settings.HotkeyMode = ModeBox.SelectedIndex == 1 ? HotkeyMode.Hold : HotkeyMode.Toggle;
        settings.AudioInputDeviceNumber = MicrophoneBox.SelectedValue is int device ? device : 0;
        settings.AutoPaste = AutoPasteBox.IsChecked == true;
        try
        {
            await store.SaveAsync(settings);
            SettingsSaved?.Invoke(this, EventArgs.Empty);
            StatusText.Text = "Gespeichert. Fenster schließen und in der gewünschten App diktieren.";
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }
}
