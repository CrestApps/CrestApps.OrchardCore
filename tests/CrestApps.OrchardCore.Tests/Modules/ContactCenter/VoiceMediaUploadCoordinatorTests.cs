using CrestApps.OrchardCore.ContactCenter.Core.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// One upload per clip and provider at a time, and its reference shared with whoever asked while it ran or shortly
/// after, so a burst of calls after a change of provider leaves one copy at the provider rather than one per call.
/// </summary>
public sealed class VoiceMediaUploadCoordinatorTests
{
    [Fact]
    public async Task UploadOnceAsync_WhenCallersOverlap_RunsOneUploadAndSharesItsReference()
    {
        // Arrange
        var coordinator = new VoiceMediaUploadCoordinator(new MovableClock());
        var uploads = 0;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<string> Upload(CancellationToken _)
        {
            Interlocked.Increment(ref uploads);
            started.TrySetResult();
            await release.Task;

            return "copy-1";
        }

        // Act
        var first = coordinator.UploadOnceAsync("Telnyx", "master-1", Upload, TestContext.Current.CancellationToken);
        await started.Task;
        var second = coordinator.UploadOnceAsync("telnyx", "master-1", Upload, TestContext.Current.CancellationToken);
        release.SetResult();
        var references = await Task.WhenAll(first, second);

        // Assert
        Assert.Equal(1, uploads);
        Assert.Equal(new[] { "copy-1", "copy-1" }, references);
    }

    [Fact]
    public async Task UploadOnceAsync_ReusesAReferenceMadeWithinTheWindow_AndUploadsAgainAfterIt()
    {
        // Arrange
        var clock = new MovableClock();
        var coordinator = new VoiceMediaUploadCoordinator(clock);
        var uploads = 0;

        Task<string> Upload(CancellationToken _)
            => Task.FromResult($"copy-{Interlocked.Increment(ref uploads)}");

        // Act
        var first = await coordinator.UploadOnceAsync("Telnyx", "master-1", Upload, TestContext.Current.CancellationToken);
        clock.Advance(VoiceMediaUploadCoordinator.RecentUploadWindow - TimeSpan.FromSeconds(1));
        var inside = await coordinator.UploadOnceAsync("Telnyx", "master-1", Upload, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(2));
        var outside = await coordinator.UploadOnceAsync("Telnyx", "master-1", Upload, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("copy-1", first);
        Assert.Equal("copy-1", inside);
        Assert.Equal("copy-2", outside);
    }

    [Fact]
    public async Task UploadOnceAsync_KeepsClipsAndProvidersApart()
    {
        // Arrange
        var coordinator = new VoiceMediaUploadCoordinator(new MovableClock());
        var uploads = 0;

        Task<string> Upload(CancellationToken _)
            => Task.FromResult($"copy-{Interlocked.Increment(ref uploads)}");

        // Act
        var telnyx = await coordinator.UploadOnceAsync("Telnyx", "master-1", Upload, TestContext.Current.CancellationToken);
        var other = await coordinator.UploadOnceAsync("OtherProvider", "master-1", Upload, TestContext.Current.CancellationToken);
        var newAudio = await coordinator.UploadOnceAsync("Telnyx", "master-2", Upload, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new[] { "copy-1", "copy-2", "copy-3" }, new[] { telnyx, other, newAudio });
    }

    [Fact]
    public async Task UploadOnceAsync_DoesNotRememberAFailedUpload()
    {
        // Arrange
        var coordinator = new VoiceMediaUploadCoordinator(new MovableClock());
        var attempts = 0;

        Task<string> Upload(CancellationToken _)
            => Task.FromResult(Interlocked.Increment(ref attempts) == 1 ? null : "copy-2");

        // Act
        var failed = await coordinator.UploadOnceAsync("Telnyx", "master-1", Upload, TestContext.Current.CancellationToken);
        var retried = await coordinator.UploadOnceAsync("Telnyx", "master-1", Upload, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(failed);
        Assert.Equal("copy-2", retried);
    }

    private sealed class MovableClock : IClock
    {
        public DateTime UtcNow { get; private set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public void Advance(TimeSpan by)
            => UtcNow = UtcNow.Add(by);

        public ITimeZone GetTimeZone(string timeZoneId)
            => throw new NotSupportedException();

        public ITimeZone[] GetTimeZones()
            => throw new NotSupportedException();

        public ITimeZone GetSystemTimeZone()
            => throw new NotSupportedException();

        public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone)
            => throw new NotSupportedException();
    }
}
