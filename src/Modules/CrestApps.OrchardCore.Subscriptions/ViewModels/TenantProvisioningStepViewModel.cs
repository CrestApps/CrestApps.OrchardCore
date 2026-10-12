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
    /// Gets or sets the chosen site template.
    /// </summary>
    public string RecipeName { get; set; }

    /// <summary>
    /// Gets or sets the site templates on offer.
    /// </summary>
    public IList<TenantRecipeOption> Recipes { get; set; } = [];
}

/// <summary>
/// One site template a customer can choose.
/// </summary>
public sealed class TenantRecipeOption
{
    /// <summary>
    /// Gets or sets the recipe name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name shown to the customer.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description shown to the customer.
    /// </summary>
    public string Description { get; set; }
}
