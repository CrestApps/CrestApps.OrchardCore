using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the editable tenant onboarding settings for subscription flows.
/// </summary>
public class SubscriptionOnboardingSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether subscribers may provide custom tenant domains.
    /// </summary>
    public bool AllowCustomDomains { get; set; }

    /// <summary>
    /// Gets or sets how local tenant domains are supplied during onboarding.
    /// </summary>
    public LocalDomainType LocalDomainType { get; set; }

    /// <summary>
    /// Gets or sets the template used to generate local tenant domains.
    /// </summary>
    /// <remarks>
    /// The template can contain the <see cref="SubscriptionOnboardingSettings.TenantKeyVariable"/> and
    /// <see cref="SubscriptionOnboardingSettings.CurrentHostVariable"/> placeholders.
    /// </remarks>
    public string LocalDomainTemplate { get; set; }

    /// <summary>
    /// Gets or sets the available local domain generation options.
    /// </summary>
    public IEnumerable<SelectListItem> LocalDomainTypes { get; set; }
}
