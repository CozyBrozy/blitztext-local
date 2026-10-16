using Blitztext.Core.Models;
using Blitztext.Core.Settings;
using Blitztext.Core.Transcription;
using Blitztext.Core.Workflow;
namespace Blitztext.Tests;
public sealed class WorkflowRunnerTests
{
    [Fact]
    public async Task LocalTranscriptionNeedsNoKeyAndDeletesAudio()
    {
        var path = Audio();
        var runner = new WorkflowRunner(new FakeTranscriber(), () => new AppSettings());
        Assert.Equal("Hallo Welt.", await runner.ProcessAsync(WorkflowType.Transcription, path, TimeSpan.FromSeconds(3), default));
        Assert.False(File.Exists(path));
    }
    [Fact]
    public async Task FailureDeletesAudio()
    {
        var path = Audio();
        var runner = new WorkflowRunner(new FakeTranscriber(true), () => new AppSettings());
        await Assert.ThrowsAsync<IOException>(() => runner.ProcessAsync(WorkflowType.Transcription, path, TimeSpan.FromSeconds(3), default));
        Assert.False(File.Exists(path));
    }
    [Fact]
    public async Task CancellationDeletesAudio()
    {
        var path = Audio();
        var runner = new WorkflowRunner(new FakeTranscriber(), () => new AppSettings());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.ProcessAsync(WorkflowType.Transcription, path, TimeSpan.FromSeconds(3), new CancellationToken(true)));
        Assert.False(File.Exists(path));
    }
    [Fact]
    public async Task RewriteIsRejectedWithoutCallingEngine()
    {
        var path = Audio();
        var runner = new WorkflowRunner(new FakeTranscriber(true), () => new AppSettings());
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.ProcessAsync(WorkflowType.TextImprover, path, TimeSpan.FromSeconds(3), default));
        Assert.False(File.Exists(path));
    }
    [Fact]
    public async Task MissingModelFailsLocally()
    {
        var path = Audio();
        using var engine = new LocalWhisperTranscriber(Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".bin"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.TranscribeAsync(path, "de", default));
        File.Delete(path);
    }
    private static string Audio()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".wav");
        File.WriteAllBytes(path, [0,1,2]);
        return path;
    }
    private sealed class FakeTranscriber(bool fail = false) : ILocalTranscriber
    {
        public Task<string> TranscribeAsync(string audioPath, string language, CancellationToken token)
        {
            Assert.True(File.Exists(audioPath));
            if (fail) throw new IOException("Test failure");
            Assert.Equal("de", language);
            return Task.FromResult(" Hallo Welt. ");
        }
    }
}
