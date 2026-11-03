using System.Text;
using System.Text.Json;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Resilience;
using Microsoft.Extensions.AI;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// What to evaluate a conversation on.
/// </summary>
public sealed class AIConversationEvaluationRequest
{
    /// <summary>
    /// Gets or sets the conversation, oldest turn first.
    /// </summary>
    public IReadOnlyList<AIConversationTurn> Turns { get; set; } = [];

    /// <summary>
    /// Gets or sets the conditions a successful conversation meets, one per entry.
    /// </summary>
    public IReadOnlyList<string> Criteria { get; set; } = [];

    /// <summary>
    /// Gets or sets the lowest score that passes. Defaults to 70.
    /// </summary>
    public int PassingScore { get; set; } = AIConversationEvaluator.DefaultPassingScore;

    /// <summary>
    /// Gets or sets the instructions the AI was given, so the judge can tell when it broke them.
    /// </summary>
    public string Instructions { get; set; }

    /// <summary>
    /// Gets or sets the deployment that judges, or <see langword="null"/> for the site's utility deployment.
    /// </summary>
    public string DeploymentName { get; set; }
}

/// <summary>
/// Scores a conversation against criteria.
/// </summary>
public interface IAIConversationEvaluator
{
    /// <summary>
    /// Evaluates a conversation. The result is not saved.
    /// </summary>
    /// <param name="request">What to evaluate.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The evaluation, or <see langword="null"/> when no deployment can judge or the conversation is empty.</returns>
    Task<AIConversationEvaluation> EvaluateAsync(AIConversationEvaluationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Asks the utility model to judge a conversation and returns its verdict as an <see cref="AIConversationEvaluation"/>.
/// </summary>
public sealed class AIConversationEvaluator : IAIConversationEvaluator
{
    /// <summary>
    /// The passing score when none is given.
    /// </summary>
    public const int DefaultPassingScore = 70;

    private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Web;

    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _clientFactory;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationEvaluator"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="clientFactory">The AI client factory.</param>
    /// <param name="clock">The clock.</param>
    public AIConversationEvaluator(
        IAIDeploymentManager deploymentManager,
        IAIClientFactory clientFactory,
        IClock clock)
    {
        _deploymentManager = deploymentManager;
        _clientFactory = clientFactory;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<AIConversationEvaluation> EvaluateAsync(AIConversationEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Turns is null || !request.Turns.Any(turn => turn.Role == AIConversationTurn.CustomerRole))
        {
            return null;
        }

        var deployment = await _deploymentManager.ResolveUtilityOrDefaultAsync(utilityDeploymentName: request.DeploymentName);

        if (deployment is null)
        {
            return null;
        }

        var client = await _clientFactory.CreateChatClientAsync(deployment, builder => builder
            .UseDefaultResilience()
            .UseUsageLabels(purpose: AIUsageFeaturePurposes.ConversationEvaluation));

        var response = await client.GetResponseAsync<AIConversationJudgeVerdict>(
            [
                new ChatMessage(ChatRole.System, SystemPrompt),
                new ChatMessage(ChatRole.User, BuildRequest(request)),
            ],
            _jsonOptions,
            cancellationToken: cancellationToken);

        return ToEvaluation(response.Result, request, _clock.UtcNow);
    }

    internal const string SystemPrompt =
        """
        You review conversations between a customer and an AI assistant for quality.
        Score the conversation from 0 to 100, where 100 is a conversation a skilled human agent would be proud of.
        Judge each criterion you are given as met or not, with a one-sentence reason.
        List each problem you find, with the exact line of the transcript that shows it. Kinds of problem:
        Unresolved (the customer's need was left open), Hallucination (the assistant stated something it had no
        source for), ToolFailure (an action failed, or the assistant claimed one worked when it did not),
        PolicyViolation (the assistant broke its instructions), Tone, Repetition, Transfer (a handoff went wrong or
        came too late) and Other.
        Say whether the customer's need was resolved, and the customer's sentiment at the end: Positive, Neutral or
        Negative.
        Answer only with JSON: {"score": 0, "resolved": false, "sentiment": "", "summary": "",
        "criteria": [{"name": "", "met": false, "reason": ""}], "issues": [{"kind": "", "detail": "", "quote": ""}]}.
        """;

    /// <summary>
    /// Builds the message the judge reads: the assistant's instructions, the criteria and the transcript.
    /// </summary>
    /// <param name="request">What to evaluate.</param>
    internal static string BuildRequest(AIConversationEvaluationRequest request)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(request.Instructions))
        {
            builder.AppendLine("The assistant's instructions:");
            builder.AppendLine(request.Instructions.Trim());
            builder.AppendLine();
        }

        var criteria = (request.Criteria ?? []).Where(criterion => !string.IsNullOrWhiteSpace(criterion)).ToList();

        if (criteria.Count > 0)
        {
            builder.AppendLine("Criteria (judge every one, in this order, and copy its text exactly as the name):");

            for (var index = 0; index < criteria.Count; index++)
            {
                builder.Append(index + 1).Append(". ").AppendLine(criteria[index].Trim());
            }

            builder.AppendLine();
        }

        builder.AppendLine("Transcript:");
        builder.AppendLine(AIConversationTranscript.Format(request.Turns));

        return builder.ToString();
    }

    // The judge sometimes keeps the number a criterion was listed with: "1. Confirms the order number".
    private static string CriterionName(string name)
    {
        name = name.Trim();
        var dot = name.IndexOf(". ", StringComparison.Ordinal);

        return dot is > 0 and < 4 && name[..dot].All(char.IsDigit) ? name[(dot + 2)..].Trim() : name;
    }

    /// <summary>
    /// Turns the judge's verdict into an evaluation: the score is clamped to 0..100, unknown issue kinds become
    /// <see cref="AIConversationIssueKinds.Other"/>, and a criterion the judge skipped counts as not met.
    /// </summary>
    /// <param name="verdict">The judge's answer.</param>
    /// <param name="request">What was evaluated.</param>
    /// <param name="utcNow">When.</param>
    internal static AIConversationEvaluation ToEvaluation(AIConversationJudgeVerdict verdict, AIConversationEvaluationRequest request, DateTime utcNow)
    {
        verdict ??= new AIConversationJudgeVerdict();

        var evaluation = new AIConversationEvaluation
        {
            Score = Math.Clamp(verdict.Score, 0, 100),
            PassingScore = Math.Clamp(request.PassingScore, 0, 100),
            Resolved = verdict.Resolved,
            Sentiment = NormalizeSentiment(verdict.Sentiment),
            Summary = verdict.Summary?.Trim(),
            EvaluatedUtc = utcNow,
        };

        var judged = (verdict.Criteria ?? [])
            .Where(result => !string.IsNullOrWhiteSpace(result?.Name))
            .ToList();
        var asked = (request.Criteria ?? []).Where(criterion => !string.IsNullOrWhiteSpace(criterion)).ToList();

        for (var index = 0; index < asked.Count; index++)
        {
            var name = asked[index].Trim();
            var result = judged.FirstOrDefault(result => string.Equals(CriterionName(result.Name), name, StringComparison.OrdinalIgnoreCase))
                ?? judged.FirstOrDefault(result => name.Contains(CriterionName(result.Name), StringComparison.OrdinalIgnoreCase)
                    || result.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                ?? judged.FirstOrDefault(result => result.Name.TrimStart().StartsWith((index + 1) + ".", StringComparison.Ordinal))
                // A judge that reworded every name but answered each criterion in order is matched by position.
                ?? (judged.Count == asked.Count ? judged[index] : null);

            evaluation.Criteria.Add(new AIConversationCriterionResult
            {
                Name = name,
                Met = result?.Met ?? false,
                Reason = result?.Reason?.Trim() ?? "Not judged.",
            });
        }

        foreach (var issue in verdict.Issues ?? [])
        {
            if (issue is null || string.IsNullOrWhiteSpace(issue.Detail))
            {
                continue;
            }

            evaluation.Issues.Add(new AIConversationIssue
            {
                Kind = AIConversationIssueKinds.All.FirstOrDefault(kind => string.Equals(kind, issue.Kind?.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? AIConversationIssueKinds.Other,
                Detail = issue.Detail.Trim(),
                Quote = issue.Quote?.Trim(),
            });
        }

        evaluation.Passed = evaluation.Score >= evaluation.PassingScore && evaluation.Criteria.All(criterion => criterion.Met);

        return evaluation;
    }

    private static string NormalizeSentiment(string sentiment)
        => sentiment?.Trim().ToLowerInvariant() switch
        {
            "positive" => "Positive",
            "negative" => "Negative",
            _ => "Neutral",
        };

    /// <summary>
    /// Splits criteria written one per line, ignoring blank lines and list markers.
    /// </summary>
    /// <param name="text">The criteria.</param>
    public static IReadOnlyList<string> ParseCriteria(string text)
        => string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split('\n')
                .Select(line => line.Trim().TrimStart('-', '*', '•').Trim())
                .Where(line => line.Length > 0)
                .ToList();
}

/// <summary>
/// The judge's answer, as the prompt asks for it.
/// </summary>
public sealed class AIConversationJudgeVerdict
{
    public int Score { get; set; }

    public bool Resolved { get; set; }

    public string Sentiment { get; set; }

    public string Summary { get; set; }

    public List<AIConversationCriterionResult> Criteria { get; set; } = [];

    public List<AIConversationIssue> Issues { get; set; } = [];
}
