using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the editable tenant onboarding settings for subscription flows.
/// </summary>
public class SubscriptionOnboardingSettingsViewModel
{
    /// <summary>
    /// Gets or sets the database provider every provisioned site is created with.
    /// </summary>
    public string DatabaseProvider { get; set; }

    /// <summary>
    /// Gets or sets the connection string provisioned sites are created with.
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the schema provisioned sites are created in.
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// Gets or sets the database providers this installation can create a site on.
    /// </summary>
    public IEnumerable<SelectListItem> DatabaseProviders { get; set; }
}
