using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Modules;
using static CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports.AuditEvents;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

public sealed class ReconciledTimecardTests
{
    private static readonly DateTime _day = new(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_AFullyAuditedDay_Reconciles()
    {
        // Arrange
        var signIn = _day.AddHours(9);
        var events = new[]
        {
            State("agent-1", signIn, AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            State("agent-1", signIn.AddMinutes(30).AddMilliseconds(250), AgentPresenceStatus.Available, AgentPresenceStatus.Reserved, AgentStateChangeSources.Reserved),
            State("agent-1", signIn.AddMinutes(30).AddSeconds(7), AgentPresenceStatus.Reserved, AgentPresenceStatus.Busy, AgentStateChangeSources.Accepted),
            State("agent-1", signIn.AddMinutes(41), AgentPresenceStatus.Busy, AgentPresenceStatus.WrapUp, AgentStateChangeSources.WrapUpStarted),
            State("agent-1", signIn.AddMinutes(43), AgentPresenceStatus.WrapUp, AgentPresenceStatus.Break, AgentStateChangeSources.RequestApplied),
            State("agent-1", signIn.AddMinutes(58), AgentPresenceStatus.Break, AgentPresenceStatus.Available),
            State("agent-1", signIn.AddHours(8), AgentPresenceStatus.Available, AgentPresenceStatus.Offline, AgentStateChangeSources.SignOut),
        };

        // Act
        var day = Assert.Single(ReconciledTimecard.Build(AgentStateTimeline.Build(events), _day, _day.AddDays(1)));

        // Assert
        Assert.True(day.IsReconciled);
        Assert.Equal(TimeSpan.FromHours(8).TotalSeconds, day.SignedInSeconds);
        Assert.Equal(day.SignedInSeconds, day.StateSeconds, 6);
        Assert.Equal(6.75, day.States.ReservedSeconds, 6);
        Assert.Equal(signIn, day.FirstInUtc);
        Assert.Equal(signIn.AddHours(8), day.LastOutUtc);
        Assert.True(day.EndsSignedOff);
        Assert.True(day.FromAudit);
        Assert.False(day.FromLegacy);
    }

    [Fact]
    public void Build_StateRecordedAfterSignOff_DoesNotReconcile()
    {
        // Arrange: something moved the agent to Available after they signed out, without signing them in.
        var events = new[]
        {
            State("agent-1", _day.AddHours(9), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            State("agent-1", _day.AddHours(12), AgentPresenceStatus.Available, AgentPresenceStatus.Offline, AgentStateChangeSources.SignOut),
            State("agent-1", _day.AddHours(13), AgentPresenceStatus.Reserved, AgentPresenceStatus.Available, AgentStateChangeSources.Released, actorType: ContactCenterActorType.System),
            State("agent-1", _day.AddHours(14), AgentPresenceStatus.Available, AgentPresenceStatus.Offline, AgentStateChangeSources.Reconciled, actorType: ContactCenterActorType.System),
        };

        // Act
        var day = Assert.Single(ReconciledTimecard.Build(AgentStateTimeline.Build(events), _day, _day.AddDays(1)));

        // Assert
        Assert.False(day.IsReconciled);
        Assert.Equal(TimeSpan.FromHours(3).TotalSeconds, day.SignedInSeconds);
        Assert.Equal(TimeSpan.FromHours(4).TotalSeconds, day.StateSeconds);
        Assert.Equal(TimeSpan.FromHours(1).TotalSeconds, day.DifferenceSeconds);
        Assert.Equal(1, day.MissingTransitions);
    }

    [Fact]
    public void Build_MissingTransition_DoesNotReconcileEvenWhenTheTotalsAgree()
    {
        // Arrange: the move from Available to Busy was never recorded.
        var events = new[]
        {
            State("agent-1", _day.AddHours(9), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            State("agent-1", _day.AddHours(10), AgentPresenceStatus.Busy, AgentPresenceStatus.WrapUp, AgentStateChangeSources.WrapUpStarted),
            State("agent-1", _day.AddHours(11), AgentPresenceStatus.WrapUp, AgentPresenceStatus.Offline, AgentStateChangeSources.SignOut),
        };

        // Act
        var day = Assert.Single(ReconciledTimecard.Build(AgentStateTimeline.Build(events), _day, _day.AddDays(1)));

        // Assert
        Assert.Equal(0, day.DifferenceSeconds, 6);
        Assert.Equal(1, day.MissingTransitions);
        Assert.False(day.IsReconciled);
    }

    [Fact]
    public void Build_ShiftAcrossMidnight_IsSplitIntoTwoDaysThatEachReconcile()
    {
        // Arrange
        var events = new[]
        {
            State("agent-1", _day.AddHours(22), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            State("agent-1", _day.AddHours(23).AddMinutes(30), AgentPresenceStatus.Available, AgentPresenceStatus.Break),
            State("agent-1", _day.AddDays(1).AddMinutes(15), AgentPresenceStatus.Break, AgentPresenceStatus.Available),
            State("agent-1", _day.AddDays(1).AddHours(4), AgentPresenceStatus.Available, AgentPresenceStatus.Offline, AgentStateChangeSources.SignOut),
        };

        // Act
        var days = ReconciledTimecard.Build(AgentStateTimeline.Build(events), _day, _day.AddDays(2));

        // Assert
        Assert.Collection(
            days,
            first =>
            {
                Assert.Equal(DateOnly.FromDateTime(_day), first.Date);
                Assert.True(first.IsReconciled);
                Assert.Equal(TimeSpan.FromHours(2).TotalSeconds, first.SignedInSeconds);
                Assert.Equal(TimeSpan.FromMinutes(30).TotalSeconds, first.States.BreakSeconds);
                Assert.False(first.EndsSignedOff);
            },
            second =>
            {
                Assert.Equal(DateOnly.FromDateTime(_day.AddDays(1)), second.Date);
                Assert.True(second.IsReconciled);
                Assert.Equal(TimeSpan.FromHours(4).TotalSeconds, second.SignedInSeconds);
                Assert.Equal(TimeSpan.FromMinutes(15).TotalSeconds, second.States.BreakSeconds);
                Assert.Equal(_day.AddDays(1), second.FirstInUtc);
                Assert.True(second.EndsSignedOff);
            });
    }

    [Fact]
    public void Build_AgentStillSignedInAtTheEndOfThePeriod_IsCountedUpToItAndReconciles()
    {
        // Arrange
        var periodEnd = _day.AddHours(13).AddSeconds(17);
        var events = new[]
        {
            State("agent-1", _day.AddHours(9), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            State("agent-1", _day.AddHours(12), AgentPresenceStatus.Available, AgentPresenceStatus.Busy, AgentStateChangeSources.Accepted),
        };

        // Act
        var day = Assert.Single(ReconciledTimecard.Build(AgentStateTimeline.Build(events), _day, periodEnd));

        // Assert
        Assert.True(day.IsReconciled);
        Assert.Equal((periodEnd - _day.AddHours(9)).TotalSeconds, day.SignedInSeconds);
        Assert.Equal(periodEnd, day.LastOutUtc);
        Assert.False(day.EndsSignedOff);
    }

    [Fact]
    public void Build_SignOffDatedByTheLastHeartbeat_EndsTheDayThereAndReconciles()
    {
        // Arrange
        var heartbeat = _day.AddHours(15).AddMilliseconds(420);
        var events = new[]
        {
            State("agent-1", _day.AddHours(9), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            State("agent-1", heartbeat.AddSeconds(30), AgentPresenceStatus.Available, AgentPresenceStatus.Reserved, AgentStateChangeSources.Reserved, actorType: ContactCenterActorType.System),
            State("agent-1", heartbeat, AgentPresenceStatus.Reserved, AgentPresenceStatus.Offline, AgentStateChangeSources.SessionExpired, recordedUtc: heartbeat.AddSeconds(120), actorType: ContactCenterActorType.System),
        };

        // Act
        var day = Assert.Single(ReconciledTimecard.Build(AgentStateTimeline.Build(events), _day, _day.AddDays(1)));

        // Assert
        Assert.True(day.IsReconciled);
        Assert.Equal(heartbeat, day.LastOutUtc);
        Assert.Equal((heartbeat - _day.AddHours(9)).TotalSeconds, day.SignedInSeconds, 6);
        Assert.Equal(0, day.States.ReservedSeconds);
    }

    [Fact]
    public void Report_CountsUnreconciledDays_AndItsColumnsAddUpToTheSecond()
    {
        // Arrange
        var events = new[]
        {
            // Day one reconciles, with fractions of a second in every state.
            State("agent-1", _day.AddHours(9), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            State("agent-1", _day.AddHours(9).AddSeconds(10.4), AgentPresenceStatus.Available, AgentPresenceStatus.Reserved, AgentStateChangeSources.Reserved),
            State("agent-1", _day.AddHours(9).AddSeconds(20.8), AgentPresenceStatus.Reserved, AgentPresenceStatus.Busy, AgentStateChangeSources.Accepted),
            State("agent-1", _day.AddHours(9).AddSeconds(31.2), AgentPresenceStatus.Busy, AgentPresenceStatus.Offline, AgentStateChangeSources.SignOut),

            // Day two is missing a transition.
            State("agent-1", _day.AddDays(1).AddHours(9), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            State("agent-1", _day.AddDays(1).AddHours(10), AgentPresenceStatus.Busy, AgentPresenceStatus.Offline, AgentStateChangeSources.SignOut),
        };
        var days = ReconciledTimecard.Build(AgentStateTimeline.Build(events), _day, _day.AddDays(2));
        var provider = new ReconciledPayrollTimecardReportProvider(
            Mock.Of<IContactCenterReportingService>(),
            Mock.Of<IContactCenterReportCapabilityGuard>(),
            Mock.Of<IInteractionEventStore>(),
            Mock.Of<IAgentProfileManager>(),
            Mock.Of<IClock>(),
            new PassThroughLocalizer<ReconciledPayrollTimecardReportProvider>());

        // Act
        var document = provider.Build(days, agentId => agentId);

        // Assert
        var summary = document.Sections[0];
        Assert.Equal("1", summary.Metrics.Single(metric => metric.Label == "Unreconciled days").Value);

        var table = document.Sections[1];
        var first = table.Rows[0].Cells;
        Assert.Equal("0:00:31", first[4]);
        Assert.Equal(
            ParseSeconds(first[12]),
            Enumerable.Range(5, 7).Sum(index => ParseSeconds(first[index])));
        Assert.Equal(first[4], first[12]);
        Assert.Equal("Reconciled", first[14]);

        var second = table.Rows[1].Cells;
        Assert.Contains("missing", second[14], StringComparison.Ordinal);
        Assert.Equal(ReportRowKind.GrandTotal, table.Rows[^1].Kind);
    }

    private static long ParseSeconds(string clock)
    {
        var parts = clock.Split(':');

        return (long.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) * 3600) +
            (long.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) * 60) +
            long.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
