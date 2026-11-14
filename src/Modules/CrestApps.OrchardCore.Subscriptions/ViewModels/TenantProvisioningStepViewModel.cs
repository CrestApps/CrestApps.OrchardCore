namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// The form a customer fills in to describe the site they are buying.
/// </summary>
public class TenantProvisioningStepViewModel
{
    /// <summary>
    /// Gets or sets the Orchard Core tenant name.
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
    /// Gets or sets the custom domains, separated by commas.
    /// </summary>
    public string Domains { get; set; }

    /// <summary>
    /// Gets or sets the administrator user name.
    /// </summary>
    public string AdminUsername { get; set; }

    /// <summary>
    /// Gets or sets the administrator email address.
    /// </summary>
    public string AdminEmail { get; set; }

    /// <summary>
    /// Gets or sets the administrator password. It is never rendered back into the form.
    /// </summary>
    public string AdminPassword { get; set; }

    /// <summary>
    /// Gets or sets the repeated administrator password.
    /// </summary>
    public string ConfirmPassword { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a password has already been captured, so the form can say the
    /// field may be left blank instead of demanding it again.
    /// </summary>
    public bool HasPassword { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the buyer may choose the URL prefix; otherwise it is the site name.
    /// </summary>
    public bool AllowCustomPrefix { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the buyer may name domains of their own.
    /// </summary>
    public bool AllowCustomDomains { get; set; }
}
