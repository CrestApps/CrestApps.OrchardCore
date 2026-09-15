using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

/// <summary>
/// Automated cadences respect business hours; human sends did not, so an agent working late could text a
/// customer at three in the morning their time with nothing in the way. The guard warns rather than blocks,
/// because a person who genuinely needs to reach a customer out of hours exists and should not be locked out —
/// but overriding it takes a permission, so it is a decision someone made rather than one nobody noticed.
/// </summary>
public sealed class SmsQuietHoursTests
{
    private static readonly DateTime _now = new(2026, 3, 4, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Evaluate_InsideBusinessHours_IsNotQuiet()
    {
        // Arrange
        var guard = CreateGuard(isOpen: true);
        var conversation = Conversation();

        // Act
        var decision = await guard.EvaluateAsync(conversation, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(decision.IsQuietHours);
    }

    [Fact]
    public async Task Evaluate_OutsideBusinessHours_IsQuiet_AndSaysSo()
    {
        // Arrange
        var guard = CreateGuard(isOpen: false);
        var conversation = Conversation();

        // Act
        var decision = await guard.EvaluateAsync(conversation, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.IsQuietHours);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public async Task Evaluate_OnAQueueWithNoCalendar_IsNotQuiet()
    {
        // Arrange
        // A queue that has defined no hours has not said when it is closed, and inventing quiet hours would warn
        // an agent about a boundary nobody set.
        var guard = CreateGuard(isOpen: false, calendarId: null);
        var conversation = Conversation();

        // Act
        var decision = await guard.EvaluateAsync(conversation, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(decision.IsQuietHours);
    }

    [Fact]
    public async Task Evaluate_UsesTheContactLocalTimeZone()
    {
        // Arrange
        // Three in the morning is a property of where the customer is, not where the agent or the server is.
        var gate = new Mock<IBusinessHoursGate>();
        gate.Setup(value => value.IsOpenAsync("cal-1", _now, "America/Los_Angeles", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var guard = CreateGuard(isOpen: true, gate: gate, contactTimeZoneId: "America/Los_Angeles");

        // Act
        var decision = await guard.EvaluateAsync(Conversation(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.IsQuietHours);
        gate.Verify(
            value => value.IsOpenAsync("cal-1", _now, "America/Los_Angeles", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static SmsConversation Conversation()
        => new()
        {
            ItemId = "c1",
            OwnerId = "queue-1",
            OwnerType = SmsConversationOwnerType.Queue,
            ServiceAddress = "+16502530000",
            ContactAddress = "+16502530001",
        };

    private static SmsQuietHoursGuard CreateGuard(
        bool isOpen,
        string calendarId = "cal-1",
        Mock<IBusinessHoursGate> gate = null,
        string contactTimeZoneId = null)
    {
        // A caller that supplied its own gate has already described the behaviour it cares about; adding a
        // catch-all on top would override it, because a later Moq setup wins.
        if (gate is null)
        {
            gate = new Mock<IBusinessHoursGate>();
            gate.Setup(value => value.IsOpenAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(isOpen);
        }

        var queuePolicyReader = new Mock<ISmsQueuePolicyReader>();
        queuePolicyReader.Setup(reader => reader.ReadAsync("queue-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmsQueuePolicy(true, 0, calendarId));

        var timeZoneResolver = new Mock<ISmsContactTimeZoneResolver>();
        timeZoneResolver.Setup(resolver => resolver.ResolveAsync(It.IsAny<SmsConversation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contactTimeZoneId);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new SmsQuietHoursGuard(gate.Object, queuePolicyReader.Object, timeZoneResolver.Object, clock.Object);
    }
}
