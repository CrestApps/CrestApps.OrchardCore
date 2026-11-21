using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// The editor of a Surveys workflow event.
/// </summary>
public class SurveyEventActivityViewModel
{
    /// <summary>
    /// Gets or sets the ids of the surveys chosen.
    /// </summary>
    public string[] SurveyIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the kinds, separated by commas.
    /// </summary>
    public string Kinds { get; set; }

    /// <summary>
    /// Gets or sets the collector ids, separated by commas.
    /// </summary>
    public string CollectorIds { get; set; }

    /// <summary>
    /// Gets or sets the context kinds, separated by commas.
    /// </summary>
    public string ContextKinds { get; set; }

    /// <summary>
    /// Gets or sets whether a change a workflow made starts the workflow too.
    /// </summary>
    public bool IncludeWorkflowChanges { get; set; }

    /// <summary>
    /// Gets or sets the page, ending or quota names, separated by commas, for the events that filter by them.
    /// </summary>
    public string Names { get; set; }

    /// <summary>
    /// Gets or sets the rule of the matches-condition event, as JSON.
    /// </summary>
    public string RuleJson { get; set; }

    /// <summary>
    /// Gets or sets whether the event is about responses (and has the response filters).
    /// </summary>
    [BindNever]
    public bool IsResponseEvent { get; set; }

    /// <summary>
    /// Gets or sets the surveys to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> SurveyOptions { get; set; } = [];
}
