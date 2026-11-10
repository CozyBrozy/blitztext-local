using Blitztext.Core.Settings;
using Blitztext.Core.Transcription;

namespace Blitztext.Tests;

public sealed class LocalWhisperModelsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "blitztext-model-test-" + Guid.NewGuid().ToString("N"));

    public LocalWhisperModelsTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void Resolve_DefaultsRemainSmallAndBase()
    {
        var models = LocalWhisperModels.Resolve(new AppSettings(), directory);
        Assert.Equal(Path.Combine(directory, "ggml-small.bin"), models.DictationPath);
        Assert.Equal(Path.Combine(directory, "ggml-base.bin"), models.ConversationPath);
    }

    [Fact]
    public async Task Resolve_UsesSavedQualitySelectionForBothModesAndKeepsSnapshot()
    {
        var store = new SettingsStore(Path.Combine(directory, "settings.json"));
        await store.SaveAsync(new AppSettings { LocalWhisperModelFile = "ggml-large-v3.bin" });
        var settings = await store.LoadAsync();
        var models = LocalWhisperModels.Resolve(settings, directory);
        settings.LocalWhisperModelFile = "ggml-base.bin";

        Assert.Equal(Path.Combine(directory, "ggml-large-v3.bin"), models.DictationPath);
        Assert.Equal(models.DictationPath, models.ConversationPath);
    }

    [Theory]
    [InlineData("../ggml-base.bin")]
    [InlineData("unknown.bin")]
    public void Resolve_RejectsUnsupportedOrEscapingSelection(string selected)
    {
        Assert.Throws<InvalidOperationException>(() => LocalWhisperModels.Resolve(
            new AppSettings { LocalWhisperModelFile = selected }, directory));
    }

    [Fact]
    public void ValidateModel_ReadsExistingGgmlFile()
    {
        var path = WriteModel("ggml-large-v3.bin", [0x6c, 0x6d, 0x67, 0x67, 0x01]);
        LocalWhisperModels.ValidateModel(path);
    }

    [Fact]
    public void ValidateModel_DoesNotDowngradeWhenSelectedModelIsMissing()
    {
        WriteModel("ggml-base.bin", [0x6c, 0x6d, 0x67, 0x67]);
        var models = LocalWhisperModels.Resolve(new AppSettings { LocalWhisperModelFile = "ggml-large-v3.bin" }, directory);
        var error = Assert.Throws<FileNotFoundException>(() => LocalWhisperModels.ValidateModel(models.ConversationPath));
        Assert.Equal(models.ConversationPath, error.FileName);
        Assert.Contains("ggml-large-v3.bin", error.Message);
    }

    [Fact]
    public void ValidateModel_DistinguishesMissingDirectory()
    {
        var path = Path.Combine(directory, "absent", "ggml-base.bin");
        var error = Assert.Throws<FileNotFoundException>(() => LocalWhisperModels.ValidateModel(path));
        Assert.Equal(path, error.FileName);
    }

    [Theory]
    [InlineData(new byte[] { 0x6c, 0x6d })]
    [InlineData(new byte[] { 0x48, 0x54, 0x4d, 0x4c })]
    public void ValidateModel_ReportsTruncatedOrWrongModelAsInvalid(byte[] content)
    {
        var path = WriteModel("ggml-base.bin", content);
        var error = Assert.Throws<InvalidDataException>(() => LocalWhisperModels.ValidateModel(path));
        Assert.Contains(path, error.Message);
        Assert.DoesNotContain("fehlt", error.Message);
    }

    [Fact]
    public void ValidateModel_ReportsDeniedAccessSeparatelyFromMissingFile()
    {
        var error = Assert.Throws<UnauthorizedAccessException>(() => LocalWhisperModels.ValidateModel(directory));
        Assert.Contains("Kein Lesezugriff", error.Message);
        Assert.DoesNotContain("fehlt", error.Message);
    }

    [Fact]
    public void ValidateModel_ReportsLockedFileSeparatelyFromMissingFile()
    {
        var path = WriteModel("ggml-base.bin", [0x6c, 0x6d, 0x67, 0x67]);
        using var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var error = Assert.Throws<IOException>(() => LocalWhisperModels.ValidateModel(path));
        Assert.Contains("kann nicht gelesen werden", error.Message);
        Assert.DoesNotContain("fehlt", error.Message);
    }

    private string WriteModel(string name, byte[] content)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    public void Dispose() => Directory.Delete(directory, true);
}
