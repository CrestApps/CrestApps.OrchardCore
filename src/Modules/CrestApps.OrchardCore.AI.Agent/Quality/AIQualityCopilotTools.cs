using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Quality;

/// <summary>
/// Sums up how AI conversations scored: pass rate, average score, resolution, sentiment, the most common issues and the
/// questions asked most, so the copilot can say why things went wrong and what to change.
/// </summary>
public sealed class SummarizeAIQualityTool : AICopilotTool
{
    public const string TheName = "summarizeAIQuality";

    public SummarizeAIQualityTool()
        : base("""
            "fromUtc": { "type": "string", "description": "The start, as an ISO 8601 time. Defaults to 7 days ago." },
            "toUtc": { "type": "string", "description": "The end, as an ISO 8601 time. Defaults to now." },
            "label": { "type": "string", "description": "Only evaluations with this label, such as a QA cohort's label or 'Test: name'." }
            """)
    {
    }

    public override string Name => TheName;

    public override string Description => "Sums up the QA evaluations of AI conversations over a period: how many were scored, average score, pass rate, resolution rate, sentiment, the most common issues (such as hallucinations or missed steps), the questions asked most and their resolution rates, and the trend by day.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var now = arguments.Services.GetRequiredService<IClock>().UtcNow;
        var from = ReadDate(arguments, "fromUtc") ?? now.AddDays(-7);
        var to = ReadDate(arguments, "toUtc") ?? now;
        arguments.TryGetFirstString("label", out var label);

        var dashboard = await arguments.Services.GetRequiredService<AIQADashboardService>()
            .BuildAsync(string.IsNullOrWhiteSpace(label) ? null : label.Trim(), from, to, cancellationToken);

        return JsonSerializer.Serialize(new { FromUtc = from, ToUtc = to, Label = label, Dashboard = dashboard });
    }
}

/// <summary>
/// Lists the AI test cases, optionally of one profile.
/// </summary>
public sealed class ListAITestCasesTool : AICopilotTool
{
    public const string TheName = "listAITestCases";

    public ListAITestCasesTool()
        : base("""
            "profileId": { "type": "string", "description": "Only this AI profile's test cases." }
            """)
    {
    }

    public override string Name => TheName;

    public override string Description => "Lists the AI simulation test cases: each one's id, name, profile, scenario and criteria.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        arguments.TryGetFirstString("profileId", out var profileId);
        var testCases = await arguments.Services.GetRequiredService<IAITestCaseStore>().GetAllAsync(cancellationToken);

        return JsonSerializer.Serialize(testCases
            .Where(testCase => string.IsNullOrWhiteSpace(profileId) || testCase.ProfileId == profileId)
            .Select(testCase => new { Id = testCase.ItemId, testCase.Name, testCase.ProfileId, testCase.Scenario, testCase.Criteria, testCase.PassingScore }));
    }
}

/// <summary>
/// Queues AI test cases to run; the results show on the tests page and in the QA summary.
/// </summary>
public sealed class RunAITestsTool : AICopilotTool
{
    public const string TheName = "runAITests";

    public RunAITestsTool()
        : base("""
            "testCaseIds": { "type": "array", "items": { "type": "string" }, "description": "The test cases to run." },
            "repeats": { "type": "integer", "description": "How many times to run each, 1 to 5. Defaults to 1." }
            """)
    {
    }

    public override string Name => TheName;

    public override string Description => "Queues AI simulation test cases to run in the background. Returns the batch id; the runs finish within minutes and show on the AI tests page.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var ids = arguments.TryGetValue("testCaseIds", out var value) ? ReadIds(value) : [];

        if (ids.Count == 0)
        {
            return "Give the testCaseIds to run; listAITestCases lists them.";
        }

        var store = arguments.Services.GetRequiredService<IAITestCaseStore>();
        var testCases = new List<CrestApps.OrchardCore.AI.Core.Models.AITestCase>();

        foreach (var id in ids)
        {
            if (await store.FindByIdAsync(id, cancellationToken) is { } testCase)
            {
                testCases.Add(testCase);
            }
        }

        if (testCases.Count == 0)
        {
            return "None of those test cases exist.";
        }

        var user = arguments.Services.GetService<IHttpContextAccessor>()?.HttpContext?.User;
        var (batchId, runs) = await arguments.Services.GetRequiredService<AITestRunner>()
            .QueueAsync(testCases, ReadInt(arguments, "repeats", 1, 5), user?.FindFirstValue(ClaimTypes.Name) ?? "AI copilot", cancellationToken);

        return JsonSerializer.Serialize(new { BatchId = batchId, Queued = runs.Count });
    }

    private static List<string> ReadIds(object value)
        => value switch
        {
            JsonElement { ValueKind: JsonValueKind.Array } array => array.EnumerateArray().Select(item => item.ToString()).Where(id => !string.IsNullOrWhiteSpace(id)).ToList(),
            JsonElement { ValueKind: JsonValueKind.String } single => [single.GetString()],
            IEnumerable<string> list => list.Where(id => !string.IsNullOrWhiteSpace(id)).ToList(),
            string text => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            _ => [],
        };
}

/// <summary>
/// Lists the AI alert rules and which are open.
/// </summary>
public sealed class ListAIAlertsTool : AICopilotTool
{
    public const string TheName = "listAIAlerts";

    public ListAIAlertsTool()
        : base(string.Empty)
    {
    }

    public override string Name => TheName;

    public override string Description => "Lists the AI alert rules: each one's metric, threshold and window, and whether it is open (crossed) now.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var rules = await arguments.Services.GetRequiredService<IAIAlertRuleStore>().GetAllAsync(cancellationToken);

        return JsonSerializer.Serialize(rules.Select(rule => new
        {
            Id = rule.ItemId,
            rule.Name,
            rule.Enabled,
            Metric = rule.Metric.ToString(),
            Condition = rule.Condition.ToString(),
            rule.Threshold,
            rule.WindowMinutes,
            rule.ProfileId,
            rule.IsOpen,
        }));
    }
}
