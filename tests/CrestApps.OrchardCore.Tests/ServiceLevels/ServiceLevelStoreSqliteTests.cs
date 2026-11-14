using CrestApps.Core;
using CrestApps.OrchardCore.BusinessHours.Core;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.ServiceLevels.Core;
using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using CrestApps.OrchardCore.ServiceLevels.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Tests.ServiceLevels;

/// <summary>
/// The Service Levels stores on a real database: the index schema the migration creates holds policies and timers,
/// policies are found by scope and name, a record's timers come back newest first, and the monitor's queries find the
/// timers that are due.
/// </summary>
public sealed class ServiceLevelStoreSqliteTests
{
    private static readonly RecordRef _ticket = new("Ticket", "t1");

    [Fact]
    public async Task PoliciesAndTimers_AreStoredAndQueried_ThroughTheIndexes()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddOpenHours();
                services.AddServiceLevelsCore();
                services.TryAddEnumerable(ServiceDescriptor.Scoped<CrestApps.Core.Services.ICatalogEntryHandler<CrestApps.OrchardCore.ServiceLevels.Models.ServiceLevelPolicy>, CrestApps.OrchardCore.ServiceLevels.Core.Handlers.ServiceLevelPolicyHandler>());
            },
            ServiceLevelsIndexSchema.CreateAsync,
            [new ServiceLevelPolicyIndexProvider(), new ServiceLevelTimerIndexProvider()],
            [ServiceLevelsConstants.CollectionName]);

        var now = CalendarTestHarness.Now;

        var policyId = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IServiceLevelPolicyManager>();
            var policy = await manager.NewAsync();

            policy.Name = "Standard";
            policy.Scope = "Tickets";
            policy.Targets =
            [
                new ServiceLevelTarget { Metric = ServiceLevelMetrics.FirstResponse, Minutes = 60 },
                new ServiceLevelTarget { Metric = ServiceLevelMetrics.Resolution, Minutes = 600 },
            ];

            await manager.CreateAsync(policy);

            return policy.ItemId;
        });

        // Act
        await harness.RunAsync(async services =>
        {
            var tracker = services.GetRequiredService<IServiceLevelTracker>();

            await tracker.StartAsync(new ServiceLevelStart { Subject = _ticket, Metric = ServiceLevelMetrics.FirstResponse, PolicyId = policyId, StartUtc = now.AddMinutes(-90) });
            await tracker.StartAsync(new ServiceLevelStart { Subject = _ticket, Metric = ServiceLevelMetrics.Resolution, PolicyId = policyId, StartUtc = now.AddMinutes(-10) });
        });

        // Assert
        await harness.RunAsync(async services =>
        {
            var policies = services.GetRequiredService<IServiceLevelPolicyManager>();
            var store = services.GetRequiredService<IServiceLevelTimerStore>();

            Assert.Equal(policyId, (await policies.FindByNameAsync("tickets", "standard"))?.ItemId);
            Assert.Null(await policies.FindByNameAsync("WorkOrders", "Standard"));
            Assert.Single(await policies.ListByScopeAsync("Tickets"));

            var timers = await store.ListBySubjectAsync(_ticket, activeOnly: true);

            Assert.Equal([ServiceLevelMetrics.Resolution, ServiceLevelMetrics.FirstResponse], timers.Select(timer => timer.Metric));
            Assert.Equal(_ticket, timers[0].Subject);

            var breached = await store.ListNewlyBreachedAsync(now, 10);
            var warned = await store.ListNewlyWarnedAsync(now, 10);

            Assert.Equal(ServiceLevelMetrics.FirstResponse, Assert.Single(breached).Metric);
            Assert.Empty(warned);
            Assert.Empty(await store.ListBySubjectAsync(new RecordRef("Ticket", "other"), activeOnly: false));
        }, commit: false);

        // The monitor marks the breach once, through the concurrency-checked save.
        var raised = await harness.RunAsync(services => services.GetRequiredService<ServiceLevelMonitor>().CheckAsync());
        var raisedAgain = await harness.RunAsync(services => services.GetRequiredService<ServiceLevelMonitor>().CheckAsync());

        Assert.Equal(1, raised);
        Assert.Equal(0, raisedAgain);
    }
}
