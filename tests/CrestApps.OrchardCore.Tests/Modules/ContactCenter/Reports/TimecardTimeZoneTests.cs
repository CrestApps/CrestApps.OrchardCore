using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Modules;
using static CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports.AuditEvents;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// A timecard's day is the tenant's local day. The period is chosen as local dates, so it runs from local midnight
/// (07:00 UTC in Pacific daylight time); the timecards then grouped by the UTC day and labelled it "Date (UTC)",
/// which showed the local day's start as "First observed 07:00:00" and split every evening shift across two dates.
/// </summary>
public sealed class TimecardTimeZoneTests
{
    private const string Pacific = "America/Los_Angeles";

    // Local midnight on 24 September in Pacific daylight time, and the next.
    private static readonly DateTime _dayStartUtc = new(2026, 9, 24, 7, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _dayEndUtc = new(2026, 9, 25, 7, 0, 0, DateTimeKind.Utc);

    // 08:00 to 21:30 local: 15:00 UTC on the 24th to 04:30 UTC on the 25th.
    private static readonly DateTime _signInUtc = new(2026, 9, 24, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _signOutUtc = new(2026, 9, 25, 4, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void ReconciledTimecard_InTheTenantsZone_IsOneLocalWorkday_ThatStillReconciles()
    {
        // Arrange
        var zone = ReportTimeZone.FromId(Pacific);

        // Act
        var days = ReconciledTimecard.Build(AgentStateTimeline.Build(ShiftEvents()), _dayStartUtc, _dayEndUtc, zone);

        // Assert
        var day = Assert.Single(days);
        Assert.Equal(new DateOnly(2026, 9, 24), day.Date);
        Assert.Equal(_signInUtc, day.FirstInUtc);
        Assert.Equal(_signOutUtc, day.LastOutUtc);
        Assert.True(day.IsReconciled);
        Assert.Equal((_signOutUtc - _signInUtc).TotalSeconds, day.SignedInSeconds, 6);
        Assert.Equal(day.SignedInSeconds, day.StateSeconds, 6);
    }

    [Fact]
    public void ReconciledTimecard_SplitAtLocalMidnight_KeepsEveryDayReconciled()
    {
        // Arrange
        // A shift that runs past local midnight is two workdays, split at the tenant's midnight, and each adds up.
        var zone = ReportTimeZone.FromId(Pacific);
        var lateSignOutUtc = _dayEndUtc.AddHours(2);
        var events = new[]
        {
            State("agent-1", _signInUtc, AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            State("agent-1", _signInUtc.AddHours(3), AgentPresenceStatus.Available, AgentPresenceStatus.Break, reason: "Lunch"),
            State("agent-1", _signInUtc.AddHours(4), AgentPresenceStatus.Break, AgentPresenceStatus.Available),
            State("agent-1", lateSignOutUtc, AgentPresenceStatus.Available, AgentPresenceStatus.Offline, AgentStateChangeSources.SignOut),
        };

        // Act
        var days = ReconciledTimecard.Build(AgentStateTimeline.Build(events), _dayStartUtc, _dayEndUtc.AddDays(1), zone);

        // Assert
        Assert.Collection(
            days,
            first =>
            {
                Assert.Equal(new DateOnly(2026, 9, 24), first.Date);
                Assert.Equal((_dayEndUtc - _signInUtc).TotalSeconds, first.SignedInSeconds, 6);
                Assert.True(first.IsReconciled);
            },
            second =>
            {
                Assert.Equal(new DateOnly(2026, 9, 25), second.Date);
                Assert.Equal(_dayEndUtc, second.FirstInUtc);
                Assert.Equal(TimeSpan.FromHours(2).TotalSeconds, second.SignedInSeconds, 6);
                Assert.True(second.IsReconciled);
            });
    }

    [Fact]
    public void ReconciledPayrollTimecard_NamesTheZone_AndShowsLocalTimes()
    {
        // Arrange
        var zone = ReportTimeZone.FromId(Pacific);
        var days = ReconciledTimecard.Build(AgentStateTimeline.Build(ShiftEvents()), _dayStartUtc, _dayEndUtc, zone);
        var provider = new ReconciledPayrollTimecardReportProvider(
            Mock.Of<IContactCenterReportingService>(),
            Mock.Of<IContactCenterReportCapabilityGuard>(),
            Mock.Of<IInteractionEventStore>(),
            Mock.Of<IAgentProfileManager>(),
            Mock.Of<IClock>(),
            Mock.Of<ILocalClock>(),
            new FormattingLocalizer<ReconciledPayrollTimecardReportProvider>());

        // Act
        var document = provider.Build(days, agentId => agentId, zone);

        // Assert
        var table = document.Sections[1];
        Assert.Equal("Date (America/Los_Angeles)", table.Columns[0].Label);
        var row = table.Rows[0].Cells;
        Assert.Equal("2026-09-24", row[0]);
        Assert.Equal("08:00:00", row[2]);
        Assert.Equal("21:30:00", row[3]);
    }

    [Fact]
    public void DailyAgentTimecard_NamesTheZone_AndShowsTheLocalDayAndTimes()
    {
        // Arrange
        var zone = ReportTimeZone.FromId(Pacific);
        var intervals = AgentStateTimeline.BuildIntervals(AgentStateTimeline.Build(ShiftEvents()), _dayStartUtc, _dayEndUtc);
        var provider = new AgentWorkforceReportProvider(
            Mock.Of<IInteractionEventStore>(),
            Mock.Of<IAgentProfileManager>(),
            Mock.Of<ICatalogManager<OmnichannelCampaign>>(),
            new AgentWorkforceReportDefinition(
                "contact-center-agent-daily-timecard",
                () => new LocalizedString("name", "Daily agent timecard"),
                () => new LocalizedString("description", "Daily agent timecard"),
                AgentWorkforceReportKind.DailyTimecard,
                "Workforce",
                []),
            Mock.Of<IContactCenterReportCapabilityGuard>(),
            new FormattingLocalizer<AgentWorkforceReportProvider>(),
            Mock.Of<IClock>(),
            Mock.Of<ILocalClock>());

        // Act
        var document = provider.BuildDailyTimecard(intervals, new Dictionary<string, AgentProfile>(StringComparer.Ordinal), zone);

        // Assert
        var table = document.Sections[0];
        Assert.Equal("Date (America/Los_Angeles)", table.Columns[0].Label);

        // The whole shift is one day, with no second date for the hours after 17:00 local.
        var agentRow = Assert.Single(table.Rows, row => row.Kind == CrestApps.OrchardCore.Reports.Models.ReportRowKind.Detail);
        Assert.Equal("2026-09-24", agentRow.Cells[0]);
        Assert.Equal("08:00:00", agentRow.Cells[6]);
    }

    private static InteractionEvent[] ShiftEvents()
        =>
        [
            State("agent-1", _signInUtc, AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            State("agent-1", _signInUtc.AddHours(2).AddMilliseconds(250), AgentPresenceStatus.Available, AgentPresenceStatus.Reserved, AgentStateChangeSources.Reserved),
            State("agent-1", _signInUtc.AddHours(2).AddSeconds(9), AgentPresenceStatus.Reserved, AgentPresenceStatus.Busy, AgentStateChangeSources.Accepted),
            State("agent-1", _signInUtc.AddHours(2).AddMinutes(12), AgentPresenceStatus.Busy, AgentPresenceStatus.WrapUp, AgentStateChangeSources.WrapUpStarted),
            State("agent-1", _signInUtc.AddHours(2).AddMinutes(14), AgentPresenceStatus.WrapUp, AgentPresenceStatus.Available, AgentStateChangeSources.WorkCompleted),
            State("agent-1", _signOutUtc, AgentPresenceStatus.Available, AgentPresenceStatus.Offline, AgentStateChangeSources.SignOut),
        ];

    private sealed class FormattingLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
