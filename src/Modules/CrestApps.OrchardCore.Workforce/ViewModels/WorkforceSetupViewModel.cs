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

/// <summary>
/// A step of the Get Started checklist.
/// </summary>
public sealed class WorkforceSetupStep
{
    /// <summary>
    /// Gets or sets the step's title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets what the step is for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets what is there now, such as "3 teams".
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets whether the step is done.
    /// </summary>
    public bool Done { get; set; }

    /// <summary>
    /// Gets or sets whether the step's feature is off.
    /// </summary>
    public bool FeatureOff { get; set; }

    /// <summary>
    /// Gets or sets the button's text.
    /// </summary>
    public string Action { get; set; }

    /// <summary>
    /// Gets or sets where the button goes.
    /// </summary>
    public string Url { get; set; }
}
