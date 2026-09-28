using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// How a call the agent placed from the soft phone's keypad -- one the Contact Center did not route -- is written to the
/// audit log.
/// </summary>
public sealed class ContactCenterKeypadCallAuditTests
{
    private static readonly DateTime _now = new(2026, 9, 25, 0, 14, 51, DateTimeKind.Utc);

    // Bug: two calls the agent dialed from the keypad were written to the audit log as ExtensionCallStarted and
    // ExtensionCallEnded, while the details of the same records said "isExtension": "False". A call placed by number is
    // a dial; only a call to a colleague's extension is an extension call.
    [Fact]
    public async Task TelephonyCallObserver_RecordsAKeypadCall_AsADialAndACallEnd_NotAsAnExtensionCall()
    {
        // Arrange
        var recorder = new RecordingContactCenterAuditRecorder();
        var observer = CreateObserver(recorder);
        var call = CreateKeypadCall();

        // Act
        await observer.CallStartedAsync(call, TestContext.Current.CancellationToken);
        call.Outcome = CallOutcome.Completed;
        call.EndedUtc = _now.AddSeconds(126);
        call.DurationSeconds = 126;
        await observer.CallEndedAsync(call, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [ContactCenterConstants.Events.DialStarted, ContactCenterConstants.Events.CallEnded],
            recorder.Calls.Select(record => record.EventType));
        Assert.All(recorder.Calls, record =>
        {
            Assert.Equal("agent-1", record.Data.AgentId);
            Assert.Equal("browser-1790295290897", record.Data.ProviderCallId);
            Assert.Equal("+17024993350", record.Data.Target);
            Assert.Equal("False", record.Data.Details["isExtension"]);
            Assert.Equal(ContactCenterActorType.Agent, record.Actor.Type);
            Assert.Equal("user-1", record.Actor.Id);
        });
        Assert.Equal(_now, recorder.Calls[0].OccurredUtc);
        Assert.Equal(_now.AddSeconds(126), recorder.Calls[1].OccurredUtc);
        Assert.Equal(126, recorder.Calls[1].Data.DurationSeconds);
        Assert.NotEqual(recorder.Calls[0].IdempotencyKey, recorder.Calls[1].IdempotencyKey);
    }

    // The phone stopped reporting the call and the platform settled it: the end is the platform's doing, not the
    // agent's, and the record says so.
    [Fact]
    public async Task TelephonyCallObserver_NamesThePlatform_ForAKeypadCallItSettledBecauseThePhoneStoppedReportingIt()
    {
        // Arrange
        var recorder = new RecordingContactCenterAuditRecorder();
        var observer = CreateObserver(recorder);
        var call = CreateKeypadCall();
        call.Outcome = CallOutcome.Completed;
        call.EndedUtc = _now.AddSeconds(90);
        call.DurationSeconds = 90;
        call.Put(new ClientRecordedCallActivity { LastReportedUtc = _now.AddSeconds(90), ConnectedUtc = _now.AddSeconds(8), EndedUnreported = true });

        // Act
        await observer.CallEndedAsync(call, TestContext.Current.CancellationToken);

        // Assert
        var ended = Assert.Single(recorder.Calls);
        Assert.Equal(ContactCenterConstants.Events.CallEnded, ended.EventType);
        Assert.Equal(ContactCenterActorType.System, ended.Actor.Type);
        Assert.False(string.IsNullOrEmpty(ended.Data.Reason));
    }

    // Guard: the extension events are for calls to and from a colleague. An outbound call placed by number is never
    // one, and an outbound extension call always is. A call rung to the agent that the Contact Center did not route is a
    // colleague's direct call.
    [Theory]
    [InlineData(CallDirection.Outbound, false, false, ContactCenterConstants.Events.DialStarted)]
    [InlineData(CallDirection.Outbound, false, true, ContactCenterConstants.Events.CallEnded)]
    [InlineData(CallDirection.Outbound, true, false, ContactCenterConstants.Events.ExtensionCallStarted)]
    [InlineData(CallDirection.Outbound, true, true, ContactCenterConstants.Events.ExtensionCallEnded)]
    [InlineData(CallDirection.Inbound, true, false, ContactCenterConstants.Events.ExtensionCallStarted)]
    [InlineData(CallDirection.Inbound, true, true, ContactCenterConstants.Events.ExtensionCallEnded)]
    [InlineData(CallDirection.Inbound, false, false, ContactCenterConstants.Events.ExtensionCallStarted)]
    [InlineData(CallDirection.Inbound, false, true, ContactCenterConstants.Events.ExtensionCallEnded)]
    public void TelephonyCallObserver_OnlyCallsAnExtensionCallAnExtensionCall(CallDirection direction, bool isExtension, bool ended, string expected)
    {
        // Arrange
        var call = new TelephonyInteraction { CallId = "call-1", Direction = direction, IsExtension = isExtension };

        // Act
        var eventType = ContactCenterTelephonyCallObserver.ResolveEventType(call, ended);

        // Assert
        Assert.Equal(expected, eventType);
    }

    [Fact]
    public void TelephonyCallObserver_NeverRecordsAnOutboundCallPlacedByNumber_AsAnExtensionCall()
    {
        // Arrange
        string[] extensionEvents = [ContactCenterConstants.Events.ExtensionCallStarted, ContactCenterConstants.Events.ExtensionCallEnded];
        var call = new TelephonyInteraction { CallId = "browser-1", Direction = CallDirection.Outbound, IsExtension = false };

        // Act
        var eventTypes = new[] { false, true }.Select(ended => ContactCenterTelephonyCallObserver.ResolveEventType(call, ended));

        // Assert
        Assert.DoesNotContain(eventTypes, extensionEvents.Contains);
    }

    private static ContactCenterTelephonyCallObserver CreateObserver(RecordingContactCenterAuditRecorder recorder)
    {
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });

        return new ContactCenterTelephonyCallObserver(
            new Mock<IInteractionManager>().Object,
            agentManager.Object,
            new Lazy<IContactCenterAuditRecorder>(recorder),
            NullLogger<ContactCenterTelephonyCallObserver>.Instance);
    }

    // A call placed from the keypad: the client records it with no provider identity and no Contact Center interaction.
    private static TelephonyInteraction CreateKeypadCall()
        => new()
        {
            InteractionId = "4k7dpx49f4scetwvy26ezhhfwx",
            CallId = "browser-1790295290897",
            UserId = "user-1",
            Direction = CallDirection.Outbound,
            From = "+17787204596",
            To = "+17024993350",
            IsExtension = false,
            Outcome = CallOutcome.InProgress,
            StartedUtc = _now,
        };
}
