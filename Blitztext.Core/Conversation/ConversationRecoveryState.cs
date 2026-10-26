namespace Blitztext.Core.Conversation;

// Stored only beside the user's recovery audio, never in the source repository.
public sealed class ConversationRecoveryState
{
    public string InputFingerprint { get; set; } = "";
    public string ModelFingerprint { get; set; } = "";
    public HashSet<int> CompletedTracks { get; set; } = [];
    public List<ConversationEntry> Entries { get; set; } = [];

    public bool CompleteTrack(int index, IEnumerable<ConversationEntry> entries)
    {
        if (!CompletedTracks.Add(index)) return false;
        Entries.AddRange(entries);
        return true;
    }

    public void Validate(string inputs, string model)
    {
        if (InputFingerprint != inputs || ModelFingerprint != model)
            throw new InvalidDataException("Die Audiodateien oder das Modell unterscheiden sich vom Wiederaufnahmestand.");
    }
}
