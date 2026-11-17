namespace Blitztext.Core.Conversation;

public sealed class ConversationTranscriptFile(string path, string header)
{
    private readonly List<ConversationEntry> entries = [];
    public string Path { get; private set; } = path;
    public string CurrentStatus { get; set; } = "Aufnahme läuft";
    public string Text => ConversationFormatter.Format(entries);
    public void Rename(string name)
    {
        name = name.Trim();
        if (name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) name = name[..^3];
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120 ||
            name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 ||
            name.EndsWith('.') || name.EndsWith(' ') ||
            System.Text.RegularExpressions.Regex.IsMatch(name,
                @"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])($|\.)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new ArgumentException("Bitte einen gültigen Namen mit höchstens 120 Zeichen eingeben.");

        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!;
        for (var number = 1; ; number++)
        {
            var fileName = number == 1 ? name + ".md" : $"{name} ({number}).md";
            var destination = System.IO.Path.Combine(directory, fileName);
            if (string.Equals(System.IO.Path.GetFullPath(Path), destination, StringComparison.OrdinalIgnoreCase))
                return;
            try
            {
                File.Move(Path, destination);
                Path = destination;
                return;
            }
            catch (IOException) when (File.Exists(destination) || Directory.Exists(destination)) { }
        }
    }

    public async Task SaveAsync(IEnumerable<ConversationEntry> added, string? state = null)
    {
        entries.AddRange(added);
        var temporary = Path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, header + "\nStatus: " + (state ?? CurrentStatus) + "\n\n" + Text);
            File.Move(temporary, Path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
