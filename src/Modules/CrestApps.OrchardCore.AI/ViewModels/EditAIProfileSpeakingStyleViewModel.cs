using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// The speaking styles chosen on an AI profile.
/// </summary>
public class EditAIProfileSpeakingStyleViewModel
{
    /// <summary>
    /// Gets or sets the prompt templates of the chosen styles.
    /// </summary>
    public string[] Styles { get; set; } = [];

    /// <summary>
    /// Gets or sets the styles that can be chosen.
    /// </summary>
    [BindNever]
    public IReadOnlyList<AITemplateOption> AvailableStyles { get; set; } = [];
}

