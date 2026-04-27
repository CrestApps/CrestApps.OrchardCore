using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for AI profile template parameters.
/// </summary>
public class AIProfileTemplateParametersViewModel
{
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
    /// Gets or sets the max output tokens.
    /// </summary>
    [Range(4, int.MaxValue)]
    public int? MaxOutputTokens { get; set; }

    /// <summary>
    /// Gets or sets the past messages count.
    /// </summary>
    [Range(2, 20)]
    public int? PastMessagesCount { get; set; }
}
