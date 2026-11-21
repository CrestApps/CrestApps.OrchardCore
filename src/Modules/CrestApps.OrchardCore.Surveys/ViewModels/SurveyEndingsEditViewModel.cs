using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The endings of a survey definition.
/// </summary>
public class SurveyEndingsEditViewModel
{
    /// <summary>
    /// Gets or sets the endings.
    /// </summary>
    public List<SurveyEnding> Endings { get; set; } = [];

    /// <summary>
    /// Gets or sets the condition rule of each ending, as JSON, in the order of <see cref="Endings"/>.
    /// </summary>
    public List<string> EndingRules { get; set; } = [];

    /// <summary>
    /// Gets or sets the id of the default ending.
    /// </summary>
    public string DefaultEndingId { get; set; }

    /// <summary>
    /// Gets or sets the sources ending rules may read, as JSON.
    /// </summary>
    [BindNever]
    public string Sources { get; set; }
}
