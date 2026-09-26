using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

/// <summary>
/// Reports the health of the durable Contact Center event outbox from its dead-letter and overdue backlog counts.
/// </summary>
public sealed class ContactCenterOutboxHealthCheck : IHealthCheck
{
    private readonly IContactCenterOutboxStore _outboxStore;
    private readonly ContactCenterHealthCheckOptions _options;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterOutboxHealthCheck"/> class.
    /// </summary>
    /// <param name="outboxStore">The durable outbox message store.</param>
    /// <param name="options">The configured health-check thresholds.</param>
    /// <param name="clock">The clock used to select overdue messages.</param>
    public ContactCenterOutboxHealthCheck(
        IContactCenterOutboxStore outboxStore,
        IOptions<ContactCenterHealthCheckOptions> options,
        IClock clock)
    {
        _outboxStore = outboxStore;
        _options = options.Value;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deadLettered = await _outboxStore.CountByStatusAsync(OutboxMessageStatus.DeadLettered, cancellationToken);
            var overdue = await _outboxStore.CountOverdueAsync(_clock.UtcNow, cancellationToken);

            return BacklogHealthEvaluator.Evaluate("Contact Center event outbox", deadLettered, overdue, _options);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Unable to read the Contact Center event outbox.", ex);
        }
    }
}
