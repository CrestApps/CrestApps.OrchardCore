using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for AI profile template profile fields.
/// </summary>
public class AIProfileTemplateProfileFieldsViewModel
{
    /// <summary>
    /// Gets or sets the welcome message.
    /// </summary>
    public string WelcomeMessage { get; set; }

    /// <summary>
    /// Gets or sets the prompt template.
    /// </summary>
    public string PromptTemplate { get; set; }

    /// <summary>
    /// Gets or sets the prompt subject.
    /// </summary>
    public string PromptSubject { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the profile type.
    /// </summary>
    public AIProfileType? ProfileType { get; set; }

    /// <summary>
    /// Gets or sets the agent availability.
    /// </summary>
    public AgentAvailability? AgentAvailability { get; set; }

    /// <summary>
    /// Gets or sets the title type.
    /// </summary>
    public AISessionTitleType? TitleType { get; set; }

    /// <summary>
    /// Gets or sets the profile types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ProfileTypes { get; set; }

    /// <summary>
    /// Gets or sets the title types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TitleTypes { get; set; }

    /// <summary>
    /// Gets or sets the availability types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AvailabilityTypes { get; set; }
}
