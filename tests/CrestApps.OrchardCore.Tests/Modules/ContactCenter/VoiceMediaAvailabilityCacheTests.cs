using CrestApps.OrchardCore.ContactCenter.Core.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The memory that spares a provider a question before every play: it remembers only a confirmed presence, only
/// for the window, and forgets a copy the moment it is deleted or replaced.
/// </summary>
public sealed class VoiceMediaAvailabilityCacheTests
{
    [Fact]
    public void IsKnownAvailable_IsFalse_UntilTheCopyIsMarked()
    {
        // Arrange
        var cache = new VoiceMediaAvailabilityCache(new MovableClock());

        // Act
        var before = cache.IsKnownAvailable("Telnyx", "copy-1");
        cache.MarkAvailable("Telnyx", "copy-1");
        var after = cache.IsKnownAvailable("Telnyx", "copy-1");

        // Assert
        Assert.False(before);
        Assert.True(after);
    }

    [Fact]
    public void IsKnownAvailable_MatchesTheProviderWithoutRegardToCase()
    {
        // Arrange
        var cache = new VoiceMediaAvailabilityCache(new MovableClock());

        // Act
        cache.MarkAvailable("Telnyx", "copy-1");

        // Assert
        Assert.True(cache.IsKnownAvailable("telnyx", "copy-1"));
        Assert.False(cache.IsKnownAvailable("Telnyx", "copy-2"));
    }

    [Fact]
    public void IsKnownAvailable_Expires_WhenTheWindowHasPassed()
    {
        // Arrange
        var clock = new MovableClock();
        var cache = new VoiceMediaAvailabilityCache(clock);
        cache.MarkAvailable("Telnyx", "copy-1");

        // Act
        clock.Advance(VoiceMediaAvailabilityCache.Window - TimeSpan.FromSeconds(1));
        var inside = cache.IsKnownAvailable("Telnyx", "copy-1");
        clock.Advance(TimeSpan.FromSeconds(2));
        var outside = cache.IsKnownAvailable("Telnyx", "copy-1");

        // Assert
        Assert.True(inside);
        Assert.False(outside);
    }

    [Fact]
    public void Forget_RemovesTheCopy()
    {
        // Arrange
        var cache = new VoiceMediaAvailabilityCache(new MovableClock());
        cache.MarkAvailable("Telnyx", "copy-1");

        // Act
        cache.Forget("TELNYX", "copy-1");

        // Assert
        Assert.False(cache.IsKnownAvailable("Telnyx", "copy-1"));
    }

    [Fact]
    public void MarkAvailable_IgnoresAnEmptyProviderOrReference()
    {
        // Arrange
        var cache = new VoiceMediaAvailabilityCache(new MovableClock());

        // Act
        cache.MarkAvailable(null, "copy-1");
        cache.MarkAvailable("Telnyx", null);

        // Assert
        Assert.False(cache.IsKnownAvailable(null, "copy-1"));
        Assert.False(cache.IsKnownAvailable("Telnyx", null));
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
