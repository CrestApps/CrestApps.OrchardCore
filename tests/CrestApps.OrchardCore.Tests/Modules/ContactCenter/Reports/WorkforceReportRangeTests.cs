using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// An agent workforce report reads every state change of every agent in its period, so a period of several months
/// timed the request out. A period past the configured limit is refused, before anything is read, with a message that
/// says what happened; an export has a limit of its own.
/// </summary>
public sealed class WorkforceReportRangeTests
{
    private static readonly DateTime _from = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TryRefuse_APeriodWithinTheLimit_IsNotRefused()
    {
        // Arrange
        var context = Context(_from, _from.AddDays(31).AddTicks(-1), ReportRunMode.Display);

        // Act
        var refused = WorkforceReportRange.TryRefuse(context, new ContactCenterReportingOptions(), ReportTimeZone.Utc, new PassThroughStringLocalizer<WorkforceReportRangeTests>(), out var refusal);

        // Assert
        Assert.False(refused);
        Assert.Null(refusal);
    }

    [Fact]
    public void TryRefuse_APeriodPastTheLimit_IsRefusedWithAMessageThatNamesTheLimitAndThePeriod()
    {
        // Arrange
        var context = Context(_from, _from.AddDays(45).AddTicks(-1), ReportRunMode.Display);

        // Act
        var refused = WorkforceReportRange.TryRefuse(context, new ContactCenterReportingOptions(), ReportTimeZone.Utc, new PassThroughStringLocalizer<WorkforceReportRangeTests>(), out var refusal);

        // Assert
        Assert.True(refused);
        var section = Assert.Single(refusal.Sections);
        Assert.Equal("This period is too long for this report", section.Title);
        Assert.Contains("at most 31 days", section.Description, StringComparison.Ordinal);
        Assert.Contains("covers 45 days", section.Description, StringComparison.Ordinal);
        Assert.Empty(section.Rows);
    }

    /// <summary>
    /// A calendar month in a zone that changes its clocks during it is the month's days, not the month and an hour.
    /// </summary>
    [Fact]
    public void TryRefuse_AMonthThatCrossesADaylightSavingChange_IsMeasuredOnTheLocalClock()
    {
        // Arrange
        var zone = ReportTimeZone.FromId("America/Los_Angeles");
        var fromUtc = new DateTime(2026, 10, 15, 7, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 11, 15, 8, 0, 0, DateTimeKind.Utc).AddTicks(-1);
        var context = Context(fromUtc, toUtc, ReportRunMode.Display);

        // Act
        var refused = WorkforceReportRange.TryRefuse(context, new ContactCenterReportingOptions(), zone, new PassThroughStringLocalizer<WorkforceReportRangeTests>(), out _);

        // Assert
        Assert.True(toUtc - fromUtc > TimeSpan.FromDays(31));
        Assert.False(refused);
    }

    [Fact]
    public void TryRefuse_AnExport_UsesTheExportLimit()
    {
        // Arrange
        var options = new ContactCenterReportingOptions
        {
            MaximumWorkforceReportRange = TimeSpan.FromDays(7),
            MaximumWorkforceExportRange = TimeSpan.FromDays(62),
        };

        var display = Context(_from, _from.AddDays(31).AddTicks(-1), ReportRunMode.Display);
        var export = Context(_from, _from.AddDays(31).AddTicks(-1), ReportRunMode.Export);
        var longExport = Context(_from, _from.AddDays(70).AddTicks(-1), ReportRunMode.Export);
        var localizer = new PassThroughStringLocalizer<WorkforceReportRangeTests>();

        // Act
        var displayRefused = WorkforceReportRange.TryRefuse(display, options, ReportTimeZone.Utc, localizer, out _);
        var exportRefused = WorkforceReportRange.TryRefuse(export, options, ReportTimeZone.Utc, localizer, out _);
        var longExportRefused = WorkforceReportRange.TryRefuse(longExport, options, ReportTimeZone.Utc, localizer, out var refusal);

        // Assert
        Assert.True(displayRefused);
        Assert.False(exportRefused);
        Assert.True(longExportRefused);
        Assert.Contains("nothing was exported", Assert.Single(refusal.Sections).Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkforceReport_PastTheLimit_IsRefusedBeforeTheLogIsRead()
    {
        // Arrange
        var eventStore = new Mock<IInteractionEventStore>(MockBehavior.Strict);
        var provider = new AgentWorkforceReportProvider(
            eventStore.Object,
            Agents(),
            Mock.Of<ICatalogManager<OmnichannelCampaign>>(),
            AgentWorkforceReportKind.ReadyNotReady,
            Guard(),
            new PassThroughStringLocalizer<AgentWorkforceReportProvider>(),
            Clock(),
            Mock.Of<ILocalClock>(),
            Options.Create(new ContactCenterReportingOptions()),
            summaryReader: null);

        // Act
        var document = await provider.RunAsync(Context(_from, _from.AddDays(60), ReportRunMode.Display), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("This period is too long for this report", Assert.Single(document.Sections).Title);
        eventStore.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ActivityTimeline_PastTheLimit_IsRefusedBeforeTheLogIsRead()
    {
        // Arrange
        var eventStore = new Mock<IInteractionEventStore>(MockBehavior.Strict);
        var provider = new AgentActivityTimelineReportProvider(
            Mock.Of<IContactCenterReportingService>(),
            Guard(),
            eventStore.Object,
            Agents(),
            Clock(),
            Mock.Of<ILocalClock>(),
            Options.Create(new ContactCenterReportingOptions()),
            new PassThroughStringLocalizer<AgentActivityTimelineReportProvider>());

        // Act
        var document = await provider.RunAsync(Context(_from, _from.AddDays(60), ReportRunMode.Display), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("This period is too long for this report", Assert.Single(document.Sections).Title);
        eventStore.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReconciledPayrollTimecard_ExportPastTheExportLimit_IsRefusedBeforeTheLogIsRead()
    {
        // Arrange
        var eventStore = new Mock<IInteractionEventStore>(MockBehavior.Strict);
        var provider = new ReconciledPayrollTimecardReportProvider(
            Mock.Of<IContactCenterReportingService>(),
            Guard(),
            eventStore.Object,
            Agents(),
            Clock(),
            Mock.Of<ILocalClock>(),
            Options.Create(new ContactCenterReportingOptions()),
            new PassThroughStringLocalizer<ReconciledPayrollTimecardReportProvider>());

        // Act
        var document = await provider.RunAsync(Context(_from, _from.AddDays(60), ReportRunMode.Export), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("nothing was exported", Assert.Single(document.Sections).Description, StringComparison.Ordinal);
        eventStore.VerifyNoOtherCalls();
    }

    private static ReportContext Context(DateTime fromUtc, DateTime toUtc, ReportRunMode mode)
    {
        var filter = new ReportFilter();
        filter.SetDateRange(new ReportDateRange { FromUtc = fromUtc, ToUtc = toUtc });

        return new ReportContext(filter, mode);
    }

    private static IContactCenterReportCapabilityGuard Guard()
    {
        var guard = new Mock<IContactCenterReportCapabilityGuard>();
        guard
            .Setup(value => value.GetMissingFeaturesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return guard.Object;
    }

    private static IAgentProfileManager Agents()
    {
        var agents = new Mock<IAgentProfileManager>();
        agents
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<AgentProfile>)[]);

        return agents.Object;
    }

    private static IClock Clock()
        => Mock.Of<IClock>(clock => clock.UtcNow == _from.AddDays(90));
}
