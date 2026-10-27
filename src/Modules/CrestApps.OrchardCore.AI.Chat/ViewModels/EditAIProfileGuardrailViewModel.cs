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
    /// Gets or sets the categories refused.
    /// </summary>
    public string[] Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets other topics refused.
    /// </summary>
    public string OtherTopics { get; set; }

    /// <summary>
    /// Gets or sets what the AI says instead.
    /// </summary>
    public string PlaceholderReply { get; set; }
}
