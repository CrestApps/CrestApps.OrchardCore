using CrestApps.Core.Services;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Core.Handlers;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Tickets.Migrations;

/// <summary>
/// Moves the priority of ticket policy targets and of running ticket timers from the priority's name to its id.
/// </summary>
internal static class TicketPriorityKeys
{
    private const int BatchSize = 200;

    /// <summary>
    /// Rewrites the ticket policies' targets and the running ticket timers.
    /// </summary>
    /// <param name="services">The services of the scope the work runs in.</param>
    /// <returns>A task.</returns>
    public static async Task MoveToIdsAsync(IServiceProvider services)
    {
        var priorities = await services.GetRequiredService<ICatalog<TicketPriority>>().GetAllAsync();
        var byName = priorities
            .Where(priority => !string.IsNullOrWhiteSpace(priority.Name))
            .GroupBy(priority => priority.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().ItemId, StringComparer.OrdinalIgnoreCase);

        if (services.GetService<IServiceLevelPolicyManager>() is { } policies)
        {
            var handler = ActivatorUtilities.CreateInstance<TicketServiceLevelPolicyHandler>(services);

            foreach (var policy in await policies.ListByScopeAsync(TicketsConstants.ServiceLevelScope))
            {
                if (await handler.KeyByIdAsync(policy))
                {
                    await policies.UpdateAsync(policy);
                }
            }
        }

        var session = services.GetRequiredService<ISession>();
        const string kind = TicketsConstants.RecordKind;
        var lastId = 0L;

        while (true)
        {
            var afterId = lastId;
            var timers = (await session.Query<ServiceLevelTimer, ServiceLevelTimerIndex>(index => index.SubjectKind == kind && index.IsActive && index.Id > afterId, ServiceLevelsConstants.CollectionName)
                .OrderBy(index => index.Id)
                .Take(BatchSize)
                .ListAsync()).ToList();

            foreach (var timer in timers)
            {
                if (!string.IsNullOrEmpty(timer.Priority) && byName.TryGetValue(timer.Priority.Trim(), out var id) && id != timer.Priority)
                {
                    timer.Priority = id;
                    await session.SaveAsync(timer, false, ServiceLevelsConstants.CollectionName);
                }
            }

            if (timers.Count < BatchSize)
            {
                break;
            }

            lastId = (await session.QueryIndex<ServiceLevelTimerIndex>(index => index.SubjectKind == kind && index.IsActive && index.Id > afterId, ServiceLevelsConstants.CollectionName)
                .OrderBy(index => index.Id)
                .Skip(BatchSize - 1)
                .FirstOrDefaultAsync())?.Id ?? long.MaxValue;
        }
    }
}
