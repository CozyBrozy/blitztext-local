using Blitztext.Core.Models;
using Blitztext.Core.Settings;

namespace Blitztext.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public async Task SaveAndLoad_PreservesExplicitLocalQualityModel()
    {
        var path = Path.Combine(Path.GetTempPath(), $"blitztext-quality-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(path);
            await store.SaveAsync(new AppSettings { LocalWhisperModelFile = "ggml-large-v3.bin" });
            Assert.Equal("ggml-large-v3.bin", (await store.LoadAsync()).LocalWhisperModelFile);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task LoadAsync_WhenFileMissing_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"blitztext-settings-{Guid.NewGuid():N}.json");
        var store = new SettingsStore(path);

        var settings = await store.LoadAsync();

        Assert.Equal("de", settings.Language);
        Assert.Equal("gpt-4o-mini-transcribe", settings.TranscriptionModel);
        Assert.Equal("Ctrl+Shift+Space", settings.WorkflowHotkeys[WorkflowType.Transcription]);
        Assert.Empty(settings.CustomTerms);
    }

    [Fact]
    public async Task LoadAsync_WhenDirectoryMissing_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"blitztext-settings-{Guid.NewGuid():N}", "settings.json");
        Assert.Equal("", (await new SettingsStore(path).LoadAsync()).LocalWhisperModelFile);
    }

    [Fact]
    public async Task LoadAsync_WhenPathIsDirectory_DoesNotSilentlyResetModelSelection()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"blitztext-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new SettingsStore(directory).LoadAsync());
        }
        finally { Directory.Delete(directory); }
    }

    [Fact]
    public async Task LoadAsync_WhenFileIsLocked_DoesNotReturnDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"blitztext-settings-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, "{\"LocalWhisperModelFile\":\"ggml-large-v3.bin\"}");
            using var locked = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            await Assert.ThrowsAsync<IOException>(() => new SettingsStore(path).LoadAsync());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task SaveAsync_WhenCanceled_PreservesPreviousSettingsAndLeavesNoTemporaryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"blitztext-settings-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new SettingsStore(path);
            await store.SaveAsync(new AppSettings { LocalWhisperModelFile = "ggml-large-v3.bin" });
            var original = await File.ReadAllBytesAsync(path);
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(new AppSettings(), canceled.Token));

            Assert.Equal(original, await File.ReadAllBytesAsync(path));
            Assert.Equal("ggml-large-v3.bin", (await store.LoadAsync()).LocalWhisperModelFile);
            Assert.Equal(new[] { path }, Directory.GetFiles(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task SaveAsync_WhenReplacementIsBlocked_PreservesPreviousFileAndCleansTemporaryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"blitztext-settings-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new SettingsStore(path);
            await store.SaveAsync(new AppSettings { LocalWhisperModelFile = "ggml-large-v3.bin" });
            var original = await File.ReadAllBytesAsync(path);
            using (var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var error = await Record.ExceptionAsync(() => store.SaveAsync(new AppSettings()));
                Assert.True(error is IOException or UnauthorizedAccessException,
                    "Windows must reject replacement of the locked settings file.");
            }

            Assert.Equal(original, await File.ReadAllBytesAsync(path));
            Assert.Equal(new[] { path }, Directory.GetFiles(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task SaveAsync_DoesNotPersistApiKeyField()
    {
        var path = Path.Combine(Path.GetTempPath(), $"blitztext-settings-{Guid.NewGuid():N}.json");
        var store = new SettingsStore(path);
        var settings = new AppSettings();
        settings.CustomTerms.Add("Contoso");

        await store.SaveAsync(settings);

        var json = await File.ReadAllTextAsync(path);
        Assert.Contains("Contoso", json);
        Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("server", json, StringComparison.OrdinalIgnoreCase);
    }
}
