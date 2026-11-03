using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.NLWeb.ViewModels;

/// <summary>
/// The editor of the AI settings of NLWeb.
/// </summary>
public class NLWebAISettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether follow-up questions are rewritten with their conversation's context.
    /// </summary>
    public bool RewriteQueries { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an AI model ranks the results.
    /// </summary>
    public bool RankResults { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether callers can ask for a summary.
    /// </summary>
    public bool AllowSummarize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether callers can ask for an answer.
    /// </summary>
    public bool AllowGenerate { get; set; }

    /// <summary>
    /// Gets or sets the AI deployment used, or empty for the default utility deployment.
    /// </summary>
    public string DeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the AI deployments to choose from.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Deployments { get; set; } = [];
}
