namespace Blitztext.Core.Conversation;

public sealed class ConversationTranscriptFile(string path, string header)
{
    private readonly List<ConversationEntry> entries = [];
    public string Path { get; } = path;
    public string CurrentStatus { get; set; } = "Aufnahme läuft";
    public string Text => ConversationFormatter.Format(entries);
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
