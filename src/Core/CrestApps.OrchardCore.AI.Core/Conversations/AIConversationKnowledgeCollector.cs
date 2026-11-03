using System.Text;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Finds what an AI profile's knowledge base and data sources hold on what a customer asked, by running the profile's
/// own retrieval for the customer's questions, so a judge can tell an answer from the knowledge base from one the AI
/// made up.
/// </summary>
public sealed class AIConversationKnowledgeCollector
{
    /// <summary>
    /// The most customer questions looked up per conversation.
    /// </summary>
    public const int MaxQuestions = 3;

    /// <summary>
    /// The most characters of knowledge handed to the judge.
    /// </summary>
    public const int MaxLength = 8000;

    private readonly IOrchestrationContextBuilder _orchestrationContextBuilder;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationKnowledgeCollector"/> class.
    /// </summary>
    /// <param name="orchestrationContextBuilder">Builds the profile's context, which runs its retrieval.</param>
    /// <param name="logger">The logger.</param>
    public AIConversationKnowledgeCollector(IOrchestrationContextBuilder orchestrationContextBuilder, ILogger<AIConversationKnowledgeCollector> logger)
    {
        _orchestrationContextBuilder = orchestrationContextBuilder;
        _logger = logger;
    }

    /// <summary>
    /// Collects the knowledge for a conversation.
    /// </summary>
    /// <param name="profile">The profile that answered.</param>
    /// <param name="turns">The conversation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The knowledge, or <see langword="null"/> when the profile retrieves nothing for these questions.</returns>
    public async Task<string> CollectAsync(AIProfile profile, IReadOnlyList<AIConversationTurn> turns, CancellationToken cancellationToken = default)
    {
        if (profile is null || turns is null)
        {
            return null;
        }

        var questions = SelectQuestions(turns);

        if (questions.Count == 0)
        {
            return null;
        }

        try
        {
            // The profile's instructions alone, so only what retrieval adds for a question is kept.
            var baseline = await BuildSystemMessageAsync(profile, null, cancellationToken);
            var knowledge = new StringBuilder();

            foreach (var question in questions)
            {
                var withQuestion = await BuildSystemMessageAsync(profile, question, cancellationToken);
                var added = Added(baseline, withQuestion);

                if (string.IsNullOrWhiteSpace(added))
                {
                    continue;
                }

                knowledge.Append("For \"").Append(question).AppendLine("\":");
                knowledge.AppendLine(added.Trim());
                knowledge.AppendLine();

                if (knowledge.Length >= MaxLength)
                {
                    break;
                }
            }

            return knowledge.Length == 0
                ? null
                : knowledge.ToString(0, Math.Min(knowledge.Length, MaxLength));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The knowledge of AI profile '{ProfileId}' could not be collected; the conversation is judged on its transcript alone.", profile.ItemId.SanitizeLogValue());

            return null;
        }
    }

    /// <summary>
    /// The customer lines worth looking up: the longest few, which are the questions rather than "yes" and "thanks".
    /// </summary>
    /// <param name="turns">The conversation.</param>
    /// <returns>The questions, in the order they were asked.</returns>
    internal static IReadOnlyList<string> SelectQuestions(IReadOnlyList<AIConversationTurn> turns)
    {
        var lines = turns
            .Select((turn, index) => (turn, index))
            .Where(item => item.turn.Role == AIConversationTurn.CustomerRole && item.turn.Text?.Trim().Length >= 12)
            .ToList();

        return lines
            .OrderByDescending(item => item.turn.Text.Length)
            .Take(MaxQuestions)
            .OrderBy(item => item.index)
            .Select(item => item.turn.Text.Trim())
            .ToList();
    }

    /// <summary>
    /// What <paramref name="withQuestion"/> holds beyond <paramref name="baseline"/>.
    /// </summary>
    /// <param name="baseline">The system message without a question.</param>
    /// <param name="withQuestion">The system message built for a question.</param>
    /// <returns>The added text.</returns>
    internal static string Added(string baseline, string withQuestion)
    {
        if (string.IsNullOrEmpty(withQuestion))
        {
            return null;
        }

        if (string.IsNullOrEmpty(baseline))
        {
            return withQuestion;
        }

        return withQuestion.StartsWith(baseline, StringComparison.Ordinal)
            ? withQuestion[baseline.Length..]
            : withQuestion.Replace(baseline, string.Empty, StringComparison.Ordinal);
    }

    private async Task<string> BuildSystemMessageAsync(AIProfile profile, string question, CancellationToken cancellationToken)
    {
        var context = await _orchestrationContextBuilder.BuildAsync(profile, orchestration => orchestration.UserMessage = question, cancellationToken);

        return context?.SystemMessageBuilder.ToString();
    }
}
