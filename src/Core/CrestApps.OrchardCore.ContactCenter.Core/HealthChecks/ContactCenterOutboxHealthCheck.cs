using CrestApps.Core.ContactCenter.Models;
using CrestApps.Core.ContactCenter.Services;
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
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterOutboxHealthCheck"/> class.
    /// </summary>
    /// <param name="outboxStore">The durable outbox message store.</param>
    /// <param name="options">The configured health-check thresholds.</param>
    /// <param name="timeProvider">The time provider used to select overdue messages.</param>
    public ContactCenterOutboxHealthCheck(
        IContactCenterOutboxStore outboxStore,
        IOptions<ContactCenterHealthCheckOptions> options,
        TimeProvider timeProvider)
    {
        _outboxStore = outboxStore;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deadLettered = await _outboxStore.CountByStatusAsync(OutboxMessageStatus.DeadLettered, cancellationToken);
            var overdue = await _outboxStore.CountOverdueAsync(_timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

            return BacklogHealthEvaluator.Evaluate("Contact Center event outbox", deadLettered, overdue, _options);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Unable to read the Contact Center event outbox.", ex);
        }
    }
}
