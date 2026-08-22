using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the editable tenant onboarding step in a subscription flow.
/// </summary>
public class TenantOnboardingStepViewModel
{
    /// <summary>
    /// Gets or sets the custom domain names entered for the tenant.
    /// </summary>
    public string DomainName { get; set; }

    /// <summary>
    /// Gets or sets the tenant shell name used as the site key.
    /// </summary>
    public string TenantName { get; set; }

    /// <summary>
    /// Gets or sets the display title of the tenant site.
    /// </summary>
    public string TenantTitle { get; set; }

    /// <summary>
    /// Gets or sets the username for the tenant administrator account.
    /// </summary>
    public string AdminUsername { get; set; }

    /// <summary>
    /// Gets or sets the email address for the tenant administrator account.
    /// </summary>
    public string AdminEmail { get; set; }

    /// <summary>
    /// Gets or sets the password entered for the tenant administrator account.
    /// </summary>
    public string AdminPassword { get; set; }

    /// <summary>
    /// Gets or sets the password confirmation entered for the tenant administrator account.
    /// </summary>
    public string AdminPasswordConfirmation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether custom tenant domains are allowed.
    /// </summary>
    [BindNever]
    public bool AllowCustomDomain { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the step already has a protected administrator password.
    /// </summary>
    [BindNever]
    public bool HasSavedPassword { get; set; }

    /// <summary>
    /// Gets or sets the local domain template displayed to the subscriber.
    /// </summary>
    [BindNever]
    public string DomainsTemplate { get; set; }
}
