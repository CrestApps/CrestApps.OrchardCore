using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// The guardrails of an AI profile.
/// </summary>
public class EditAIProfileGuardrailViewModel
{
    /// <summary>
    /// Gets or sets whether what the customer says is checked.
    /// </summary>
    public bool CheckCustomer { get; set; }

    /// <summary>
    /// Gets or sets whether the AI's replies are checked.
    /// </summary>
    public bool CheckReplies { get; set; }

    /// <summary>
    /// Gets or sets the prompt templates of the topics refused.
    /// </summary>
    public string[] Topics { get; set; } = [];

    /// <summary>
    /// Gets or sets the topics that can be chosen.
    /// </summary>
    [BindNever]
    public IReadOnlyList<AITemplateOption> AvailableTopics { get; set; } = [];

    /// <summary>
    /// Gets or sets other topics refused.
    /// </summary>
    public string OtherTopics { get; set; }

    /// <summary>
    /// Gets or sets what the AI says instead.
    /// </summary>
    public string PlaceholderReply { get; set; }
}
