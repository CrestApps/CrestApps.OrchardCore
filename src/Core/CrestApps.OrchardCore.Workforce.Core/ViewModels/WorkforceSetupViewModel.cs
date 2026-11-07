namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The Get Started checklist.
/// </summary>
public sealed class WorkforceSetupViewModel
{
    /// <summary>
    /// Gets the steps, in order.
    /// </summary>
    public IList<WorkforceSetupStep> Steps { get; } = [];

    /// <summary>
    /// Gets or sets whether the sample recipe can be run from the recipes page.
    /// </summary>
    public bool HasRecipes { get; set; }
}

