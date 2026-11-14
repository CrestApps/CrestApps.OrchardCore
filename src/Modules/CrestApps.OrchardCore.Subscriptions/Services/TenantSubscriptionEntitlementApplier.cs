using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Keeps a purchased site running for as long as its subscription is current, and takes it out of service
/// when the subscription is not.
/// </summary>
/// <remarks>
/// A site that keeps serving after the customer stops paying is the expensive failure in selling hosted
/// sites: it costs the owner real money indefinitely and nobody notices until a bill arrives.
///
/// The site is <em>disabled</em>, never removed. A disabled tenant stops serving immediately but keeps every
/// byte of the customer's data, so somebody who pays a late invoice gets their site back exactly as it was.
/// Deleting it would make a billing lapse indistinguishable from a decision to destroy a customer's work,
/// and nothing here is confident enough to make that call.
/// </remarks>
public sealed class TenantSubscriptionEntitlementApplier : ISubscriptionEntitlementApplier
{
    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly ITenantProvisioningJobStore _jobStore;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantSubscriptionEntitlementApplier"/> class.
    /// </summary>
    /// <param name="shellHost">The shell host used to read and update the tenant's state.</param>
    /// <param name="shellSettings">The settings of the tenant this code runs on.</param>
    /// <param name="logger">The logger.</param>
    public TenantSubscriptionEntitlementApplier(
        IShellHost shellHost,
        ShellSettings shellSettings,
        ITenantProvisioningJobStore jobStore,
        ILogger<TenantSubscriptionEntitlementApplier> logger)
    {
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _jobStore = jobStore;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Kind => SubscriptionConstants.EntitlementKinds.Tenant;

    /// <inheritdoc/>
    public Task ApplyAsync(SubscriptionEntitlementContext context)
        => SetStateAsync(context, TenantState.Running);

    /// <inheritdoc/>
    public Task RevokeAsync(SubscriptionEntitlementContext context)
        => SetStateAsync(context, TenantState.Disabled);

    private async Task SetStateAsync(SubscriptionEntitlementContext context, TenantState state)
    {
        var tenantName = context?.Entitlement?.Value;

        if (string.IsNullOrEmpty(tenantName))
        {
            return;
        }

        // Only the default tenant hosts other tenants. Running this anywhere else would try to reach shells
        // the current tenant cannot see.
        if (!_shellSettings.IsDefaultShell())
        {
            return;
        }

        if (string.Equals(tenantName, ShellSettings.DefaultShellName, StringComparison.OrdinalIgnoreCase))
        {
            // Suspending the default tenant would take the whole installation down, including the billing
            // that decided to suspend it.
            _logger.LogWarning("A subscription entitlement named the default tenant. It was ignored.");

            return;
        }

        // The entitlement acts only on a tenant its own provisioning job created. A name typed at checkout, for a site
        // that was never created because the name was taken, must not reach the tenant that already had it.
        if (!await WasCreatedForAsync(context, tenantName))
        {
            _logger.LogWarning("A tenant entitlement of subscription '{SubscriptionId}' names a tenant its provisioning job did not create. It was ignored.", context.Subscription?.ItemId);

            return;
        }

        if (!_shellHost.TryGetSettings(tenantName, out var settings))
        {
            // The site has not been created yet, or was removed. Neither is an error here: the provisioning
            // job owns creation, and a removed site has nothing to suspend.
            return;
        }

        // A site removed and sold again under the same name is another tenant, with another id.
        if (context.Entitlement.Data is not null &&
            context.Entitlement.Data.TryGetValue(SubscriptionConstants.EntitlementData.TenantId, out var tenantId) &&
            !string.IsNullOrEmpty(tenantId) &&
            !string.Equals(tenantId, settings.TenantId, StringComparison.Ordinal))
        {
            _logger.LogWarning("A tenant entitlement of subscription '{SubscriptionId}' names a tenant that was replaced since. It was ignored.", context.Subscription?.ItemId);

            return;
        }

        // An uninitialized tenant has never been set up, so enabling it would present a setup screen to
        // somebody who bought a finished site.
        if (settings.IsUninitialized() || settings.State == state)
        {
            return;
        }

        var suspendedByBilling = string.Equals(settings[SubscriptionConstants.SuspendedBySubscriptionSetting], "true", StringComparison.OrdinalIgnoreCase);

        // Only a site billing took down comes back with billing. One an operator disabled stays disabled, whatever the
        // subscriber does: canceling at period end makes the agreement current, and that used to switch the site back on.
        if (state == TenantState.Running && !suspendedByBilling)
        {
            return;
        }

        settings.State = state;
        settings[SubscriptionConstants.SuspendedBySubscriptionSetting] = state == TenantState.Disabled ? "true" : null;

        await _shellHost.UpdateShellSettingsAsync(settings);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Tenant '{TenantName}' was set to {State} because subscription '{SubscriptionId}' is {Status}.",
                tenantName,
                state,
                context.Subscription.ItemId,
                context.Subscription.Status);
        }
    }

    private async Task<bool> WasCreatedForAsync(SubscriptionEntitlementContext context, string tenantName)
    {
        TenantProvisioningJob job;

        if (context.Entitlement.Data is not null &&
            context.Entitlement.Data.TryGetValue(SubscriptionConstants.EntitlementData.ProvisioningJobId, out var jobId) &&
            !string.IsNullOrEmpty(jobId))
        {
            job = await _jobStore.FindByIdAsync(jobId);

            // After a site is removed its name can be sold again. The tenant answering to the name is then the newest
            // job's, and an entitlement recorded before it carried the tenant's id must not reach it.
            var newest = job is null ? null : await _jobStore.GetByTenantNameAsync(tenantName);

            if (newest is not null && !string.Equals(newest.ItemId, job.ItemId, StringComparison.Ordinal))
            {
                return false;
            }
        }
        else
        {
            // An entitlement recorded before it carried its job: the tenant's job must belong to this subscription.
            job = await _jobStore.GetByTenantNameAsync(tenantName);

            if (job is not null && !string.Equals(job.SubscriptionId, context.Subscription?.ItemId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return job is not null &&
            job.Status == TenantProvisioningStatus.Succeeded &&
            string.Equals(job.TenantName, tenantName, StringComparison.OrdinalIgnoreCase);
    }
}
