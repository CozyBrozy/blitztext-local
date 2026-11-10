using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Blitztext.Core.Transcription;

namespace Blitztext.Core.Settings;

public sealed class LocalStartupRecovery(
    string reserveDirectory,
    IReadOnlyDictionary<string, (long Length, string Sha256)>? verifiedModels = null)
{
    private const string FullModelName = "ggml-large-v3.bin";

    private static readonly IReadOnlyDictionary<string, (long Length, string Sha256)> DefaultModels =
        new Dictionary<string, (long Length, string Sha256)>(StringComparer.Ordinal)
        {
            ["ggml-base.bin"] = (147951465, "60ED5BC3DD14EEA856493D334349B405782DDCAF0028D4B5DF4088345FBA2EFE"),
            ["ggml-small.bin"] = (487601967, "1BE3A9B2063867B937E64E2EC7483364A79917E157FA98C5D94B5C1FFFEA987B"),
            [FullModelName] = (3095033483, "64D182B440B98D5203C4F9BD541544D84C605196C4F7B845DFA11FB23594D1E2")
        };

    public async Task<(AppSettings Settings, bool Restored)> LoadSettingsAsync(SettingsStore store)
    {
        using var current = TryOpenRead(store.SettingsPath);
        var settings = current is null ? null : await store.LoadAsync();
        if (!string.IsNullOrWhiteSpace(settings?.LocalWhisperModelFile)) return (settings!, false);

        var backupPath = Path.Combine(reserveDirectory, "settings.json");
        using var backup = TryOpenRead(backupPath);
        if (backup is null)
        {
            if (settings is not null) return (settings, false);
            if (!Directory.Exists(reserveDirectory)) return (await store.LoadAsync(), false);
            throw new FileNotFoundException("Gesicherte Blitztext-Einstellungen fehlen: " + backupPath, backupPath);
        }

        var backupSettings = await ReadBackupSettingsAsync(backup, backupPath);
        if (settings is null)
        {
            backup.Position = 0;
            await CopyAtomicallyAsync(backup, store.SettingsPath, overwrite: false);
            return (backupSettings, true);
        }

        if (!string.Equals(backupSettings.LocalWhisperModelFile?.Trim(), FullModelName, StringComparison.Ordinal))
            return (settings, false);

        var json = await JsonNode.ParseAsync(current!) as JsonObject
            ?? throw new InvalidDataException("Blitztext-Einstellungen enthalten kein JSON-Objekt: " + store.SettingsPath);
        current!.Dispose();
        settings.LocalWhisperModelFile = FullModelName;
        settings.LocalWhisperRequireCuda = backupSettings.LocalWhisperRequireCuda;
        json[nameof(AppSettings.LocalWhisperModelFile)] = settings.LocalWhisperModelFile;
        json[nameof(AppSettings.LocalWhisperRequireCuda)] = settings.LocalWhisperRequireCuda;
        using var updated = new MemoryStream(Encoding.UTF8.GetBytes(json.ToJsonString(new JsonSerializerOptions { WriteIndented = true })));
        await CopyAtomicallyAsync(updated, store.SettingsPath, overwrite: true);
        return (settings, true);
    }

    private static async Task<AppSettings> ReadBackupSettingsAsync(FileStream backup, string path)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(backup);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Gesicherte Blitztext-Einstellungen enthalten kein JSON-Objekt: " + path);
            return await new SettingsStore(path).LoadAsync();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Gesicherte Blitztext-Einstellungen sind ungültig: " + path, ex);
        }
    }


    public async Task<bool> RestoreOrValidateModelAsync(string modelPath)
    {
        var modelName = Path.GetFileName(modelPath);
        var models = verifiedModels ?? DefaultModels;
        var canRestore = models.TryGetValue(modelName, out var expected) && Directory.Exists(reserveDirectory);
        try
        {
            LocalWhisperModels.ValidateModel(modelPath);
            if (canRestore && new FileInfo(modelPath).Length != expected.Length)
                throw new InvalidDataException("Lokales Whisper-Modell hat eine unerwartete Dateigröße: " + modelPath);
            return false;
        }
        catch (Exception ex) when (canRestore && (ex is FileNotFoundException or DirectoryNotFoundException or InvalidDataException))
        {
            var backupPath = Path.Combine(reserveDirectory, modelName);
            using var backup = TryOpenRead(backupPath)
                ?? throw new FileNotFoundException("Lokales Whisper-Modell fehlt oder ist beschädigt. Keine Sicherung gefunden: " + backupPath, backupPath, ex);
            if (backup.Length != expected.Length ||
                !string.Equals(Convert.ToHexString(await SHA256.HashDataAsync(backup)), expected.Sha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Gesichertes Whisper-Modell stimmt nicht mit der Prüfsumme überein: " + backupPath);

            backup.Position = 0;
            await CopyAtomicallyAsync(backup, modelPath, overwrite: true);
            LocalWhisperModels.ValidateModel(modelPath);
            return true;
        }
    }

    private static FileStream? TryOpenRead(string path)
    {
        try { return File.OpenRead(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static async Task CopyAtomicallyAsync(Stream source, string destination, bool overwrite)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(destination))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".restore");
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(output);
                await output.FlushAsync();
            }

            File.Move(temporary, destination, overwrite);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
