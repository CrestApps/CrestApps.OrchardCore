using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the setup step of the "New AI profile" wizard, which asks only what is needed to create a
/// profile from the chosen scenario.
/// </summary>
public class ProfileWizardSetupViewModel
{
    /// <summary>
    /// Gets or sets the profile title.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the profile's technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the chat deployment, or an empty value to use the site default.
    /// </summary>
    public string ChatDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the scenario the profile is created from.
    /// </summary>
    [BindNever]
    public ProfileScenarioCardViewModel Scenario { get; set; }

    /// <summary>
    /// Gets or sets the chat deployments that can be chosen.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> ChatDeployments { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the site has no default chat deployment for an empty
    /// selection to fall back to.
    /// </summary>
    [BindNever]
    public bool ShowMissingDefaultChatDeploymentWarning { get; set; }
}
