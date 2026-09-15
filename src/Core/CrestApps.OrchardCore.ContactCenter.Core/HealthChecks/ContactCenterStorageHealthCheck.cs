using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

/// <summary>
/// Reports whether the Contact Center persistence store is reachable by issuing a cheap, side-effect-free query.
/// A failure here indicates the tenant database or its Contact Center collection is unavailable.
/// </summary>
public sealed class ContactCenterStorageHealthCheck : IHealthCheck
{
    private readonly IContactCenterOutboxStore _outboxStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterStorageHealthCheck"/> class.
    /// </summary>
    /// <param name="outboxStore">The durable outbox store used as a lightweight storage probe.</param>
    public ContactCenterStorageHealthCheck(IContactCenterOutboxStore outboxStore)
    {
        _outboxStore = outboxStore;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _outboxStore.CountByStatusAsync(OutboxMessageStatus.Completed, cancellationToken);

            return HealthCheckResult.Healthy("Contact Center storage is reachable.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Contact Center storage is unreachable.", ex);
        }
    }
}
