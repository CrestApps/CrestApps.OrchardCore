using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using static CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports.AuditEvents;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// The presence audit lists one row per state change with no cap, so one busy day was 2.9 MB of HTML and took 21
/// seconds, and a wide period never finished. The browser view is capped like the activity timeline; an export is not.
/// </summary>
public sealed class PresenceAuditRowLimitTests
{
    private static readonly DateTime _startUtc = new(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuildPresenceAudit_InTheBrowser_ListsOnlyTheMostRecentRows_AndSaysSo()
    {
        // Arrange
        var timelines = Timelines(AgentWorkforceReportProvider.PresenceAuditEntryLimit + 250);
        var provider = CreateProvider();

        // Act
        var document = provider.BuildPresenceAudit(timelines, Agents(), _startUtc, _startUtc.AddDays(2), limitRows: true);

        // Assert
        var section = Assert.Single(document.Sections);
        Assert.Equal(AgentWorkforceReportProvider.PresenceAuditEntryLimit, section.Rows.Count);
        Assert.False(string.IsNullOrWhiteSpace(section.Description));
    }

    [Fact]
    public void BuildPresenceAudit_ForAnExport_ListsEveryRow()
    {
        // Arrange
        var timelines = Timelines(AgentWorkforceReportProvider.PresenceAuditEntryLimit + 250);
        var provider = CreateProvider();

        // Act
        var document = provider.BuildPresenceAudit(timelines, Agents(), _startUtc, _startUtc.AddDays(2), limitRows: false);

        // Assert
        var section = Assert.Single(document.Sections);
        Assert.True(section.Rows.Count > AgentWorkforceReportProvider.PresenceAuditEntryLimit);
        Assert.True(string.IsNullOrEmpty(section.Description));
    }

    [Fact]
    public void BuildPresenceAudit_BelowTheLimit_ListsEveryRowWithoutANote()
    {
        // Arrange
        var timelines = Timelines(20);
        var provider = CreateProvider();

        // Act
        var document = provider.BuildPresenceAudit(timelines, Agents(), _startUtc, _startUtc.AddDays(2), limitRows: true);

        // Assert
        var section = Assert.Single(document.Sections);
        Assert.NotEmpty(section.Rows);
        Assert.True(section.Rows.Count <= 20);
        Assert.True(string.IsNullOrEmpty(section.Description));
    }

    // Alternates an agent between available and busy once a second, so every event is a state change.
    private static IReadOnlyList<AgentStateTimeline> Timelines(int changes)
    {
        var events = new List<InteractionEvent>(changes);

        for (var i = 0; i < changes; i++)
        {
            var toBusy = i % 2 == 0;

            events.Add(State(
                "agent-1",
                _startUtc.AddSeconds(i + 1),
                toBusy ? AgentPresenceStatus.Available : AgentPresenceStatus.Busy,
                toBusy ? AgentPresenceStatus.Busy : AgentPresenceStatus.Available,
                toBusy ? AgentStateChangeSources.Accepted : AgentStateChangeSources.WorkCompleted));
        }

        return AgentStateTimeline.Build(events);
    }

    private static Dictionary<string, AgentProfile> Agents()
        => new(StringComparer.Ordinal);

    private static AgentWorkforceReportProvider CreateProvider()
        => new(
            Mock.Of<IInteractionEventStore>(),
            Mock.Of<IAgentProfileManager>(),
            Mock.Of<ICatalogManager<OmnichannelCampaign>>(),
            AgentWorkforceReportKind.PresenceAudit,
            Mock.Of<IContactCenterReportCapabilityGuard>(),
            new FormattingLocalizer<AgentWorkforceReportProvider>(),
            Mock.Of<IClock>(),
            Mock.Of<ILocalClock>(),
            Options.Create(new ContactCenterReportingOptions()),
            summaryReader: null);

    private sealed class FormattingLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
