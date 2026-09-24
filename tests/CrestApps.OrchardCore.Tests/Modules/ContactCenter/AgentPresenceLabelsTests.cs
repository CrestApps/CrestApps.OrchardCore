using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Mvc.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The first render of the agent's presence, before the script takes over; it has to read exactly as
/// shared/agent-presence.js will, or the header changes wording the moment the first update arrives.
/// </summary>
public sealed class AgentPresenceLabelsTests
{
    private static readonly IReadOnlyDictionary<string, string> _labels = AgentPresenceLabels.Create(CreateLocalizer());

    // Bug: a break started with no reason read "Short break" -- the first break reason in the menu -- while the
    // audit record said plain "Break".
    [Theory]
    [InlineData(AgentPresenceStatus.Break, null, "Break")]
    [InlineData(AgentPresenceStatus.Break, " ", "Break")]
    [InlineData(AgentPresenceStatus.Break, "Lunch", "Lunch")]
    [InlineData(AgentPresenceStatus.Away, "Away from desk", "Away from desk")]
    [InlineData(AgentPresenceStatus.Busy, null, "On a call")]
    [InlineData(AgentPresenceStatus.Busy, "Lunch", "On a call")]
    [InlineData(AgentPresenceStatus.WrapUp, "Lunch", "Wrap-up")]
    [InlineData(AgentPresenceStatus.Reserved, null, "Reserved")]
    [InlineData(AgentPresenceStatus.Offline, "site-sign-out", "Offline")]
    public void Describe_NamesTheState_AndShowsAReasonOnlyForTheNotReadyStateItWasChosenFor(
        AgentPresenceStatus status,
        string reason,
        string expected)
    {
        // Act
        var label = AgentPresenceLabels.Describe(status, reason, _labels);

        // Assert
        Assert.Equal(expected, label);
    }

    [Theory]
    [InlineData(AgentPresenceStatus.WrapUp, AgentPresenceStatus.Break, "Lunch", "Break pending: Lunch")]
    [InlineData(AgentPresenceStatus.Busy, AgentPresenceStatus.Break, null, "Break pending")]
    [InlineData(AgentPresenceStatus.Break, AgentPresenceStatus.Break, "Lunch", null)]
    [InlineData(AgentPresenceStatus.WrapUp, AgentPresenceStatus.Available, null, null)]
    [InlineData(AgentPresenceStatus.WrapUp, null, null, null)]
    public void DescribePending_SaysWhichBreakIsWaiting(
        AgentPresenceStatus status,
        AgentPresenceStatus? requested,
        string reason,
        string expected)
    {
        // Act
        var label = AgentPresenceLabels.DescribePending(status, requested, reason, _labels);

        // Assert
        Assert.Equal(expected, label);
    }

    // Bug: the presence menu headed the break reasons "Request break" even for an Available agent, whose break starts
    // the moment they pick one. It is a request only while work holds the agent, as the presence manager decides.
    [Theory]
    [InlineData(AgentPresenceStatus.Available, false, "Break")]
    [InlineData(AgentPresenceStatus.Away, false, "Break")]
    [InlineData(AgentPresenceStatus.Break, false, "Break")]
    [InlineData(AgentPresenceStatus.Offline, false, "Break")]
    [InlineData(AgentPresenceStatus.Reserved, false, "Request break")]
    [InlineData(AgentPresenceStatus.Busy, false, "Request break")]
    [InlineData(AgentPresenceStatus.WrapUp, false, "Request break")]
    [InlineData(AgentPresenceStatus.Available, true, "Request break")]
    public void DescribeBreakChoice_SaysBreakWhenItStartsNow_AndRequestBreakWhenItWaitsForTheWork(
        AgentPresenceStatus status,
        bool hasActiveReservation,
        string expected)
    {
        // Act
        var label = AgentPresenceLabels.DescribeBreakChoice(status, hasActiveReservation, _labels);

        // Assert
        Assert.Equal(expected, label);
    }

    [Fact]
    public void Create_CarriesEveryKeyTheScriptReads()
    {
        // Assert
        Assert.Equal(
            new[]
            {
                "afterHoursUnavailable", "available", "away", "break", "breakPending", "breakPendingWithReason", "busy",
                "doNotDisturb", "meeting", "offline", "requestBreak", "reserved", "training", "wrapUp",
            },
            _labels.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("Break pending: {0}", _labels["breakPendingWithReason"]);
    }

    private static IHtmlLocalizer CreateLocalizer()
    {
        var localizer = new Mock<IHtmlLocalizer>();
        localizer
            .Setup(value => value[It.IsAny<string>()])
            .Returns((string name) => new LocalizedHtmlString(name, name));

        return localizer.Object;
    }
}
