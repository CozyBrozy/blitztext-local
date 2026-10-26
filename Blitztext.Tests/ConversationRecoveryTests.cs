using System.Text.Json;
using Blitztext.Core.Conversation;

namespace Blitztext.Tests;

public sealed class ConversationRecoveryTests
{
    [Fact]
    public async Task TranscriptShowsProcessingStatusWithoutLosingEarlierText()
    {
        var path = Path.Combine(Path.GetTempPath(), $"blitztext-state-{Guid.NewGuid():N}.md");
        try
        {
            var file = new ConversationTranscriptFile(path, "# Test");
            await file.SaveAsync([new("Du", TimeSpan.Zero, TimeSpan.FromSeconds(2), "Bleibt erhalten")]);
            file.CurrentStatus = "Verarbeitung läuft";
            await file.SaveAsync([]);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("Status: Verarbeitung läuft", text);
            Assert.Contains("Bleibt erhalten", text);
            await file.SaveAsync([], "Beendet");
            Assert.Contains("Status: Beendet", await File.ReadAllTextAsync(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RestartPreservesCompletedSegmentsAndDoesNotDuplicateThem()
    {
        var state = new ConversationRecoveryState { InputFingerprint = "audio", ModelFingerprint = "model" };
        var entry = new ConversationEntry("Du", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), "Test");
        Assert.True(state.CompleteTrack(0, [entry]));
        var resumed = JsonSerializer.Deserialize<ConversationRecoveryState>(JsonSerializer.Serialize(state))!;
        resumed.Validate("audio", "model");
        Assert.False(resumed.CompleteTrack(0, [entry]));
        Assert.Single(resumed.Entries);
        Assert.Equal(TimeSpan.FromSeconds(3), resumed.Entries[0].Start);
        Assert.True(resumed.CompleteTrack(1, [entry with { Source = "Andere Teilnehmer" }]));
        Assert.Equal(2, resumed.Entries.Count);
    }

    [Theory]
    [InlineData("changed audio", "model")]
    [InlineData("audio", "changed model")]
    public void ChangedSourceOrModelCannotResumeAnExistingCheckpoint(string audio, string model)
    {
        var state = new ConversationRecoveryState { InputFingerprint = "audio", ModelFingerprint = "model" };
        Assert.Throws<InvalidDataException>(() => state.Validate(audio, model));
    }
}
