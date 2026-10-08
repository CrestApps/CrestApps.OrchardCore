using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.Modules;
using static CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports.AuditEvents;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// A call routed straight to one agent is carried under a synthetic queue id that is not a stored queue. The call
/// handling and queue usage reports printed that raw id as the queue's name; these pin that they call the routing
/// what it is, as the interaction reports already do, while real queues keep their names.
/// </summary>
public sealed class QueueReportNamingTests
{
    private static readonly DateTime _from = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _to = new(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc);
    private static readonly DateTime _queuedUtc = new(2026, 9, 24, 14, 4, 40, DateTimeKind.Utc);

    [Fact]
    public async Task CallHandling_NamesDirectToAgentRoutingAndRealQueues()
    {
        // Arrange
        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.GetByAggregateWindowAsync(
                nameof(Interaction),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Call(ContactCenterConstants.Events.CallQueued, "direct", _queuedUtc, queueId: ContactCenterConstants.DirectRouting.QueueId),
                Call(ContactCenterConstants.Events.CallQueued, "queued", _queuedUtc, queueId: "queue-1"),
            ]);
        eventStore
            .Setup(store => store.GetByAggregateWindowAsync(
                nameof(ActivityReservation),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ActivityQueue { ItemId = "queue-1", Name = "Support" }]);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_to.AddDays(1));

        var provider = new CallHandlingReportProvider(
            new Mock<IContactCenterReportingService>().Object,
            CreateCapabilityGuard(),
            eventStore.Object,
            agentManager.Object,
            queueManager.Object,
            clock.Object,
            new PassThroughStringLocalizer<CallHandlingReportProvider>());

        // Act
        var document = await provider.RunAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        var queueNames = document.Sections
            .Single(section => section.Title == "By queue")
            .Rows
            .Select(row => row.Cells[0])
            .ToArray();

        Assert.Contains("Direct to agent", queueNames);
        Assert.Contains("Support", queueNames);
        Assert.DoesNotContain(ContactCenterConstants.DirectRouting.QueueId, queueNames);
    }

    [Fact]
    public async Task QueueUsage_NamesDirectToAgentRoutingAndRealQueues()
    {
        // Arrange
        var reportingService = new Mock<IContactCenterReportingService>();
        reportingService
            .Setup(service => service.GetQueueUsageAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<ContactCenterReportCriteria>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueUsageReport
            {
                // The reporting service names a queue id with no stored queue by the id itself.
                Rows =
                [
                    new QueueUsageRow
                    {
                        QueueId = ContactCenterConstants.DirectRouting.QueueId,
                        QueueName = ContactCenterConstants.DirectRouting.QueueId,
                        InteractionsHandled = 1,
                    },
                    new QueueUsageRow
                    {
                        QueueId = "queue-1",
                        QueueName = "Support",
                        InteractionsHandled = 1,
                    },
                ],
            });

        var provider = new QueueUsageReportProvider(
            reportingService.Object,
            CreateCapabilityGuard(),
            new PassThroughStringLocalizer<QueueUsageReportProvider>());

        // Act
        var document = await provider.RunAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        var queueNames = document.Sections
            .Single(section => section.Title == "Queues")
            .Rows
            .Select(row => row.Cells[0])
            .ToArray();

        Assert.Contains("Direct to agent", queueNames);
        Assert.Contains("Support", queueNames);
        Assert.DoesNotContain(ContactCenterConstants.DirectRouting.QueueId, queueNames);
    }

    private static IContactCenterReportCapabilityGuard CreateCapabilityGuard()
    {
        var guard = new Mock<IContactCenterReportCapabilityGuard>();
        guard
            .Setup(value => value.GetMissingFeaturesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return guard.Object;
    }

    private static ReportContext CreateContext()
    {
        var filter = new ReportFilter();
        filter.SetDateRange(new ReportDateRange { FromUtc = _from, ToUtc = _to });

        return new ReportContext(filter);
    }
}
