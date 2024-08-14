using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class TenantOnboardingStepViewModel
{
    public string DomainName { get; set; }

    public string TenantName { get; set; }

    public string TenantTitle { get; set; }

    public string AdminUsername { get; set; }

    public string AdminEmail { get; set; }

    public string AdminPassword { get; set; }

    public string AdminPasswordConfirmation { get; set; }

    [BindNever]
    public bool AllowCustomDomain { get; set; }

    [BindNever]
    public string DomainsTemplate { get; set; }
}
