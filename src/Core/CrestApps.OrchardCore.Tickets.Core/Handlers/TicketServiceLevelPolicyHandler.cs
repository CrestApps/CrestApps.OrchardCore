using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Core.Handlers;

/// <summary>
/// Keeps the targets of ticket policies keyed by the priority's id, so renaming a priority (Urgent to P1) doesn't detach
/// its targets. A target written with a priority's name, as a recipe can't know the ids a site gave its priorities, is
/// keyed by that priority's id when the policy is saved.
/// </summary>
public sealed class TicketServiceLevelPolicyHandler : CatalogEntryHandlerBase<ServiceLevelPolicy>
{
    private readonly ICatalog<TicketPriority> _priorities;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketServiceLevelPolicyHandler"/> class.
    /// </summary>
    public TicketServiceLevelPolicyHandler(ICatalog<TicketPriority> priorities)
    {
        _priorities = priorities;
    }

    /// <inheritdoc/>
    public override Task CreatingAsync(CreatingContext<ServiceLevelPolicy> context, CancellationToken cancellationToken = default)
        => KeyByIdAsync(context.Model, cancellationToken);

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<ServiceLevelPolicy> context, CancellationToken cancellationToken = default)
        => KeyByIdAsync(context.Model, cancellationToken);

    /// <summary>
    /// Keys the targets of a ticket policy by priority id.
    /// </summary>
    /// <param name="policy">The policy.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether a target changed.</returns>
    public async Task<bool> KeyByIdAsync(ServiceLevelPolicy policy, CancellationToken cancellationToken = default)
    {
        if (policy is null || !string.Equals(policy.Scope, TicketsConstants.ServiceLevelScope, StringComparison.OrdinalIgnoreCase) || policy.Targets is not { Count: > 0 })
        {
            return false;
        }

        var priorities = await _priorities.GetAllAsync(cancellationToken);
        var changed = false;

        foreach (var target in policy.Targets.Where(target => !string.IsNullOrEmpty(target.Priority)))
        {
            if (priorities.Any(priority => priority.ItemId == target.Priority))
            {
                continue;
            }

            if (priorities.FirstOrDefault(priority => string.Equals(priority.Name?.Trim(), target.Priority.Trim(), StringComparison.OrdinalIgnoreCase)) is { } named)
            {
                target.Priority = named.ItemId;
                changed = true;
            }
        }

        return changed;
    }
}
