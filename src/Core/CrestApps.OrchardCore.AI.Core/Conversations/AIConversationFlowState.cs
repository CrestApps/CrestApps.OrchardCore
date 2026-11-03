using System.Text;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Where a conversation stands in its conversation flow: the workflow running it, the stage it is in, and the stages it
/// has been through. Kept on the AI session.
/// </summary>
public sealed class AIConversationFlowState
{
    /// <summary>
    /// Gets or sets the workflow instance running the flow.
    /// </summary>
    public string WorkflowId { get; set; }

    /// <summary>
    /// Gets or sets the stage activity the workflow waits at, or <see langword="null"/> once the flow has finished.
    /// </summary>
    public string StageActivityId { get; set; }

    /// <summary>
    /// Gets or sets the stage's name.
    /// </summary>
    public string StageName { get; set; }

    /// <summary>
    /// Gets or sets what the AI does in this stage.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets where the conversation may go from this stage.
    /// </summary>
    public List<AIConversationFlowOutcome> Outcomes { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the flow has finished.
    /// </summary>
    public bool Finished { get; set; }

    /// <summary>
    /// Gets or sets when the conversation entered the stage.
    /// </summary>
    public DateTime EnteredUtc { get; set; }

    /// <summary>
    /// Gets or sets the stages the conversation went through, oldest first.
    /// </summary>
    public List<AIConversationFlowStep> History { get; set; } = [];
}

/// <summary>
/// A way out of a conversation stage: a name and when to take it.
/// </summary>
public sealed class AIConversationFlowOutcome
{
    /// <summary>
    /// Gets or sets the outcome's name, which the workflow continues by.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets when the AI should take it.
    /// </summary>
    public string When { get; set; }
}

/// <summary>
/// A stage the conversation was in.
/// </summary>
public sealed class AIConversationFlowStep
{
    /// <summary>
    /// Gets or sets the stage's name.
    /// </summary>
    public string StageName { get; set; }

    /// <summary>
    /// Gets or sets the outcome the conversation left it by, or <see langword="null"/> while in it.
    /// </summary>
    public string Outcome { get; set; }

    /// <summary>
    /// Gets or sets when the conversation entered it.
    /// </summary>
    public DateTime EnteredUtc { get; set; }

    /// <summary>
    /// Gets or sets how long moving on from it took the workflow, in milliseconds.
    /// </summary>
    public long? TransitionMs { get; set; }
}

/// <summary>
/// The conversation flow an AI profile runs.
/// </summary>
public sealed class AIProfileFlowSettings
{
    /// <summary>
    /// Gets or sets the workflow type of the flow: one that starts with the <c>AI Conversation Flow Started</c> event.
    /// </summary>
    public string WorkflowTypeId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every stage's instructions are given at once, so a caller who jumps
    /// between tasks is followed, rather than only the current stage's.
    /// </summary>
    public bool Flexible { get; set; }
}

/// <summary>
/// Writes the instructions a conversation flow adds to the AI's system message.
/// </summary>
public static class AIConversationFlowPrompt
{
    /// <summary>
    /// The tool the AI calls to move to the next stage.
    /// </summary>
    public const string ToolName = "moveToStage";

    /// <summary>
    /// The instructions for the current stage.
    /// </summary>
    /// <param name="state">The flow state.</param>
    /// <param name="otherStages">Every stage's name and instructions, in flexible mode; otherwise empty.</param>
    /// <returns>The text to add to the system message, or <see langword="null"/> when the flow has finished.</returns>
    public static string Build(AIConversationFlowState state, IReadOnlyList<(string Name, string Instructions)> otherStages)
    {
        if (state is null || state.Finished || string.IsNullOrEmpty(state.StageName))
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine("## Conversation flow");
        builder.Append("You are in the stage \"").Append(state.StageName).AppendLine("\".");

        if (!string.IsNullOrWhiteSpace(state.Instructions))
        {
            builder.AppendLine(state.Instructions.Trim());
        }

        if (state.Outcomes.Count > 0)
        {
            builder.AppendLine();
            builder.Append("When one of these is true, call the ").Append(ToolName).AppendLine(" tool with its name, then follow what it returns. Do not mention stages to the customer.");

            foreach (var outcome in state.Outcomes)
            {
                builder.Append("- ").Append(outcome.Name);

                if (!string.IsNullOrWhiteSpace(outcome.When))
                {
                    builder.Append(": ").Append(outcome.When.Trim());
                }

                builder.AppendLine();
            }
        }

        if (otherStages is { Count: > 0 })
        {
            builder.AppendLine();
            builder.AppendLine("The other stages of this conversation, for when the customer jumps ahead or back; move with the tool rather than skipping a stage silently:");

            foreach (var (name, instructions) in otherStages.Where(stage => !string.Equals(stage.Name, state.StageName, StringComparison.Ordinal)))
            {
                builder.Append("### ").AppendLine(name);

                if (!string.IsNullOrWhiteSpace(instructions))
                {
                    builder.AppendLine(instructions.Trim());
                }
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Reads <c>name: when</c> lines into outcomes; a line without a colon is a name alone.
    /// </summary>
    /// <param name="text">The lines.</param>
    /// <returns>The outcomes.</returns>
    public static List<AIConversationFlowOutcome> ParseOutcomes(string text)
    {
        var outcomes = new List<AIConversationFlowOutcome>();

        foreach (var line in (text ?? string.Empty).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = line.IndexOf(':');
            var name = (colon > 0 ? line[..colon] : line).Trim();

            if (name.Length > 0 && !outcomes.Exists(outcome => string.Equals(outcome.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                outcomes.Add(new AIConversationFlowOutcome { Name = name, When = colon > 0 ? line[(colon + 1)..].Trim() : null });
            }
        }

        return outcomes;
    }
}
