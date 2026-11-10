using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.AI.Core.Telemetry;

/// <summary>
/// The OpenTelemetry instruments of the CrestApps Orchard Core AI modules, on the meter and activity source named
/// <c>CrestApps.OrchardCore.AI</c>. They cover the work only the Orchard Core modules do: syncing data sources into a
/// knowledge base index, conversation evaluations, test runs, QA cohorts and alert rules, signed webhooks, tool time
/// limits, the completion API and the recipes an AI tool imports.
/// </summary>
/// <remarks>
/// The AI pipeline itself (chat turns, orchestration, completions, tools, retrieval, ingestion, usage, MCP, A2A and
/// realtime voice) is measured once, by CrestApps.Core, on the <c>CrestApps.Core</c> meter. Nothing here repeats it.
/// The tags are bounded: profile, session, data source, rule, cohort and test ids go on spans only, and free text
/// such as event names, labels, criteria and tool instance names never becomes a tag.
/// </remarks>
public static class AIModuleTelemetry
{
    /// <summary>
    /// The area of the meter and activity source: <c>CrestApps.OrchardCore.AI</c>.
    /// </summary>
    public const string Area = "AI";

    /// <summary>
    /// The tag that records the provider of a knowledge base index, such as <c>Elasticsearch</c> or <c>AzureAISearch</c>.
    /// </summary>
    public const string ProviderTag = CrestAppsOrchardCoreTelemetry.ProviderTag;

    /// <summary>
    /// The tag that records whether a data source sync read every document (<see cref="ModeFull"/>) or only the
    /// documents that changed (<see cref="ModeIncremental"/>), or whether a recipe import ran inline or deferred.
    /// </summary>
    public const string ModeTag = "mode";

    /// <summary>
    /// The tag that records what a data source sync did with a source document: <see cref="ActionIndexed"/>,
    /// <see cref="ActionFailed"/> or <see cref="ActionRemoved"/>.
    /// </summary>
    public const string ActionTag = "action";

    /// <summary>
    /// The tag that records the kind of an evaluation: <see cref="KindJudge"/>, <see cref="KindTestRun"/> or
    /// <see cref="KindQACohort"/>.
    /// </summary>
    public const string KindTag = "kind";

    /// <summary>
    /// The tag that records the conversation metric an alert rule watches, from <c>AIConversationMetric</c>.
    /// </summary>
    public const string MetricTag = "metric";

    /// <summary>
    /// The tag that records where a tool comes from: <c>local</c>, <c>mcp</c>, <c>agent</c>, <c>a2a</c>,
    /// <c>system</c> or <c>unknown</c>, the values CrestApps.Core uses on <c>crestapps.core.ai.tool.duration</c>.
    /// </summary>
    public const string ToolSourceTag = "tool.source";

    /// <summary>
    /// A sync that read every document of the data source.
    /// </summary>
    public const string ModeFull = "full";

    /// <summary>
    /// A sync of the source documents that changed.
    /// </summary>
    public const string ModeIncremental = "incremental";

    /// <summary>
    /// A recipe import that ran while the tool was called.
    /// </summary>
    public const string ModeInline = "inline";

    /// <summary>
    /// A recipe import that ran after the request that called the tool ended.
    /// </summary>
    public const string ModeDeferred = "deferred";

    /// <summary>
    /// A source document written to the knowledge base index.
    /// </summary>
    public const string ActionIndexed = "indexed";

    /// <summary>
    /// A source document that could not be embedded or written, and was skipped.
    /// </summary>
    public const string ActionFailed = "failed";

    /// <summary>
    /// A source document removed from the knowledge base index.
    /// </summary>
    public const string ActionRemoved = "removed";

    /// <summary>
    /// One verdict of the LLM judge on a conversation.
    /// </summary>
    public const string KindJudge = "judge";

    /// <summary>
    /// One simulation test run: the simulated conversation and its verdict.
    /// </summary>
    public const string KindTestRun = "test_run";

    /// <summary>
    /// One pass of a QA cohort over the conversations that closed since its last pass.
    /// </summary>
    public const string KindQACohort = "qa_cohort";

    /// <summary>
    /// The evaluation passed.
    /// </summary>
    public const string ResultPassed = "passed";

    /// <summary>
    /// The evaluation failed.
    /// </summary>
    public const string ResultFailed = "failed";

    /// <summary>
    /// A sync or a pass that finished, but lost some of its items on the way. The value CrestApps.Core uses.
    /// </summary>
    public const string OutcomePartial = "partial";

    /// <summary>
    /// A recipe an AI tool passed that did not match the recipe schema, or could not be read.
    /// </summary>
    public const string OutcomeInvalid = "invalid";

    /// <summary>
    /// The name of the span of one data source sync, full or incremental.
    /// </summary>
    public const string DataSourceSyncSpanName = "crestapps.ai.datasource.sync";

    /// <summary>
    /// The meter of the AI modules.
    /// </summary>
    public static readonly Meter Meter = CrestAppsOrchardCoreTelemetry.CreateMeter(Area);

    /// <summary>
    /// The activity source of the AI modules.
    /// </summary>
    public static readonly ActivitySource ActivitySource = CrestAppsOrchardCoreTelemetry.CreateActivitySource(Area);

    /// <summary>
    /// The duration of syncing a data source into its knowledge base index, tagged with <see cref="ProviderTag"/>,
    /// <see cref="ModeTag"/> and the outcome: <c>ok</c>, <see cref="OutcomePartial"/>, <c>skipped</c>, <c>error</c>
    /// or <c>cancelled</c>. A retried sync records one duration per attempt.
    /// </summary>
    public static readonly Histogram<double> DataSourceSyncDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.ai.datasource.sync.duration",
        "The duration of syncing a data source into its knowledge base index.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// The number of source documents a data source sync handled, tagged with <see cref="ProviderTag"/>,
    /// <see cref="ModeTag"/> and <see cref="ActionTag"/>.
    /// </summary>
    public static readonly Counter<long> DataSourceDocuments = Meter.CreateCounter<long>(
        "crestapps.ai.datasource.documents",
        "{document}",
        "The number of source documents data source syncs indexed, failed to index or removed.");

    /// <summary>
    /// The number of tool calls stopped by their time limit, tagged with <see cref="ToolSourceTag"/>. The call is
    /// also counted as an error on CrestApps.Core's <c>crestapps.core.ai.tool.duration</c>.
    /// </summary>
    public static readonly Counter<long> ToolTimeouts = Meter.CreateCounter<long>(
        "crestapps.ai.tool.timeouts",
        "{call}",
        "The number of AI tool calls stopped because they did not answer within their time limit.");

    /// <summary>
    /// The duration of an evaluation, tagged with <see cref="KindTag"/> and the outcome.
    /// </summary>
    public static readonly Histogram<double> EvaluationDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.ai.eval.duration",
        "The duration of a conversation evaluation, a simulation test run or a QA cohort pass.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// The number of judge verdicts and test runs, tagged with <see cref="KindTag"/> (<see cref="KindJudge"/> or
    /// <see cref="KindTestRun"/>) and the result: <see cref="ResultPassed"/>, <see cref="ResultFailed"/>,
    /// <c>error</c> or <c>skipped</c>.
    /// </summary>
    public static readonly Counter<long> EvaluationRuns = Meter.CreateCounter<long>(
        "crestapps.ai.eval.runs",
        "{evaluation}",
        "The number of conversation evaluations and simulation test runs, by their result.");

    /// <summary>
    /// The number of alert incidents opened, tagged with <see cref="MetricTag"/>.
    /// </summary>
    public static readonly Counter<long> AlertsRaised = Meter.CreateCounter<long>(
        "crestapps.ai.alerts.raised",
        "{alert}",
        "The number of AI alert rule incidents opened because a conversation metric crossed its threshold.");

    /// <summary>
    /// The number of alert incidents resolved, tagged with <see cref="MetricTag"/>.
    /// </summary>
    public static readonly Counter<long> AlertsResolved = Meter.CreateCounter<long>(
        "crestapps.ai.alerts.resolved",
        "{alert}",
        "The number of AI alert rule incidents resolved because the metric was back within its threshold.");

    /// <summary>
    /// The number of signed webhooks the Send Signed Webhook workflow task delivered or gave up on, tagged with the
    /// outcome (<c>ok</c> or <c>error</c>), the <c>status_class</c> of the last response and, for a failure, the
    /// <c>reason</c>: <c>invalid_url</c>, <c>missing_secret</c>, <c>egress_refused</c>, <c>rejected</c> or
    /// <c>exhausted</c>. The event name is free text a workflow renders, so it is never a tag.
    /// </summary>
    public static readonly Counter<long> WebhookDeliveries = Meter.CreateCounter<long>(
        "crestapps.ai.webhook.deliveries",
        "{delivery}",
        "The number of signed webhooks the workflow task delivered or gave up on.");

    /// <summary>
    /// The duration of delivering a signed webhook, every attempt and the waits between them included, tagged
    /// with the outcome, the <c>status_class</c> of the last response and the <c>reason</c> of a failure.
    /// </summary>
    public static readonly Histogram<double> WebhookDeliveryDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.ai.webhook.delivery.duration",
        "The duration of delivering a signed webhook, retries included.");

    /// <summary>
    /// The number of requests to the AI completion API, tagged with the <c>operation</c>, the <c>status_class</c>
    /// of the response and the outcome.
    /// </summary>
    public static readonly Counter<long> ApiRequests = Meter.CreateCounter<long>(
        "crestapps.ai.api.requests",
        "{request}",
        "The number of requests to the AI completion API, by the class of their response.");

    /// <summary>
    /// The number of recipes AI tools imported, tagged with <see cref="ModeTag"/> (<see cref="ModeInline"/> or
    /// <see cref="ModeDeferred"/>) and the outcome: <c>ok</c>, <c>error</c> or <see cref="OutcomeInvalid"/>.
    /// </summary>
    public static readonly Counter<long> RecipeImports = Meter.CreateCounter<long>(
        "crestapps.recipes.ai_imports",
        "{import}",
        "The number of recipes AI tools imported, by how the import ended.");

    /// <summary>
    /// Gets the <see cref="ToolSourceTag"/> value of a tool registry entry, the same value CrestApps.Core records.
    /// </summary>
    /// <param name="source">Where the tool comes from.</param>
    /// <returns>The bounded tool source.</returns>
    public static string GetToolSource(ToolRegistryEntrySource source)
    {
        return source switch
        {
            ToolRegistryEntrySource.McpServer => "mcp",
            ToolRegistryEntrySource.System => "system",
            ToolRegistryEntrySource.Agent => "agent",
            ToolRegistryEntrySource.A2AAgent => "a2a",
            ToolRegistryEntrySource.Local => "local",
            _ => CrestAppsOrchardCoreTelemetry.Unknown,
        };
    }

    /// <summary>
    /// Counts one judge verdict or test run.
    /// </summary>
    /// <param name="kind">The kind, <see cref="KindJudge"/> or <see cref="KindTestRun"/>.</param>
    /// <param name="result">The result, <see cref="ResultPassed"/>, <see cref="ResultFailed"/>, <c>error</c> or <c>skipped</c>.</param>
    public static void RecordEvaluation(string kind, string result)
    {
        if (!EvaluationRuns.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { KindTag, kind },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        EvaluationRuns.Add(1, tags);
    }

    /// <summary>
    /// Counts an opened or a resolved alert incident.
    /// </summary>
    /// <param name="counter">The counter, <see cref="AlertsRaised"/> or <see cref="AlertsResolved"/>.</param>
    /// <param name="metric">The metric the rule watches.</param>
    public static void RecordAlert(Counter<long> counter, string metric)
    {
        ArgumentNullException.ThrowIfNull(counter);

        if (!counter.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { MetricTag, metric ?? CrestAppsOrchardCoreTelemetry.Unknown },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        counter.Add(1, tags);
    }

    /// <summary>
    /// Counts the source documents a data source sync handled.
    /// </summary>
    /// <param name="count">The number of documents. Zero and negative counts are ignored.</param>
    /// <param name="provider">The provider of the knowledge base index.</param>
    /// <param name="mode">The mode of the sync.</param>
    /// <param name="action">What the sync did with them.</param>
    public static void RecordDocuments(long count, string provider, string mode, string action)
    {
        if (count <= 0 || !DataSourceDocuments.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { ProviderTag, provider ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { ModeTag, mode },
            { ActionTag, action },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        DataSourceDocuments.Add(count, tags);
    }

    /// <summary>
    /// Adds one to a counter with the given bounded tags and the tenant tag when it is on.
    /// </summary>
    /// <param name="counter">The counter.</param>
    /// <param name="tags">The bounded tags.</param>
    public static void Add(Counter<long> counter, TagList tags)
    {
        ArgumentNullException.ThrowIfNull(counter);

        if (!counter.Enabled)
        {
            return;
        }

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        counter.Add(1, tags);
    }
}
