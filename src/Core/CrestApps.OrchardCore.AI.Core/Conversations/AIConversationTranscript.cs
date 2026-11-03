using System.Text;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Turns an AI session's stored prompts into a transcript: the turns a workflow, a summary or an evaluation reads.
/// Prompts the system generated (instructions put to the model as a turn) are left out.
/// </summary>
public static class AIConversationTranscript
{
    /// <summary>
    /// Gets the conversation's turns, oldest first.
    /// </summary>
    /// <param name="prompts">The session's prompts.</param>
    /// <returns>The turns.</returns>
    public static IReadOnlyList<AIConversationTurn> GetTurns(IEnumerable<AIChatSessionPrompt> prompts)
        => (prompts ?? [])
            .Where(prompt => !prompt.IsGeneratedPrompt && !string.IsNullOrWhiteSpace(prompt.Content))
            .OrderBy(prompt => prompt.CreatedUtc)
            .Select(prompt => new AIConversationTurn(
                prompt.Role == ChatRole.User ? AIConversationTurn.CustomerRole : AIConversationTurn.AssistantRole,
                prompt.Content.Trim(),
                prompt.CreatedUtc))
            .ToList();

    /// <summary>
    /// Formats the turns as plain text, one <c>Customer:</c> or <c>Assistant:</c> line per turn.
    /// </summary>
    /// <param name="turns">The turns.</param>
    /// <returns>The transcript.</returns>
    public static string Format(IEnumerable<AIConversationTurn> turns)
    {
        var builder = new StringBuilder();

        foreach (var turn in turns ?? [])
        {
            builder.Append(turn.Role).Append(": ").AppendLine(turn.Text);
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Adds the transcript and the conversation variables to a workflow event's input, as <c>Transcript</c> (text),
    /// <c>Turns</c> (the list) and <c>Variables</c>.
    /// </summary>
    /// <param name="input">The workflow input.</param>
    /// <param name="session">The AI session.</param>
    /// <param name="prompts">The session's prompts, or <see langword="null"/> to add the variables only.</param>
    public static void AddTo(IDictionary<string, object> input, AIChatSession session, IEnumerable<AIChatSessionPrompt> prompts)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (session is not null)
        {
            input["Variables"] = new Dictionary<string, string>(session.GetConversationVariables(), StringComparer.OrdinalIgnoreCase);
        }

        if (prompts is null)
        {
            return;
        }

        var turns = GetTurns(prompts);
        input["Turns"] = turns;
        input["Transcript"] = Format(turns);
    }
}
