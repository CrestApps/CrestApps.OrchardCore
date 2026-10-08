using System.Security.Claims;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Withdraws an activity's queued work when the activity leaves the routable set through the CRM: purged,
/// cancelled, completed or failed from the admin, or deleted. Without it the queue item stays Waiting and routing
/// keeps offering an activity that is already finished.
/// </summary>
/// <remarks>
/// The Omnichannel activity manager raises this through its catalog handlers, so the CRM never references the
/// Contact Center. The withdrawal runs after the change commits, in its own scope: it can revoke an offer, which
/// commits as it goes, and doing that inside the CRM's transaction would leave that transaction saving activities
/// it loaded before the commit. The actor is captured now, while the request that made the change is still here.
/// </remarks>
internal sealed class ContactCenterActivityRoutabilityHandler : CatalogEntryHandlerBase<OmnichannelActivity>
{
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterActivityRoutabilityHandler"/> class.
    /// </summary>
    /// <param name="scopeExecutor">The executor that runs the withdrawal after the change commits.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor used to name the user who made the change.</param>
    public ContactCenterActivityRoutabilityHandler(
        IContactCenterScopeExecutor scopeExecutor,
        IHttpContextAccessor httpContextAccessor)
    {
        _scopeExecutor = scopeExecutor;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public override Task UpdatedAsync(UpdatedContext<OmnichannelActivity> context, CancellationToken cancellationToken = default)
    {
        var activity = context.Model;

        if (activity is null || !activity.Status.IsTerminal())
        {
            return Task.CompletedTask;
        }

        var userId = activity.Status == ActivityStatus.Purged && !string.IsNullOrEmpty(activity.PurgedById)
            ? activity.PurgedById
            : CurrentUserId();

        return WithdrawAsync(activity, QueuedWorkWithdrawalReasons.For(activity.Status), userId);
    }

    /// <inheritdoc/>
    public override Task DeletedAsync(DeletedContext<OmnichannelActivity> context, CancellationToken cancellationToken = default)
    {
        var activity = context.Model;

        if (activity is null)
        {
            return Task.CompletedTask;
        }

        return WithdrawAsync(activity, QueuedWorkWithdrawalReasons.ActivityDeleted, CurrentUserId());
    }

    private Task WithdrawAsync(OmnichannelActivity activity, string reason, string userId)
    {
        if (string.IsNullOrEmpty(activity.ItemId))
        {
            return Task.CompletedTask;
        }

        var activityItemId = activity.ItemId;
        var actor = ResolveActor(activity, userId);

        // Nothing to defer to outside a shell scope (a test host, a tool), so the withdrawal runs straight away in
        // a child scope instead.
        if (_scopeExecutor.ScheduleAfterCommit<IQueuedWorkWithdrawalService>(service =>
            service.WithdrawAsync(activityItemId, reason, actor, CancellationToken.None)))
        {
            return Task.CompletedTask;
        }

        return _scopeExecutor.ExecuteAsync<IQueuedWorkWithdrawalService>(service =>
            service.WithdrawAsync(activityItemId, reason, actor, CancellationToken.None));
    }

    /// <summary>
    /// The agent the activity belongs to acts for themselves; anyone else closing it (an admin, a supervisor
    /// managing activities in bulk) acts as a supervisor; with no user it is the platform.
    /// </summary>
    private static ContactCenterActor ResolveActor(OmnichannelActivity activity, string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return ContactCenterActor.System;
        }

        return string.Equals(userId, activity.AssignedToId, StringComparison.Ordinal)
            ? ContactCenterActor.Agent(userId)
            : ContactCenterActor.Supervisor(userId);
    }

    private string CurrentUserId()
        => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
