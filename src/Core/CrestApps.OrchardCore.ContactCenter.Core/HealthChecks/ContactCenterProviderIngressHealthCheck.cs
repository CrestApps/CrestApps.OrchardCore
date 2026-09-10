using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

/// <summary>
/// Reports the health of the durable provider webhook ingress inbox from its dead-letter and overdue backlog counts.
/// </summary>
public sealed class ContactCenterProviderIngressHealthCheck : IHealthCheck
{
    private readonly IProviderWebhookInboxStore _inboxStore;
    private readonly ContactCenterHealthCheckOptions _options;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterProviderIngressHealthCheck"/> class.
    /// </summary>
    /// <param name="inboxStore">The durable provider webhook inbox store.</param>
    /// <param name="options">The configured health-check thresholds.</param>
    /// <param name="clock">The clock used to select overdue messages.</param>
    public ContactCenterProviderIngressHealthCheck(
        IProviderWebhookInboxStore inboxStore,
        IOptions<ContactCenterHealthCheckOptions> options,
        IClock clock)
    {
        _inboxStore = inboxStore;
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
            var deadLettered = await _inboxStore.CountByStatusAsync(ProviderWebhookInboxStatus.DeadLettered, cancellationToken);
            var overdue = await _inboxStore.CountOverdueAsync(_clock.UtcNow, cancellationToken);

            return BacklogHealthEvaluator.Evaluate("Contact Center provider ingress", deadLettered, overdue, _options);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Unable to read the Contact Center provider ingress inbox.", ex);
        }
    }
}
