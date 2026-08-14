using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class SubscriptionConfirmationViewModel
{
    public Invoice Invoice { get; set; }

    public IReadOnlyList<SubscriptionInfo> Subscriptions { get; set; } = [];

    public TenantOnboardingConfirmationViewModel TenantOnboarding { get; set; }

    /// <summary>
    /// Builds the confirmation view model from a completed subscription session.
    /// The customer's admin password is intentionally never copied into the view model.
    /// </summary>
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

public class TenantOnboardingConfirmationViewModel
{
    public string SiteTitle { get; set; }

    public string AdminUsername { get; set; }

    public string AdminEmail { get; set; }

    public IReadOnlyList<string> Domains { get; set; } = [];
}
