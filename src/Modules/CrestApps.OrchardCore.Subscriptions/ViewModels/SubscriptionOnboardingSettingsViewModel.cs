using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class SubscriptionOnboardingSettingsViewModel
{
    /// <summary>
    /// When true, the user is prompted to provide their own domain names.
    /// </summary>
    public bool AllowCustomDomains { get; set; }

    public LocalDomainType LocalDomainType { get; set; }

    /// <summary>
    /// The template can contain a <see cref="SubscriptionOnboardingSettings.TenantKeyVariable"/> or <see cref="SubscriptionOnboardingSettings.CurrentHostVariable"/> variables.
    /// </summary>
    public string LocalDomainTemplate { get; set; }

    public IEnumerable<SelectListItem> LocalDomainTypes { get; set; }
}
