using System.Security.Cryptography;
using Blitztext.Core.Settings;
using Blitztext.Core.Transcription;
using Xunit;

namespace Blitztext.Tests;

public sealed class LocalStartupRecoveryTests : IDisposable
{
    private const string ModelName = "ggml-large-v3.bin";
    private static readonly byte[] VerifiedModel = "lmgg-fixture"u8.ToArray();
    private readonly string root = Path.Combine(Path.GetTempPath(), "BlitztextStartupTest-" + Guid.NewGuid().ToString("N"));
    private string Reserve => Path.Combine(root, "reserve");
    private string SettingsPath => Path.Combine(root, "data", "settings.json");
    private string Models => Path.Combine(root, "data", "models");

    [Fact]
    public async Task MissingSettingsAndModelRestoreTheExplicitSelectionForBothModes()
    {
        Directory.CreateDirectory(Reserve);
        var backupSettings = Path.Combine(Reserve, "settings.json");
        await new SettingsStore(backupSettings).SaveAsync(new AppSettings
        {
            LocalWhisperModelFile = ModelName,
            LocalWhisperRequireCuda = true,
            Language = "de"
        });
        await File.WriteAllBytesAsync(Path.Combine(Reserve, ModelName), VerifiedModel);
        var recovery = CreateRecovery();

        var store = new SettingsStore(SettingsPath);
        var (settings, restored) = await recovery.LoadSettingsAsync(store);
        Assert.True(restored);
        var selection = LocalWhisperModels.Resolve(settings, Models);
        Assert.True(settings.LocalWhisperRequireCuda);
        Assert.Equal(Path.Combine(Models, ModelName), selection.DictationPath);
        Assert.Equal(selection.DictationPath, selection.ConversationPath);
        Assert.True(await recovery.RestoreOrValidateModelAsync(selection.ConversationPath));
        Assert.Equal(await File.ReadAllBytesAsync(backupSettings), await File.ReadAllBytesAsync(SettingsPath));
        Assert.Equal(VerifiedModel, await File.ReadAllBytesAsync(selection.ConversationPath));
        Assert.False((await recovery.LoadSettingsAsync(new SettingsStore(SettingsPath))).Restored);
        Assert.False(await recovery.RestoreOrValidateModelAsync(selection.ConversationPath));
    }

    [Fact]
    public async Task WrongReserveChecksumLeavesAnExistingDamagedModelUntouched()
    {
        Directory.CreateDirectory(Reserve);
        Directory.CreateDirectory(Models);
        var damagedModel = "lmgg"u8.ToArray();
        var modelPath = Path.Combine(Models, ModelName);
        await File.WriteAllBytesAsync(modelPath, damagedModel);
        await File.WriteAllBytesAsync(Path.Combine(Reserve, ModelName), "lmgg-altered"u8.ToArray());

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateRecovery().RestoreOrValidateModelAsync(modelPath));

        Assert.Equal(damagedModel, await File.ReadAllBytesAsync(modelPath));
        Assert.Empty(Directory.GetFiles(Models, "*.restore"));
    }

    [Fact]
    public async Task AReserveWithMissingSettingsDoesNotFallBackToDefaults()
    {
        Directory.CreateDirectory(Reserve);
        var error = await Assert.ThrowsAsync<FileNotFoundException>(
            () => CreateRecovery().LoadSettingsAsync(new SettingsStore(SettingsPath)));
        Assert.Equal(Path.Combine(Reserve, "settings.json"), error.FileName);
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public async Task FreshInstallWithoutAReserveKeepsExistingStartupBehavior()
    {
        var recovery = CreateRecovery();
        Assert.False((await recovery.LoadSettingsAsync(new SettingsStore(SettingsPath))).Restored);

        var selection = LocalWhisperModels.Resolve(await new SettingsStore(SettingsPath).LoadAsync(), Models);
        Assert.Equal("ggml-small.bin", Path.GetFileName(selection.DictationPath));
        Assert.Equal("ggml-base.bin", Path.GetFileName(selection.ConversationPath));
        var error = await Assert.ThrowsAsync<FileNotFoundException>(
            () => recovery.RestoreOrValidateModelAsync(selection.ConversationPath));
        Assert.Equal(selection.ConversationPath, error.FileName);
    }

    [Theory]
    [InlineData("", true, ModelName)]
    [InlineData("ggml-small.bin", false, "ggml-small.bin")]
    public async Task ReserveModelOnlyRepairsAnEmptyActiveSelection(string activeModel, bool restored, string expectedModel)
    {
        Directory.CreateDirectory(Reserve);
        await new SettingsStore(Path.Combine(Reserve, "settings.json")).SaveAsync(new AppSettings
        {
            LocalWhisperModelFile = ModelName,
            LocalWhisperRequireCuda = true,
            Language = "fr"
        });
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        await File.WriteAllTextAsync(SettingsPath, $$"""
            { "LocalWhisperModelFile": "{{activeModel}}", "LocalWhisperRequireCuda": false,
              "Language": "de", "UnknownProperty": { "value": 5 } }
            """);

        var (settings, changed) = await CreateRecovery().LoadSettingsAsync(new SettingsStore(SettingsPath));

        Assert.Equal(restored, changed);
        Assert.Equal(expectedModel, settings.LocalWhisperModelFile);
        Assert.Equal(restored, settings.LocalWhisperRequireCuda);
        Assert.Equal("de", settings.Language);
        using var saved = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(SettingsPath));
        Assert.Equal(5, saved.RootElement.GetProperty("UnknownProperty").GetProperty("value").GetInt32());
        Assert.Equal(expectedModel, saved.RootElement.GetProperty("LocalWhisperModelFile").GetString());
        Assert.Equal(restored, saved.RootElement.GetProperty("LocalWhisperRequireCuda").GetBoolean());
    }


    private LocalStartupRecovery CreateRecovery() =>
        new(Reserve, new Dictionary<string, (long, string)>
        {
            [ModelName] = (VerifiedModel.Length, Convert.ToHexString(SHA256.HashData(VerifiedModel)))
        });

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
