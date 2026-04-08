using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Chat;

/// <summary>
/// Provides context for the <see cref="IAIChatSessionHandler.MessageCompletedAsync"/> callback.
/// </summary>
public sealed class ChatMessageCompletedContext
{
    /// <summary>
    /// Gets the AI profile that was used.
    /// </summary>
    public required AIProfile Profile { get; init; }
    /// <summary>
    /// Gets the current chat session.
    /// </summary>
    public required AIChatSession ChatSession { get; init; }
    /// <summary>
    /// Gets the prompts associated with the current chat session.
    /// </summary>
    public required IReadOnlyList<AIChatSessionPrompt> Prompts { get; init; }
    /// <summary>
    /// Gets or sets the time in milliseconds the AI took to generate the response.
    /// </summary>
    public double ResponseLatencyMs { get; init; }
    /// <summary>
    /// Gets or sets the number of input tokens used in this completion.
    /// </summary>
    public int InputTokenCount { get; init; }
    /// <summary>
    /// Gets or sets the number of output tokens generated in this completion.
    /// </summary>
    public int OutputTokenCount { get; init; }

    /// <summary>
    /// Gets a shared item bag that handlers can use to pass host-agnostic results
    /// to later handlers without rerunning the same processing logic.
    /// </summary>
    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>(StringComparer.Ordinal);
}
