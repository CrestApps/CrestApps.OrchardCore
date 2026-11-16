using CrestApps.OrchardCore.Learning.Lti;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.Tests.Learning.Lti;

/// <summary>
/// The message hint (<c>lti_message_hint</c>) is signed, short-lived and bound to the person, the tool, the course and
/// the course item: an altered, expired or foreign hint reads as nothing.
/// </summary>
public sealed class LtiMessageHintsTests
{
    private static readonly DateTime _now = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    private readonly EphemeralDataProtectionProvider _protection = new();
    private readonly TestClock _clock = new(_now);

    [Fact]
    public void Read_AHintItMade_ReturnsWhatTheLaunchIsFor()
    {
        // Arrange
        var hints = new LtiMessageHints(_protection, _clock);
        var value = hints.Create(Hint());

        // Act
        var hint = hints.Read(value);

        // Assert
        Assert.NotNull(hint);
        Assert.Equal(LtiConstants.MessageTypes.ResourceLinkRequest, hint.MessageType);
        Assert.Equal("tool-1", hint.ToolId);
        Assert.Equal("user-1", hint.UserId);
        Assert.Equal("course-1", hint.CourseId);
        Assert.Equal("item-1", hint.ItemId);
        Assert.Equal("enrollment-1", hint.EnrollmentId);
        Assert.Equal([LtiConstants.Roles.Learner], hint.Roles);
        Assert.Equal(_now + LtiMessageHints.Lifetime, hint.ExpiresUtc);
    }

    [Fact]
    public void Read_AnAlteredHint_ReturnsNull()
    {
        // Arrange
        var hints = new LtiMessageHints(_protection, _clock);
        var value = hints.Create(Hint());
        var altered = string.Concat(value[0] == 'A' ? "B" : "A", value.AsSpan(1));

        // Act and assert
        Assert.Null(hints.Read(altered));
        Assert.Null(hints.Read("not-a-hint"));
        Assert.Null(hints.Read(string.Empty));
    }

    [Fact]
    public void Read_AfterFiveMinutes_ReturnsNull()
    {
        // Arrange
        var hints = new LtiMessageHints(_protection, _clock);
        var value = hints.Create(Hint());

        _clock.UtcNow = _now.AddMinutes(5);

        // Act and assert
        Assert.Null(hints.Read(value));
    }

    [Fact]
    public void Read_AHintOfAnotherSite_ReturnsNull()
    {
        // Arrange: another tenant has other data protection keys.
        var value = new LtiMessageHints(new EphemeralDataProtectionProvider(), _clock).Create(Hint());

        // Act and assert
        Assert.Null(new LtiMessageHints(_protection, _clock).Read(value));
    }

    private static LtiMessageHint Hint()
        => new()
        {
            MessageType = LtiConstants.MessageTypes.ResourceLinkRequest,
            ToolId = "tool-1",
            UserId = "user-1",
            CourseId = "course-1",
            ItemId = "item-1",
            EnrollmentId = "enrollment-1",
            Roles = [LtiConstants.Roles.Learner],
        };
}
