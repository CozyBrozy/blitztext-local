using Blitztext.Core.Conversation;

namespace Blitztext.Tests;

public sealed class ConversationTurnTests
{
    [Fact]
    public void ConsecutiveSameSpeakerSegmentsBecomeOneTurnEvenAcrossPauses()
    {
        var text = ConversationFormatter.Format([
            new("Du", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "Hallo."),
            new("Du", TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(10), "Noch ein Satz.")]);
        Assert.Equal("[00:00:01 - 00:00:10] Du: Hallo. Noch ein Satz." + Environment.NewLine, text);
    }

    [Fact]
    public void InterruptionStartsNewTurnAndKeepsOverlappingTimestamps()
    {
        var text = ConversationFormatter.Format([
            new("Stefan", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(9), "Erster Satz."),
            new("Du", TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(9), "Mhm."),
            new("Stefan", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(11), "Weiter.")]);
        Assert.Equal(3, text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("[00:00:01 - 00:00:09] Stefan: Erster Satz.", text);
        Assert.Contains("[00:00:08 - 00:00:09] Du: Mhm.", text);
        Assert.Contains("[00:00:10 - 00:00:11] Stefan: Weiter.", text);
    }

    [Fact]
    public void MergedEndNeverShrinksForOverlappingSameSpeakerSegments()
    {
        var text = ConversationFormatter.Format([
            new("Du", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(9), "A"),
            new("Du", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), "B")]);
        Assert.Contains("[00:00:01 - 00:00:09] Du: A B", text);
    }

    [Fact]
    public async Task LiveRerenderMergesNewBatchButSplitsWhenDelayedOtherSourceArrives()
    {
        var path = Path.Combine(Path.GetTempPath(), $"blitztext-turns-{Guid.NewGuid():N}.md");
        try
        {
            var file = new ConversationTranscriptFile(path, "# Test");
            await file.SaveAsync([new("Du", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "A")]);
            await file.SaveAsync([new("Du", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(6), "B")]);
            Assert.Equal("[00:00:01 - 00:00:06] Du: A B" + Environment.NewLine, file.Text);
            await file.SaveAsync([new("Stefan", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4), "C")]);
            Assert.Equal(3, file.Text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.Contains("[00:00:03 - 00:00:04] Stefan: C", await File.ReadAllTextAsync(path));
        }
        finally { File.Delete(path); }
    }
}
