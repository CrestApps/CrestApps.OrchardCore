using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// Represents the view model for AI completion with config task.
/// </summary>
public class AICompletionWithConfigTaskViewModel
{
    /// <summary>
    /// Gets or sets the deployment name.
    /// </summary>
    public string DeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the prompt template.
    /// </summary>
    public string PromptTemplate { get; set; }

    /// <summary>
    /// Gets or sets the result property name.
    /// </summary>
    public string ResultPropertyName { get; set; }

    /// <summary>
    /// Gets or sets the system message.
    /// </summary>
    public string SystemMessage { get; set; }

    /// <summary>
    /// Gets or sets the temperature.
    /// </summary>
    [Range(0f, 1f)]
    public float? Temperature { get; set; }

    /// <summary>
    /// Gets or sets the top p.
    /// </summary>
    [Range(0f, 1f)]
    public float? TopP { get; set; }

    /// <summary>
    /// Gets or sets the frequency penalty.
    /// </summary>
    [Range(0f, 1f)]
    public float? FrequencyPenalty { get; set; }

    /// <summary>
    /// Gets or sets the presence penalty.
    /// </summary>
    [Range(0f, 1f)]
    public float? PresencePenalty { get; set; }

    /// <summary>
    /// Gets or sets the max tokens.
    /// </summary>
    [Range(4, int.MaxValue)]
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Gets or sets the deployment names.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> DeploymentNames { get; set; }
}
