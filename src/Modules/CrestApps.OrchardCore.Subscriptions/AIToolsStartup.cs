using CrestApps.Core.AI;
using CrestApps.OrchardCore.Subscriptions.AI;
using CrestApps.OrchardCore.Subscriptions.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// Registers the subscription agreement AI tools when the AI feature is on. They act for the signed-in caller with
/// the permission of Subscriptions › Agreements.
/// </summary>
[Feature(SubscriptionConstants.Features.Area)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListSubscriptionsTool>(ListSubscriptionsTool.TheName)
            .WithTitle(S["List Subscriptions"])
            .WithDescription(S["Lists subscription agreements with optional status, title search, owner and paging. Use it to find an agreement's id before reading, canceling or pausing it."])
            .WithCategory(S["Subscriptions"])
            .Selectable();

        services.AddCoreAITool<GetSubscriptionTool>(GetSubscriptionTool.TheName)
            .WithTitle(S["Get Subscription"])
            .WithDescription(S["Returns one agreement: owner, plan, amount, billing period, next billing date, lines, entitlements and its history of events. Use it before any change so the user sees what will be affected."])
            .WithCategory(S["Subscriptions"])
            .Selectable();

        services.AddCoreAITool<CancelSubscriptionTool>(CancelSubscriptionTool.TheName)
            .WithTitle(S["Cancel Subscription"])
            .WithDescription(S["Cancels an agreement now or at the end of the paid period and tells the payment provider to stop billing. Cannot be undone; a canceled agreement is not restarted, the customer must subscribe again. It never refunds."])
            .WithCategory(S["Subscriptions"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<SetSubscriptionPausedTool>(SetSubscriptionPausedTool.TheName)
            .WithTitle(S["Pause or Resume Subscription"])
            .WithDescription(S["Pauses billing on an agreement (with a reason) or resumes a paused or past-due one. The change is sent to the payment provider, so resuming means the customer is billed again."])
            .WithCategory(S["Subscriptions"])
            .RequireApproval()
            .Selectable();
    }
}

/// <summary>
/// Registers the site provisioning AI tools when the AI feature is on, with the Subscriptions - Sites feature.
/// </summary>
[Feature(SubscriptionConstants.Features.Tenants)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class TenantProvisioningAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantProvisioningAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TenantProvisioningAIToolsStartup(IStringLocalizer<TenantProvisioningAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListSiteProvisioningJobsTool>(ListSiteProvisioningJobsTool.TheName)
            .WithTitle(S["List Site Provisioning Jobs"])
            .WithDescription(S["Lists the jobs that create sold sites, with their status, attempts, last error, tenant name and site URL, optionally filtered by status. Use it to find paid customers whose site was not created (status Abandoned)."])
            .WithCategory(S["Subscriptions"])
            .Selectable();

        services.AddCoreAITool<ResolveSiteProvisioningJobTool>(ResolveSiteProvisioningJobTool.TheName)
            .WithTitle(S["Resolve Site Provisioning Job"])
            .WithDescription(S["Retries a site provisioning job now (action: retry), or closes an abandoned job whose site was created by hand, with its URL (action: markCreated). Retry is refused once the job no longer holds the administrator password; then create the site with the tenant tools and mark the job created."])
            .WithCategory(S["Subscriptions"])
            .Selectable();
    }
}

/// <summary>
/// Registers the installment plan AI tools when the AI feature is on, with the Subscriptions - Installment Plans
/// feature, which does not need the Subscriptions feature.
/// </summary>
[Feature(SubscriptionConstants.Features.Installments)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class InstallmentPlansAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallmentPlansAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InstallmentPlansAIToolsStartup(IStringLocalizer<InstallmentPlansAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListInstallmentPlansTool>(ListInstallmentPlansTool.TheName)
            .WithTitle(S["List Installment Plans"])
            .WithDescription(S["Lists installment plans with optional status, search and paging, showing customer, total, paid, outstanding and next payment. Use it to find past-due plans or a plan's id."])
            .WithCategory(S["Subscriptions"])
            .Selectable();

        services.AddCoreAITool<GetInstallmentPlanTool>(GetInstallmentPlanTool.TheName)
            .WithTitle(S["Get Installment Plan"])
            .WithDescription(S["Returns one plan with its schedule (each payment's number, due date, amount, status), its history, and, while it waits for its down payment, the admin link where staff take that payment by card."])
            .WithCategory(S["Subscriptions"])
            .Selectable();

        services.AddCoreAITool<CreateInstallmentPlanTool>(CreateInstallmentPlanTool.TheName)
            .WithTitle(S["Create Installment Plan"])
            .WithDescription(S["Creates a draft installment plan for an existing customer (total, down payment, number of payments, frequency, first due date, collection method) and returns the link where staff take the first payment by card. Nothing is charged by this tool; the plan stays a draft until that payment is taken."])
            .WithCategory(S["Subscriptions"])
            .Selectable();

        services.AddCoreAITool<ChargeInstallmentPaymentTool>(ChargeInstallmentPaymentTool.TheName)
            .WithTitle(S["Charge Installment Payment"])
            .WithDescription(S["Charges the customer's saved card for one scheduled payment of a plan now, by payment number, with the payment's amount and currency to confirm it and a new idempotency key. Use it for a due or failed payment; it fails when the plan has no saved card. A charge cannot be undone."])
            .WithCategory(S["Subscriptions"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<CancelInstallmentPlanTool>(CancelInstallmentPlanTool.TheName)
            .WithTitle(S["Cancel Installment Plan"])
            .WithDescription(S["Cancels a plan so payments not yet received are no longer collected. Cannot be undone; payments already received are not refunded. A customer whose plan had started is emailed."])
            .WithCategory(S["Subscriptions"])
            .RequireApproval()
            .Selectable();
    }
}
