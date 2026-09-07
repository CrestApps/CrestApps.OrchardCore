using CrestApps.OrchardCore.Subscriptions.Core;

namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// The details a buyer supplies for the site they are purchasing, captured as a checkout step.
/// </summary>
public sealed class TenantProvisioningStep
{
    /// <summary>
    /// Gets or sets the Orchard Core tenant name. It has to be unique across the installation, which is why
    /// it is validated before payment rather than after.
    /// </summary>
    public string TenantName { get; set; }

    /// <summary>
    /// Gets or sets the display title of the new site.
    /// </summary>
    public string TenantTitle { get; set; }

    /// <summary>
    /// Gets or sets the URL prefix the site is reached at.
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// Gets or sets the custom domains the site answers on.
    /// </summary>
    public string[] Domains { get; set; }

    /// <summary>
    /// Gets or sets the administrator user name created during setup.
    /// </summary>
    public string AdminUsername { get; set; }

    /// <summary>
    /// Gets or sets the administrator email address created during setup.
    /// </summary>
    public string AdminEmail { get; set; }

    /// <summary>
    /// Gets or sets the <em>data-protected</em> administrator password.
    /// </summary>
    /// <remarks>
    /// The name carries the protection state on purpose. It is protected by the step editor with
    /// <see cref="SubscriptionConstants.ProtectorPurposes.TenantOnboardingStep"/> and must be unprotected
    /// with the same purpose immediately before setup runs. Handing the protected value to the setup service
    /// creates a site whose administrator can never sign in.
    /// </remarks>
    public string ProtectedAdminPassword { get; set; }

    /// <summary>
    /// Gets or sets the setup recipe used to initialize the site.
    /// </summary>
    public string RecipeName { get; set; }

    /// <summary>
    /// Gets or sets the feature profile applied to the site.
    /// </summary>
    public string FeatureProfile { get; set; }
}
