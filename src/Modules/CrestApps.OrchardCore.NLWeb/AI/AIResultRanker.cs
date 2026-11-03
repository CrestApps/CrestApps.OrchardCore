using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.NLWeb.Protocol;
using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.NLWeb.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.NLWeb.AI;

/// <summary>
/// Asks an AI model how well each result answers the question, orders the results by that score, drops the ones that
/// do not answer it, and keeps the model's reason as the result's relevance.
/// </summary>
internal sealed class AIResultRanker : INLWebResultRanker
{
    /// <summary>
    /// The lowest score a result needs to be kept.
    /// </summary>
    internal const int MinimumScore = 25;

    private readonly NLWebChatClientFactory _chatClientFactory;
    private readonly ITemplateService _templateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIResultRanker"/> class.
    /// </summary>
    /// <param name="chatClientFactory">The NLWeb chat client factory.</param>
    /// <param name="templateService">The prompt template service.</param>
    public AIResultRanker(
        NLWebChatClientFactory chatClientFactory,
        ITemplateService templateService)
    {
        _chatClientFactory = chatClientFactory;
        _templateService = templateService;
    }

    /// <inheritdoc/>
    public async Task RankAsync(NLWebAskContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = NLWebSettingsQueryProcessor.GetSettings(context);

        if (!settings.RankResults || context.Results.Count < 2)
        {
            return;
        }

        var client = await _chatClientFactory.CreateAsync(settings);

        if (client is null)
        {
            return;
        }

        var systemPrompt = await _templateService.RenderAsync(NLWebTemplateIds.Rank, cancellationToken: cancellationToken);
        var input = new JsonObject
        {
            ["question"] = context.QueryText,
            ["results"] = JsonNode.Parse(NLWebAIPayloads.DescribeResults(context.Results)),
        };

        var response = await client.GetResponseAsync<NLWebRanking>(
            [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, input.ToJsonString(NLWebJson.SerializerOptions)),
            ],
            JsonSerializerOptions.Web,
            cancellationToken: cancellationToken);

        if (!response.TryGetResult(out var ranking) || ranking?.Items is null || ranking.Items.Count == 0)
        {
            return;
        }

        var ranked = Apply(context.Results, ranking.Items, context.MaxResults);

        context.Results.Clear();

        foreach (var result in ranked)
        {
            context.Results.Add(result);
        }
    }

    /// <summary>
    /// Orders the results by the model's scores. Results the model did not score keep their order after the scored
    /// ones. Results under <see cref="MinimumScore"/> are dropped, unless that would leave no result.
    /// </summary>
    /// <param name="results">The results, in the order the retrievers gave them.</param>
    /// <param name="scores">The model's scores.</param>
    /// <param name="maxResults">The most results to keep when every result scores low.</param>
    /// <returns>The ranked results.</returns>
    internal static IReadOnlyList<NLWebResult> Apply(IList<NLWebResult> results, IEnumerable<NLWebRankedItem> scores, int maxResults)
    {
        var byIndex = new Dictionary<int, NLWebRankedItem>();

        foreach (var score in scores)
        {
            if (score is not null && score.Index >= 0 && score.Index < results.Count)
            {
                byIndex.TryAdd(score.Index, score);
            }
        }

        var ordered = results
            .Select((result, index) => (Result: result, Index: index, Score: byIndex.GetValueOrDefault(index)))
            .OrderByDescending(entry => entry.Score is null ? -1 : entry.Score.Score)
            .ThenBy(entry => entry.Index)
            .ToList();

        foreach (var entry in ordered)
        {
            if (entry.Score is null)
            {
                continue;
            }

            entry.Result.Score = Math.Clamp(entry.Score.Score, 0, 100) / 100d;

            if (!string.IsNullOrWhiteSpace(entry.Score.Reason))
            {
                entry.Result.Relevance = entry.Score.Reason.Trim();
            }
        }

        var kept = ordered
            .Where(entry => entry.Score is null || entry.Score.Score >= MinimumScore)
            .Select(entry => entry.Result)
            .ToList();

        return kept.Count > 0
            ? kept
            : ordered.Take(Math.Max(maxResults, 1)).Select(entry => entry.Result).ToList();
    }
}
