using System.Globalization;
using System.Text.Json;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Usage;

/// <summary>
/// Returns AI usage for a period from the usage records the AI Usage Analytics report reads, with the costs that
/// report estimates from each deployment's prices. Every request sent to an AI provider counts, of every kind and from
/// every feature, as in the report's Metered usage table.
/// </summary>
public sealed class QueryAIUsageTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "queryAIUsage";

    private const string Unspecified = "Unspecified";

    private const string Anonymous = "Anonymous";

    private static readonly string[] _groupings = ["model", "deployment", "profile", "connection", "user", "day", "kind"];

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "from": {
          "type": "string",
          "description": "The first day of the period, as a local date (yyyy-MM-dd). Without it the period starts with the first recorded usage."
        },
        "to": {
          "type": "string",
          "description": "The last day of the period, included, as a local date (yyyy-MM-dd). Without it the period runs to now."
        },
        "profile": {
          "type": "string",
          "description": "Only usage of this AI profile, by ID or technical name, from listAIProfiles."
        },
        "groupBy": {
          "type": "string",
          "enum": ["model", "deployment", "profile", "connection", "user", "day", "kind"],
          "description": "How the rows are grouped: by model (the default), deployment, AI profile, provider connection, user, local day, or kind of request (chat, embeddings, images, speech and so on)."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page of rows to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many rows per page, at most 50. Defaults to 20."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns AI usage for a period: requests, input and output tokens, cached, reasoning, audio and image usage, and estimated cost, grouped by model, deployment, profile, connection, user, day or kind of request, with totals. Costs are estimates from the deployments' prices; requests to deployments without prices have no cost. Voice call minutes are on the AI Usage Analytics report.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIModulePermissions.ViewChatAnalytics))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var groupBy = arguments.TryGetFirstString("groupBy", out var groupText) && !string.IsNullOrWhiteSpace(groupText)
            ? groupText.Trim().ToLowerInvariant()
            : "model";

        if (!_groupings.Contains(groupBy, StringComparer.Ordinal))
        {
            return $"Nothing was returned: groupBy must be one of {string.Join(", ", _groupings)}.";
        }

        var localClock = services.GetRequiredService<ILocalClock>();
        DateTime? fromUtc = null;
        DateTime? toUtc = null;

        if (arguments.TryGetFirstString("from", out var from) && !string.IsNullOrWhiteSpace(from))
        {
            if (!TryParseDate(from, out var day))
            {
                return "Nothing was returned: from must be a local date as yyyy-MM-dd.";
            }

            fromUtc = DateTime.SpecifyKind(await localClock.ConvertToUtcAsync(day), DateTimeKind.Utc);
        }

        if (arguments.TryGetFirstString("to", out var to) && !string.IsNullOrWhiteSpace(to))
        {
            if (!TryParseDate(to, out var day))
            {
                return "Nothing was returned: to must be a local date as yyyy-MM-dd.";
            }

            // The last day is included.
            toUtc = DateTime.SpecifyKind(await localClock.ConvertToUtcAsync(day.AddDays(1)), DateTimeKind.Utc);
        }

        if (fromUtc is not null && toUtc is not null && fromUtc >= toUtc)
        {
            return "Nothing was returned: from is after to.";
        }

        var profileManager = services.GetRequiredService<IAIProfileManager>();
        AIProfile profile = null;

        if (arguments.TryGetFirstString("profile", out var profileReference) && !string.IsNullOrWhiteSpace(profileReference))
        {
            profileReference = profileReference.Trim();
            profile = await profileManager.FindByIdAsync(profileReference, cancellationToken)
                ?? await profileManager.FindByNameAsync(profileReference, cancellationToken);

            if (profile is null)
            {
                return $"Nothing was returned: no AI profile has the ID or name '{profileReference}'. Use listAIProfiles to find it.";
            }
        }

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        IEnumerable<AICompletionUsageRecord> records = await services.GetRequiredService<IAICompletionUsageService>().GetAsync(fromUtc, toUtc, cancellationToken);

        if (profile is not null)
        {
            records = records.Where(record => string.Equals(record.ProfileId, profile.ItemId, StringComparison.Ordinal));
        }

        var usage = records.ToList();
        var pricing = await services.GetRequiredService<IAIDeploymentStore>().GetPricingByDeploymentAsync(cancellationToken);
        var timeZone = await localClock.GetLocalTimeZoneAsync();
        var clock = services.GetRequiredService<IClock>();
        var profileTitles = groupBy == "profile"
            ? (await profileManager.GetAllAsync(cancellationToken))
                .Where(item => !string.IsNullOrEmpty(item.ItemId))
                .ToDictionary(item => item.ItemId, AIConfigurationGuard.GetTitle, StringComparer.Ordinal)
            : null;

        string Label(AICompletionUsageRecord record)
            => groupBy switch
            {
                "deployment" => Text(record.DeploymentName),
                "profile" => string.IsNullOrEmpty(record.ProfileId) ? Unspecified : profileTitles.GetValueOrDefault(record.ProfileId) ?? record.ProfileId,
                "connection" => Text(record.ConnectionName),
                "user" => !string.IsNullOrEmpty(record.UserName)
                    ? record.UserName
                    : record.IsAuthenticated && !string.IsNullOrEmpty(record.UserId) ? record.UserId : Anonymous,
                "day" => clock.ConvertToTimeZone(new DateTimeOffset(DateTime.SpecifyKind(record.CreatedUtc, DateTimeKind.Utc)), timeZone)
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                "kind" => AIUsageReport.GetOperationType(record),
                _ => AIUsageReport.GetModelName(record),
            };

        // Rows of different currencies are kept apart, as in the report, so no cost adds up two currencies.
        var rows = usage
            .GroupBy(record => (Label: Label(record), AIUsageCostEstimator.GetPricing(pricing, record)?.Currency))
            .Select(group => Summarize(group.Key.Label, group.Key.Currency, group.ToList(), pricing))
            .ToList();

        rows = groupBy == "day"
            ? rows.OrderBy(row => row.Group, StringComparer.Ordinal).ToList()
            : rows.OrderByDescending(row => row.TotalTokens).ThenByDescending(row => row.Requests).ThenBy(row => row.Group, StringComparer.OrdinalIgnoreCase).ToList();

        var costs = rows
            .Where(row => row.EstimatedCost is not null && !string.IsNullOrEmpty(row.Currency))
            .GroupBy(row => row.Currency, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new { currency = group.Key, amount = Math.Round(group.Sum(row => row.EstimatedCost.Value), 4) })
            .ToList();

        return AgentToolResults.Json(new
        {
            period = new
            {
                from = fromUtc is null ? null : from.Trim(),
                to = toUtc is null ? null : to.Trim(),
                timeZone = timeZone?.TimeZoneId,
            },
            profile = profile is null ? null : new { id = profile.ItemId, title = AIConfigurationGuard.GetTitle(profile) },
            groupBy,
            totals = new
            {
                requests = usage.Count,
                inputTokens = usage.Sum(record => (long)record.InputTokenCount),
                outputTokens = usage.Sum(record => (long)record.OutputTokenCount),
                totalTokens = usage.Sum(record => (long)record.TotalTokenCount),
                estimatedCost = costs,
            },
            items = rows.Skip((page - 1) * pageSize).Take(pageSize).Select(row => row.ToResult()).ToList(),
            total = rows.Count,
            page,
            pageSize,
            truncated = rows.Count > page * pageSize,
            note = usage.Count == 0
                ? "No AI usage was recorded for this period. Usage is recorded only while usage tracking is on."
                : "Costs are estimates from the prices set on each AI deployment; requests to deployments without prices have no cost.",
        });
    }

    private static UsageRow Summarize(string label, string currency, List<AICompletionUsageRecord> records, IReadOnlyDictionary<string, AIDeploymentPricing> pricing)
    {
        var latencySamples = records.Where(record => record.ResponseLatencyMs > 0).ToList();

        return new UsageRow
        {
            Group = label,
            Currency = currency,
            Requests = records.Count,
            InputTokens = records.Sum(record => (long)record.InputTokenCount),
            CachedInputTokens = records.Sum(record => (long)record.CachedInputTokenCount),
            OutputTokens = records.Sum(record => (long)record.OutputTokenCount),
            ReasoningTokens = records.Sum(record => (long)record.ReasoningTokenCount),
            AudioInputTokens = records.Sum(record => (long)record.InputAudioTokenCount),
            AudioOutputTokens = records.Sum(record => (long)record.OutputAudioTokenCount),
            ImageTokens = records.Sum(record => (long)record.InputImageTokenCount + record.OutputImageTokenCount),
            TotalTokens = records.Sum(record => (long)record.TotalTokenCount),
            AudioMinutes = Math.Round(records.Sum(record => record.AudioDurationMs) / 60000d, 1),
            Characters = records.Sum(record => (long)record.CharacterCount),
            Images = records.Sum(record => (long)record.ImageCount),
            AverageLatencyMs = latencySamples.Count > 0 ? Math.Round(latencySamples.Average(record => record.ResponseLatencyMs), 0) : null,
            EstimatedCost = AIUsageCostEstimator.Estimate(records, pricing),
        };
    }

    private static bool TryParseDate(string value, out DateTime day)
        => DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    private static string Text(string value)
        => string.IsNullOrEmpty(value) ? Unspecified : value;

    private sealed class UsageRow
    {
        public string Group { get; init; }

        public string Currency { get; init; }

        public int Requests { get; init; }

        public long InputTokens { get; init; }

        public long CachedInputTokens { get; init; }

        public long OutputTokens { get; init; }

        public long ReasoningTokens { get; init; }

        public long AudioInputTokens { get; init; }

        public long AudioOutputTokens { get; init; }

        public long ImageTokens { get; init; }

        public long TotalTokens { get; init; }

        public double AudioMinutes { get; init; }

        public long Characters { get; init; }

        public long Images { get; init; }

        public double? AverageLatencyMs { get; init; }

        public decimal? EstimatedCost { get; init; }

        public object ToResult()
            => new
            {
                group = Group,
                requests = Requests,
                inputTokens = InputTokens,
                cachedInputTokens = CachedInputTokens,
                outputTokens = OutputTokens,
                reasoningTokens = ReasoningTokens,
                audioInputTokens = AudioInputTokens,
                audioOutputTokens = AudioOutputTokens,
                imageTokens = ImageTokens,
                totalTokens = TotalTokens,
                audioMinutes = AudioMinutes,
                characters = Characters,
                images = Images,
                averageLatencyMs = AverageLatencyMs,
                estimatedCost = EstimatedCost is null ? null : (decimal?)Math.Round(EstimatedCost.Value, 4),
                currency = EstimatedCost is null ? null : Currency,
            };
    }
}
