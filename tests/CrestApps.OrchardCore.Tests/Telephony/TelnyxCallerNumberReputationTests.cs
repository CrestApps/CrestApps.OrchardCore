using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The calls carriers refuse as spam (SIP 607 Unwanted, 608 Rejected) are counted against the number they were placed
/// from, so an admin can see a number's reputation is suffering before every call from it goes unanswered.
/// </summary>
public sealed class TelnyxCallerNumberReputationTests
{
    private static readonly DateTime _now = new(2026, 10, 29, 15, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("608")]
    [InlineData("607")]
    public void AnOutboundLegRefusedAsSpam_IsASpamRejection(string sipCode)
    {
        // Act
        var rejected = TelnyxCallerNumberReputation.IsSpamRejection(Hangup(sipCode));

        // Assert
        Assert.True(rejected);
    }

    [Theory]
    [InlineData("call.hangup", "incoming", "608", "+17025550100", "+15551234567")]
    [InlineData("call.hangup", "outgoing", "486", "+17025550100", "+15551234567")]
    [InlineData("call.hangup", "outgoing", null, "+17025550100", "+15551234567")]
    [InlineData("call.answered", "outgoing", "608", "+17025550100", "+15551234567")]
    [InlineData("call.hangup", "outgoing", "608", "sip:agent@example.com", "+15551234567")]
    [InlineData("call.hangup", "outgoing", "608", "+17025550100", "sip:agent@example.com")]
    public void OtherEndings_AreNotSpamRejections(string eventType, string direction, string sipCode, string from, string to)
    {
        // Arrange
        var callEvent = new TelnyxCallEvent
        {
            EventType = eventType,
            Direction = direction,
            SipHangupCause = sipCode,
            From = from,
            To = to,
        };

        // Act
        var rejected = TelnyxCallerNumberReputation.IsSpamRejection(callEvent);

        // Assert
        Assert.False(rejected);
    }

    [Fact]
    public void Record_CountsByDay_AndDropsTheDaysOutsideTheWindow()
    {
        // Arrange
        var document = new TelnyxCallerNumberReputationDocument();
        TelnyxCallerNumberReputation.Record(document, "+17025550100", "608", _now.AddDays(-45));

        // Act
        TelnyxCallerNumberReputation.Record(document, "+17025550100", "608", _now.AddDays(-2));
        TelnyxCallerNumberReputation.Record(document, "+17025550100", "607", _now);
        var rejections = TelnyxCallerNumberReputation.Record(document, "+17025550100", "608", _now);

        // Assert
        Assert.Equal(4, rejections.Total);
        Assert.Equal(2, rejections.Days["2026-10-29"]);
        Assert.Equal(1, rejections.Days["2026-10-27"]);
        Assert.False(rejections.Days.ContainsKey(_now.AddDays(-45).ToString("yyyy-MM-dd")));
        Assert.Equal(3, rejections.CountSince(_now.AddDays(-TelnyxCallerNumberReputation.WindowDays)));
        Assert.Equal(2, rejections.CountSince(_now));
        Assert.Equal("608", rejections.LastSipCode);
        Assert.Equal(_now, rejections.LastRejectedUtc);
    }

    [Fact]
    public void Record_KeepsTheLatestRejectionTime_WhenAnOlderDeliveryArrivesLate()
    {
        // Arrange
        var document = new TelnyxCallerNumberReputationDocument();
        TelnyxCallerNumberReputation.Record(document, "+17025550100", "608", _now);

        // Act
        var rejections = TelnyxCallerNumberReputation.Record(document, "+17025550100", "608", _now.AddHours(-3));

        // Assert
        Assert.Equal(_now, rejections.LastRejectedUtc);
        Assert.Equal(2, rejections.Days["2026-10-29"]);
    }

    [Fact]
    public async Task Observe_CountsASpamRejection_AgainstTheCallerNumber()
    {
        // Arrange
        var document = new TelnyxCallerNumberReputationDocument();
        var manager = Manager(document);
        var reputation = new TelnyxCallerNumberReputation(manager.Object, new StubClock(_now), NullLogger<TelnyxCallerNumberReputation>.Instance);

        // Act
        var counted = await reputation.ObserveAsync(Hangup("608"));

        // Assert
        Assert.True(counted);
        Assert.Equal(1, document.Numbers["+17025550100"].Total);
        manager.Verify(documents => documents.UpdateAsync(document, It.IsAny<Func<TelnyxCallerNumberReputationDocument, Task>>()), Times.Once);
    }

    [Fact]
    public async Task Observe_LeavesTheDocumentAlone_ForAnOrdinaryHangup()
    {
        // Arrange
        var manager = Manager(new TelnyxCallerNumberReputationDocument());
        var reputation = new TelnyxCallerNumberReputation(manager.Object, new StubClock(_now), NullLogger<TelnyxCallerNumberReputation>.Instance);

        // Act
        var counted = await reputation.ObserveAsync(Hangup("200"));

        // Assert
        Assert.False(counted);
        manager.Verify(documents => documents.GetOrCreateMutableAsync(It.IsAny<Func<Task<TelnyxCallerNumberReputationDocument>>>()), Times.Never);
    }

    [Fact]
    public async Task ApplyTo_PutsTheWindowsCountOnTheAccountsNumbers()
    {
        // Arrange
        var document = new TelnyxCallerNumberReputationDocument();
        TelnyxCallerNumberReputation.Record(document, "+17025550100", "608", _now.AddDays(-40));
        TelnyxCallerNumberReputation.Record(document, "+17025550100", "608", _now.AddDays(-1));
        TelnyxCallerNumberReputation.Record(document, "+17025550100", "607", _now);
        var reputation = new TelnyxCallerNumberReputation(Manager(document).Object, new StubClock(_now), NullLogger<TelnyxCallerNumberReputation>.Instance);
        var flagged = new TelnyxOwnedNumber { Id = "1", PhoneNumber = "+17025550100" };
        var clean = new TelnyxOwnedNumber { Id = "2", PhoneNumber = "+17025550199" };

        // Act
        await reputation.ApplyToAsync([flagged, clean]);

        // Assert
        Assert.Equal(2, flagged.SpamRejections);
        Assert.Equal(_now, flagged.LastSpamRejectionUtc);
        Assert.Equal(0, clean.SpamRejections);
        Assert.Null(clean.LastSpamRejectionUtc);
    }

    private static TelnyxCallEvent Hangup(string sipCode)
        => new()
        {
            EventType = "call.hangup",
            Direction = "outgoing",
            CallControlId = "v3:leg",
            From = "+17025550100",
            To = "+15551234567",
            SipHangupCause = sipCode,
            OccurredUtc = _now,
        };

    private static Mock<IDocumentManager<TelnyxCallerNumberReputationDocument>> Manager(TelnyxCallerNumberReputationDocument document)
    {
        var manager = new Mock<IDocumentManager<TelnyxCallerNumberReputationDocument>>();
        manager
            .Setup(documents => documents.GetOrCreateMutableAsync(It.IsAny<Func<Task<TelnyxCallerNumberReputationDocument>>>()))
            .ReturnsAsync(document);
        manager
            .Setup(documents => documents.GetOrCreateImmutableAsync(It.IsAny<Func<Task<TelnyxCallerNumberReputationDocument>>>()))
            .ReturnsAsync(document);

        return manager;
    }
}
