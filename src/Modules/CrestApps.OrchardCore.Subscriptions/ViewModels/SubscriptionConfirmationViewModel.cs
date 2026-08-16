using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the data displayed after a subscription checkout session completes.
/// </summary>
public class SubscriptionConfirmationViewModel
{
    /// <summary>
    /// Gets or sets the invoice created for the completed subscription session.
    /// </summary>
    public Invoice Invoice { get; set; }

    /// <summary>
    /// Gets or sets the subscriptions created during the completed session.
    /// </summary>
    public IReadOnlyList<SubscriptionInfo> Subscriptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the tenant onboarding details collected during the completed session.
    /// </summary>
    public TenantOnboardingConfirmationViewModel TenantOnboarding { get; set; }

    /// <summary>
    /// Builds the confirmation view model from a completed subscription session.
    /// The customer's admin password is intentionally never copied into the view model.
    /// </summary>
    /// <param name="session">The completed subscription flow session.</param>
    /// <param name="serializerOptions">The JSON serializer options used to read saved subscription steps.</param>
    /// <returns>The populated subscription confirmation view model.</returns>
    public static SubscriptionConfirmationViewModel Create(ISubscriptionFlowSession session, JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(session);

        var model = new SubscriptionConfirmationViewModel();

        if (session.TryGet<Invoice>(out var invoice))
        {
            model.Invoice = invoice;
        }

        if (session.TryGet<SubscriptionsMetadata>(out var subscriptionsMetadata) && subscriptionsMetadata.Subscriptions is not null)
        {
            model.Subscriptions = subscriptionsMetadata.Subscriptions.ToArray();
        }

        if (session.SavedSteps is not null &&
            session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.TenantOnboarding, out var node) &&
            node is not null)
        {
            var step = node.Deserialize<TenantOnboardingStep>(serializerOptions);

            if (step is not null)
            {
                model.TenantOnboarding = new TenantOnboardingConfirmationViewModel
                {
                    SiteTitle = step.TenantTitle,
                    AdminUsername = step.AdminUsername,
                    AdminEmail = step.AdminEmail,
                    Domains = step.GetDomains(),
                };
            }
        }

        return model;
    }
}

/// <summary>
/// Represents the tenant onboarding information displayed after a subscription is confirmed.
/// </summary>
public class TenantOnboardingConfirmationViewModel
{
    /// <summary>
    /// Gets or sets the title of the provisioned tenant site.
    /// </summary>
    public string SiteTitle { get; set; }

    /// <summary>
    /// Gets or sets the administrator user name for the provisioned tenant.
    /// </summary>
    public string AdminUsername { get; set; }

    /// <summary>
    /// Gets or sets the administrator email address for the provisioned tenant.
    /// </summary>
    public string AdminEmail { get; set; }

    /// <summary>
    /// Gets or sets the domain names configured for the provisioned tenant.
    /// </summary>
    public IReadOnlyList<string> Domains { get; set; } = [];
}
