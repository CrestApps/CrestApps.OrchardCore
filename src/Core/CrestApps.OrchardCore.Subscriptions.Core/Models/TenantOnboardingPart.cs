using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class TenantOnboardingPart : ContentPart
{
    public string RecipeName { get; set; }

    public string FeatureProfile { get; set; }
}
