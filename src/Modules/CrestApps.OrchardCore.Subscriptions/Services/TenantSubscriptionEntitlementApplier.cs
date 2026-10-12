using CrestApps.OrchardCore.Subscriptions.Core;
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
        ILogger<TenantSubscriptionEntitlementApplier> logger)
    {
        _shellHost = shellHost;
        _shellSettings = shellSettings;
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

        if (!_shellHost.TryGetSettings(tenantName, out var settings))
        {
            // The site has not been created yet, or was removed. Neither is an error here: the provisioning
            // job owns creation, and a removed site has nothing to suspend.
            return;
        }

        // An uninitialized tenant has never been set up, so enabling it would present a setup screen to
        // somebody who bought a finished site.
        if (settings.IsUninitialized() || settings.State == state)
        {
            return;
        }

        settings.State = state;

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
}
