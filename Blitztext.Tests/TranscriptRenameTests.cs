using Blitztext.Core.Conversation;

namespace Blitztext.Tests;

public sealed class TranscriptRenameTests : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
        "BlitztextRename-" + Guid.NewGuid().ToString("N"));

    public TranscriptRenameTests() => Directory.CreateDirectory(directory);

    [Fact]
    public async Task RepeatedNamesPreserveFilesAndTheirContents()
    {
        var first = await Create("first");
        var second = await Create("second");
        var third = await Create("third");
        var original = await File.ReadAllBytesAsync(second.Path);
        first.Rename("Weekly");
        second.Rename("weekly");
        third.Rename("Weekly.md");
        Assert.Equal("Weekly.md", System.IO.Path.GetFileName(first.Path));
        Assert.Equal("weekly (2).md", System.IO.Path.GetFileName(second.Path));
        Assert.Equal("Weekly (3).md", System.IO.Path.GetFileName(third.Path));
        Assert.Equal(original, await File.ReadAllBytesAsync(second.Path));
        second.Rename("weekly");
        Assert.Equal("weekly (2).md", System.IO.Path.GetFileName(second.Path));
        await second.SaveAsync([], "Beendet");
        Assert.False(File.Exists(System.IO.Path.Combine(directory, "second.md")));
        Assert.Contains("Status: Beendet", await File.ReadAllTextAsync(second.Path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("../outside")]
    [InlineData("CON")]
    [InlineData("NUL.md")]
    [InlineData("name.")]
    [InlineData("bad:name")]
    public async Task InvalidNamesKeepOriginal(string name)
    {
        var file = await Create("original");
        var path = file.Path;
        Assert.Throws<ArgumentException>(() => file.Rename(name));
        Assert.Equal(path, file.Path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task MissingSourceLeavesOtherFilesUntouched()
    {
        var file = await Create("original");
        File.Delete(file.Path);
        var destination = System.IO.Path.Combine(directory, "Weekly.md");
        await File.WriteAllTextAsync(destination, "Keep");
        Assert.Throws<FileNotFoundException>(() => file.Rename("Weekly"));
        Assert.Equal("Keep", await File.ReadAllTextAsync(destination));
    }

    private async Task<ConversationTranscriptFile> Create(string name)
    {
        var file = new ConversationTranscriptFile(System.IO.Path.Combine(directory, name + ".md"), "# Test " + name);
        await file.SaveAsync([], "Beendet");
        return file;
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
