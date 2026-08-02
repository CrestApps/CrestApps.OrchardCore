using CrestApps.OrchardCore.ContactCenter.Core.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Reconciles Contact Center feature-owned state when a fresh tenant shell activates. This is the
/// activation counterpart to the pre-disable quiesce and drain path: a restarted tenant deterministically
/// reopens feature work admission instead of relying on the default state of freshly built singletons.
/// </summary>
/// <remarks>
/// Reconciliation is scheduled to run after activation commits rather than inline, so a slow or hung
/// participant cannot block tenant activation. It is also best-effort: the coordinator swallows and logs
/// participant failures so a single participant fault cannot throw out of the deferred task.
/// </remarks>
internal sealed class ContactCenterFeatureLifecycleActivationHandler : ModularTenantEvents
{
    private readonly IContactCenterScopeExecutor _scopeExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterFeatureLifecycleActivationHandler"/> class.
    /// </summary>
    /// <param name="scopeExecutor">The scope executor used to schedule reconciliation off the activation critical path.</param>
    public ContactCenterFeatureLifecycleActivationHandler(IContactCenterScopeExecutor scopeExecutor)
    {
        _scopeExecutor = scopeExecutor;
    }

    /// <inheritdoc/>
    public override Task ActivatedAsync()
    {
        _scopeExecutor.ScheduleAfterCommit<ContactCenterFeatureLifecycleCoordinator>(
            coordinator => coordinator.ReconcileAsync());

        return Task.CompletedTask;
    }
}
