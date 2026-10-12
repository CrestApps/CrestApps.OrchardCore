using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Represents tenant provisioning settings attached to a subscription content item.
/// </summary>
public class TenantOnboardingPart : ContentPart
{
    /// <summary>
    /// Gets or sets the setup recipe name used to initialize the provisioned tenant.
    /// </summary>
    public string RecipeName { get; set; }

    /// <summary>
    /// Gets or sets the feature profile applied to the provisioned tenant.
    /// </summary>
    public string FeatureProfile { get; set; }
}
