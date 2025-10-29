using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using CrestApps.OrchardCore.AI.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// ViewModel for editing custom chat instance configuration.
/// </summary>
public class CustomChatInstanceViewModel
{
    /// <summary>
    /// Gets or sets the session ID (empty for new instances).
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the title of the chat instance.
    /// </summary>
    [Required]
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the connection name.
    /// </summary>
    public string ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the deployment ID.
    /// </summary>
    public string DeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the system instructions.
    /// </summary>
    public string SystemMessage { get; set; }

    /// <summary>
    /// Gets or sets the maximum tokens for responses.
    /// </summary>
    [Range(4, int.MaxValue)]
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Gets or sets the temperature parameter.
    /// </summary>
    [Range(0f, 1f)]
    public float? Temperature { get; set; }

    /// <summary>
    /// Gets or sets the Top P parameter.
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
    /// Gets or sets the number of past messages to include.
    /// </summary>
    [Range(2, 20)]
    public int? PastMessagesCount { get; set; }

    /// <summary>
    /// Gets or sets whether to use caching.
    /// </summary>
    public bool UseCaching { get; set; }

    /// <summary>
    /// Gets or sets the selected tools (grouped by category).
    /// </summary>
    public Dictionary<string, ToolEntry[]> Tools { get; set; }

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string ProviderName { get; set; }

    [BindNever]
    public IList<SelectListItem> ConnectionNames { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Deployments { get; set; }

    [BindNever]
    public bool AllowCaching { get; set; }

    [BindNever]
    public bool IsNew { get; set; }
}
