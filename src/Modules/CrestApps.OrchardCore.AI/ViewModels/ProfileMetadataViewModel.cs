using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for profile metadata.
/// </summary>
public class ProfileMetadataViewModel
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
    /// Gets or sets the max tokens.
    /// </summary>
    [Range(4, int.MaxValue)]
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Gets or sets the past messages count.
    /// </summary>
    [Range(2, 20)]
    public int? PastMessagesCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether use caching.
    /// </summary>
    public bool UseCaching { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether is system message locked.
    /// </summary>
    [BindNever]
    public bool IsSystemMessageLocked { get; set; }

    /// <summary>
    /// Gets or sets the deployments.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Deployments { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether allow caching.
    /// </summary>
    [BindNever]
    public bool AllowCaching { get; set; }
}
