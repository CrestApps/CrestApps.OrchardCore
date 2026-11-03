using System.Text.RegularExpressions;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Indexes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using YesSql;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// The figures of the QA dashboard, read from the conversation evaluations of a period.
/// </summary>
public sealed partial class AIQADashboardService
{
    /// <summary>
    /// The most evaluations one dashboard reads.
    /// </summary>
    public const int MaxEvaluations = 2000;

    /// <summary>
    /// The most recent evaluations whose first customer line is read for the top questions.
    /// </summary>
    public const int QuestionSample = 200;

    private readonly ISession _session;
    private readonly YesSqlStoreOptions _storeOptions;
    private readonly IAIChatSessionPromptStore _promptStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIQADashboardService"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="storeOptions">The YesSql store options, which name the AI collection.</param>
    /// <param name="promptStore">The chat session prompt store, for the customers' questions.</param>
    public AIQADashboardService(ISession session, IOptions<YesSqlStoreOptions> storeOptions, IAIChatSessionPromptStore promptStore)
    {
        _session = session;
        _storeOptions = storeOptions.Value;
        _promptStore = promptStore;
    }

    /// <summary>
    /// Builds the dashboard of a period.
    /// </summary>
    /// <param name="label">The evaluations' label, such as a QA cohort's, or <see langword="null"/> for every evaluation.</param>
    /// <param name="fromUtc">The start of the period.</param>
    /// <param name="toUtc">The end of the period.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The figures.</returns>
    public async Task<AIQADashboard> BuildAsync(string label, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrEmpty(label)
            ? _session.Query<AIConversationEvaluation, AIConversationEvaluationIndex>(index => index.EvaluatedUtc >= fromUtc && index.EvaluatedUtc < toUtc, _storeOptions.AICollectionName)
            : _session.Query<AIConversationEvaluation, AIConversationEvaluationIndex>(index => index.Label == label && index.EvaluatedUtc >= fromUtc && index.EvaluatedUtc < toUtc, _storeOptions.AICollectionName);

        var evaluations = (await query
            .OrderByDescending(index => index.EvaluatedUtc)
            .Take(MaxEvaluations)
            .ListAsync(cancellationToken)).ToList();

        var dashboard = Summarize(evaluations);

        dashboard.TopQuestions = await TopQuestionsAsync(evaluations.Take(QuestionSample).ToList());

        return dashboard;
    }

    /// <summary>
    /// The figures that come from the evaluations alone.
    /// </summary>
    /// <param name="evaluations">The evaluations, newest first.</param>
    /// <returns>The dashboard without the top questions.</returns>
    internal static AIQADashboard Summarize(IReadOnlyList<AIConversationEvaluation> evaluations)
    {
        var dashboard = new AIQADashboard
        {
            Count = evaluations.Count,
        };

        if (evaluations.Count == 0)
        {
            return dashboard;
        }

        dashboard.AverageScore = evaluations.Average(evaluation => evaluation.Score);
        dashboard.PassRate = (double)evaluations.Count(evaluation => evaluation.Passed) / evaluations.Count;
        dashboard.ResolutionRate = (double)evaluations.Count(evaluation => evaluation.Resolved) / evaluations.Count;
        dashboard.Sentiments = evaluations
            .GroupBy(evaluation => string.IsNullOrEmpty(evaluation.Sentiment) ? "Neutral" : evaluation.Sentiment)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        dashboard.Issues = evaluations
            .SelectMany(evaluation => evaluation.Issues ?? [])
            .GroupBy(issue => string.IsNullOrEmpty(issue.Kind) ? AIConversationIssueKinds.Other : issue.Kind)
            .Select(group => new AIQAIssueCount(group.Key, group.Count()))
            .OrderByDescending(issue => issue.Count)
            .ToList();
        dashboard.Days = evaluations
            .GroupBy(evaluation => evaluation.EvaluatedUtc.Date)
            .OrderBy(group => group.Key)
            .Select(group => new AIQADay(
                group.Key,
                group.Count(),
                group.Average(evaluation => evaluation.Score),
                (double)group.Count(evaluation => evaluation.Passed) / group.Count()))
            .ToList();
        dashboard.Worst = evaluations
            .OrderBy(evaluation => evaluation.Score)
            .ThenByDescending(evaluation => evaluation.EvaluatedUtc)
            .Take(10)
            .ToList();

        return dashboard;
    }

    /// <summary>
    /// Folds a customer's opening line into a key that groups the same question asked in different words.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The key.</returns>
    internal static string QuestionKey(string line)
    {
        var words = NonWordRegex().Replace((line ?? string.Empty).ToLowerInvariant(), " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 2 && !_stopWords.Contains(word))
            .Take(6);

        return string.Join(' ', words);
    }

    private async Task<IReadOnlyList<AIQAQuestion>> TopQuestionsAsync(IReadOnlyList<AIConversationEvaluation> evaluations)
    {
        var questions = new List<(string Key, string Text, bool Resolved)>();

        foreach (var evaluation in evaluations.Where(evaluation => !string.IsNullOrEmpty(evaluation.SessionId)))
        {
            var prompts = await _promptStore.GetPromptsAsync(evaluation.SessionId);
            var first = prompts.FirstOrDefault(prompt => prompt.Role == ChatRole.User && !prompt.IsGeneratedPrompt && !string.IsNullOrWhiteSpace(prompt.Content))?.Content?.Trim();
            var key = QuestionKey(first);

            if (key.Length > 0)
            {
                questions.Add((key, first, evaluation.Resolved));
            }
        }

        return questions
            .GroupBy(question => question.Key)
            .Select(group => new AIQAQuestion(group.First().Text, group.Count(), (double)group.Count(question => question.Resolved) / group.Count()))
            .OrderByDescending(question => question.Count)
            .Take(10)
            .ToList();
    }

    private static readonly HashSet<string> _stopWords = new(StringComparer.Ordinal)
    {
        "the", "and", "for", "you", "your", "are", "can", "could", "would", "with", "have", "has", "hello", "please", "want", "need", "this", "that", "about", "how", "what", "there",
    };

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NonWordRegex();
}

/// <summary>
/// The figures of the QA dashboard.
/// </summary>
public sealed class AIQADashboard
{
    /// <summary>
    /// Gets or sets how many conversations were scored.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the average score.
    /// </summary>
    public double? AverageScore { get; set; }

    /// <summary>
    /// Gets or sets the share that passed.
    /// </summary>
    public double? PassRate { get; set; }

    /// <summary>
    /// Gets or sets the share whose customer's need was resolved.
    /// </summary>
    public double? ResolutionRate { get; set; }

    /// <summary>
    /// Gets or sets how many ended in each sentiment.
    /// </summary>
    public Dictionary<string, int> Sentiments { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets how often each kind of issue was found.
    /// </summary>
    public IReadOnlyList<AIQAIssueCount> Issues { get; set; } = [];

    /// <summary>
    /// Gets or sets the figures of each day.
    /// </summary>
    public IReadOnlyList<AIQADay> Days { get; set; } = [];

    /// <summary>
    /// Gets or sets the lowest-scored conversations.
    /// </summary>
    public IReadOnlyList<AIConversationEvaluation> Worst { get; set; } = [];

    /// <summary>
    /// Gets or sets the most frequent opening questions, with how often they were resolved.
    /// </summary>
    public IReadOnlyList<AIQAQuestion> TopQuestions { get; set; } = [];
}

/// <summary>
/// How often a kind of issue was found.
/// </summary>
/// <param name="Kind">The kind.</param>
/// <param name="Count">How often.</param>
public sealed record AIQAIssueCount(string Kind, int Count);

/// <summary>
/// The figures of one day.
/// </summary>
/// <param name="Date">The day.</param>
/// <param name="Count">How many were scored.</param>
/// <param name="AverageScore">Their average score.</param>
/// <param name="PassRate">The share that passed.</param>
public sealed record AIQADay(DateTime Date, int Count, double AverageScore, double PassRate);

/// <summary>
/// A frequent opening question.
/// </summary>
/// <param name="Example">One way it was asked.</param>
/// <param name="Count">How often.</param>
/// <param name="ResolutionRate">The share resolved.</param>
public sealed record AIQAQuestion(string Example, int Count, double ResolutionRate);
