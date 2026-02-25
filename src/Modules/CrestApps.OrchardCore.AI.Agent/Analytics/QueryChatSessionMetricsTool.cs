using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Core.Indexes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Agent.Analytics;

public sealed class QueryChatSessionMetricsTool : AIFunction
{
    public const string TheName = "queryChatSessionMetrics";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "profileId": {
              "type": "string",
              "description": "Optional. Filter metrics to a specific AI profile by its ID."
            },
            "startDateUtc": {
              "type": "string",
              "description": "Optional. Start date in ISO 8601 format (e.g., 2024-01-01T00:00:00Z). Only include sessions that started on or after this date."
            },
            "endDateUtc": {
              "type": "string",
              "description": "Optional. End date in ISO 8601 format (e.g., 2024-12-31T23:59:59Z). Only include sessions that started on or before this date."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description =>
        "Queries aggregated chat session metrics from the analytics index. " +
        "Returns statistics like total sessions, average messages per session, " +
        "resolution rate, average handle time, token usage, rating distribution, " +
        "and breakdowns by hour-of-day and day-of-week. " +
        "Useful for generating charts and reports about chat performance.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var session = arguments.Services.GetRequiredService<ISession>();

        var query = session.QueryIndex<AIChatSessionMetricsIndex>(collection: AIConstants.CollectionName);

        if (arguments.TryGetFirstString("profileId", out var profileId) && !string.IsNullOrWhiteSpace(profileId))
        {
            query = query.Where(x => x.ProfileId == profileId);
        }

        if (arguments.TryGetFirstString("startDateUtc", out var startDateStr)
            && DateTime.TryParse(startDateStr, out var startDate))
        {
            query = query.Where(x => x.SessionStartedUtc >= startDate.ToUniversalTime());
        }

        if (arguments.TryGetFirstString("endDateUtc", out var endDateStr)
            && DateTime.TryParse(endDateStr, out var endDate))
        {
            query = query.Where(x => x.SessionStartedUtc <= endDate.ToUniversalTime());
        }

        var metrics = (await query.ListAsync(cancellationToken)).ToList();

        if (metrics.Count == 0)
        {
            return JsonSerializer.Serialize(new { message = "No session metrics found for the given filters.", totalSessions = 0 });
        }

        var totalSessions = metrics.Count;
        var completedSessions = metrics.Where(m => m.SessionEndedUtc.HasValue).ToList();
        var resolvedSessions = metrics.Count(m => m.IsResolved);
        var authenticatedSessions = metrics.Count(m => m.IsAuthenticated);
        var ratingsPositive = metrics.Count(m => m.UserRating == true);
        var ratingsNegative = metrics.Count(m => m.UserRating == false);
        var ratingsTotal = metrics.Count(m => m.UserRating.HasValue);

        var hourDistribution = metrics
            .GroupBy(m => m.HourOfDay)
            .OrderBy(g => g.Key)
            .Select(g => new { hour = g.Key, count = g.Count() })
            .ToList();

        var dayDistribution = metrics
            .GroupBy(m => m.DayOfWeek)
            .OrderBy(g => g.Key)
            .Select(g => new { dayOfWeek = g.Key, count = g.Count() })
            .ToList();

        var result = new
        {
            totalSessions,
            completedSessions = completedSessions.Count,
            resolutionRate = totalSessions > 0 ? Math.Round((double)resolvedSessions / totalSessions * 100, 1) : 0,
            abandonmentRate = totalSessions > 0 ? Math.Round((double)(totalSessions - completedSessions.Count) / totalSessions * 100, 1) : 0,
            authenticatedRate = totalSessions > 0 ? Math.Round((double)authenticatedSessions / totalSessions * 100, 1) : 0,
            averageMessagesPerSession = totalSessions > 0 ? Math.Round(metrics.Average(m => m.MessageCount), 1) : 0,
            averageHandleTimeSeconds = completedSessions.Count > 0 ? Math.Round(completedSessions.Average(m => m.HandleTimeSeconds), 1) : 0,
            averageResponseLatencyMs = totalSessions > 0 ? Math.Round(metrics.Average(m => m.AverageResponseLatencyMs), 1) : 0,
            totalInputTokens = metrics.Sum(m => m.TotalInputTokens),
            totalOutputTokens = metrics.Sum(m => m.TotalOutputTokens),
            averageInputTokensPerSession = totalSessions > 0 ? Math.Round(metrics.Average(m => m.TotalInputTokens), 1) : 0,
            averageOutputTokensPerSession = totalSessions > 0 ? Math.Round(metrics.Average(m => m.TotalOutputTokens), 1) : 0,
            feedback = new
            {
                totalRatings = ratingsTotal,
                thumbsUp = ratingsPositive,
                thumbsDown = ratingsNegative,
                positiveRate = ratingsTotal > 0 ? Math.Round((double)ratingsPositive / ratingsTotal * 100, 1) : 0,
            },
            hourOfDayDistribution = hourDistribution,
            dayOfWeekDistribution = dayDistribution,
        };

        return JsonSerializer.Serialize(result);
    }
}
