using Blitztext.Core.Settings;

namespace Blitztext.Core.Transcription;

public sealed class LocalWhisperModels
{
    public string DictationPath { get; }
    public string ConversationPath { get; }

    private LocalWhisperModels(string dictationPath, string conversationPath)
    {
        DictationPath = dictationPath;
        ConversationPath = conversationPath;
    }

    public static LocalWhisperModels Resolve(AppSettings settings, string modelsDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelsDirectory);
        var selected = settings.LocalWhisperModelFile?.Trim() ?? "";
        if (selected.Length > 0 && selected is not
            ("ggml-base.bin" or "ggml-small.bin" or "ggml-medium.bin" or "ggml-large-v3.bin" or "ggml-large-v3-turbo.bin"))
            throw new InvalidOperationException("Unbekannte lokale Whisper-Modellauswahl: " + selected);

        var directory = Path.GetFullPath(modelsDirectory);
        return new LocalWhisperModels(
            Path.Combine(directory, selected.Length == 0 ? "ggml-small.bin" : selected),
            Path.Combine(directory, selected.Length == 0 ? "ggml-base.bin" : selected));
    }

    public static void ValidateModel(string modelPath)
    {
        try
        {
            // Opening the file distinguishes an absent model from denied or locked access.
            using var stream = File.Open(modelPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<byte> header = stackalloc byte[4];
            if (stream.Length < header.Length)
                throw new InvalidDataException("Lokales Whisper-Modell ist unvollstaendig: " + modelPath);
            stream.ReadExactly(header);
            if (!header.SequenceEqual(new byte[] { 0x6c, 0x6d, 0x67, 0x67 }))
                throw new InvalidDataException("Lokales Whisper-Modell hat keinen gueltigen GGML-Dateikopf: " + modelPath);
        }
        catch (FileNotFoundException ex)
        {
            throw new FileNotFoundException("Lokales Whisper-Modell fehlt: " + modelPath, modelPath, ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new FileNotFoundException("Lokales Whisper-Modell fehlt: " + modelPath, modelPath, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException("Kein Lesezugriff auf das lokale Whisper-Modell: " + modelPath, ex);
        }
        catch (IOException ex)
        {
            throw new IOException("Lokales Whisper-Modell kann nicht gelesen werden: " + modelPath + ". " + ex.Message, ex);
        }
    }
}
