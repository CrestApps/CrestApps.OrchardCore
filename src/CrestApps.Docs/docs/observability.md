---
sidebar_label: Observability
sidebar_position: 4
title: Observability with OpenTelemetry
description: The traces, metrics and structured logs the CrestApps modules emit, how to collect them with Orchard Core's OpenTelemetry support, and the rules every instrument follows.
---

# Observability with OpenTelemetry

> See what the site is doing: what is running, how long it takes, how often it fails, what is backed up, and where a request, a call or an import spent its time.

The CrestApps modules emit OpenTelemetry traces, metrics and logs through `System.Diagnostics` only, so they add no OpenTelemetry dependency and cost nothing when nothing listens. Each area has its own meter and activity source, named `CrestApps.OrchardCore.<Area>`. CrestApps.Core, the framework underneath, emits under `CrestApps.Core` and `CrestApps.Core.*` (see the [CrestApps.Core observability page](https://core.crestapps.com/docs/core/observability)), and Orchard Core emits under `OrchardCore`.

**Scope.** The modules instrument their own operations only: a call reaching an agent, an offer, a report run, an import, a checkout. Every other library reports its own telemetry, so nothing here wraps a single HTTP or SDK call (HttpClient instrumentation records those), Orchard Core's distributed locks, recipes, data migrations or file stores, YesSql queries, or SignalR connections.

## Turning it on

When an OTLP endpoint is configured, the `CrestApps.OrchardCore.Cms.Web` host turns on Orchard Core's OpenTelemetry support (the `OrchardCore.OpenTelemetry` package) and adds the sources and meters of our own code. Without an endpoint it adds nothing, so a site without a collector creates no spans or measurements:

```csharp
var exportTelemetry = builder.Configuration.HasOtlpEndpoint();

string[] ownSources =
[
    CrestAppsTelemetry.SourceName,
    CrestAppsTelemetry.SourceName + ".*",
    CrestAppsOrchardCoreTelemetry.SourceNameWildcard,
    "AICompletionService",
];

builder.Services
    .AddOrchardCms(orchardCoreBuilder =>
    {
        if (exportTelemetry)
        {
            orchardCoreBuilder.AddOpenTelemetry(openTelemetry => openTelemetry
                .WithTracing(tracing => tracing.AddSource(ownSources))
                .WithMetrics(metrics => metrics.AddMeter(ownSources)));
        }
    });
```

- `HasOtlpEndpoint()` is true when `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_{SIGNAL}_ENDPOINT` or `OrchardCore:OpenTelemetry:Otlp:Endpoint` is set.
- `AddOpenTelemetry()` collects Orchard Core, ASP.NET Core, HttpClient and runtime telemetry, tags spans and logs with their tenant, and exports them over OTLP.
- `CrestApps.Core` and `CrestApps.Core.*` collect the framework; `CrestApps.OrchardCore.*` (`CrestAppsOrchardCoreTelemetry.SourceNameWildcard`) collects every CrestApps module. The OpenTelemetry SDK matches the wildcards itself; `X.*` does not match `X`, so both are listed.
- `AICompletionService` is the source the AI chat clients report their `gen_ai.*` spans and metrics on, only while the AI **Enable OpenTelemetry** setting is on (**Settings → Artificial Intelligence → General**).
- Other libraries' telemetry, such as the OpenAI SDK's, SignalR's, the MCP and A2A SDKs', Npgsql's or Polly's, is not collected by default. Add their source or meter names to `OrchardCore:OpenTelemetry:Tracing:Sources` or `Metrics:Meters` to collect it.

Every OpenTelemetry package comes from Orchard Core (1.19.1), so no version needs pinning.

A host that configures OpenTelemetry through settings instead of code lists the same names:

```json
{
  "OrchardCore": {
    "OpenTelemetry": {
      "Tracing": { "Sources": [ "CrestApps.OrchardCore.*", "CrestApps.Core", "CrestApps.Core.*", "AICompletionService" ] },
      "Metrics": { "Meters": [ "CrestApps.OrchardCore.*", "CrestApps.Core", "CrestApps.Core.*", "AICompletionService" ] }
    }
  }
}
```

### Seeing it locally

Running the Aspire AppHost (`CrestApps.Aspire.AppHost`) sets `OTEL_EXPORTER_OTLP_ENDPOINT` for the CMS, so everything shows on the Aspire dashboard. Without the AppHost, start the dashboard on its own and point the site at it:

```bash
docker run --rm -it -p 18888:18888 -p 4317:18889 mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

Then set `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317` and `OTEL_SERVICE_NAME=OrchardCoreCMS` for the site and open `http://localhost:18888`.

## Rules every instrument follows

- **Names.** Meter and activity source `CrestApps.OrchardCore.<Area>`; instruments `crestapps.<area>.<thing>`; spans `crestapps.<area>.<operation>`; UCUM units (`s`, `By`, `{row}`, `{item}`). Durations are in seconds with bucket boundaries advised for seconds. The names are a public contract.
- **Outcome.** Every `outcome` tag is one of `ok`, `error`, `skipped`, `cancelled`, `timeout` or `blocked`, plus the bounded values an area lists below. A duration histogram's count is the number of operations, so no separate counter repeats it; a count of failures is the count of a duration whose `outcome` is `error`. An operation that ends without saying how (such as an early return) records `unknown`.
- **Tags a metric never carries.** Call, interaction, activity, reservation, contact, entry, batch, session, profile, report, query or item ids; phone numbers; user names; URLs and hosts; lock keys; file names; SQL text; free-text names (tool instances, data sets, crawlers, calendars, report titles). `CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys` lists the keys, and a unit test fails when a shared helper emits one. Allowed tags are bounded: `provider`, `operation`, `direction`, `channel`, `outcome`, `reason`, `result`, `format`, `status_class`, `trigger`, `task`, and bounded configuration values such as a connector type or a model deployment.
- **Spans and logs** may carry ids, so a trace can be found from a record, but never phone numbers, transcripts or SIP URIs. A failed span records the exception type and stack trace but not the exception message, which can quote such values; the message stays in the logs.
- **Tenant.** Spans and logs carry the tenant through `OrchardCore.OpenTelemetry`. The CrestApps metrics carry it as `orchardcore.tenant.name`, the attribute Orchard Core uses, and follow Orchard Core's own `OrchardCore:OpenTelemetry:Metrics:IncludeTenantName` option (default `true`, see [Configuration](configuration.md#observability)). Turn it off to limit the number of series with many tenants.
- **No double counting.** Instruments sit at the state transition, never in a handler that a durable outbox can replay.
- **Approximate counters.** A few counters are recorded inside a unit of work, before it commits: the contact center queue and offer counters, messaging delivery and assignment, inventory holds and movements, orders and subscription transitions. When a write conflict makes the work retry, they can count it twice; treat them as approximate.
- **Async hops.** Work that crosses a queue, an outbox, a webhook inbox or a stored import entry keeps the `traceparent` of the request that queued it and starts its span with a link to that trace, not as its child.

## Shared platform instruments

The `CrestApps.OrchardCore.Platform` meter and activity source hold the instruments every module records through the helpers in `CrestApps.OrchardCore.Diagnostics`.

| Instrument | Type | Unit | Tags | What it measures |
| --- | --- | --- | --- | --- |
| `crestapps.background_task.outcomes` | Counter | `{run}` | `task`, `outcome` (`error`, `skipped`, `skipped_lease`, `cancelled`, `timeout`, `budget_exceeded`) | Background task runs that did not end `ok`, including failures a task logged and swallowed, which Orchard Core sees as successful runs. Every run, `ok` ones included, is counted by Orchard Core's `orchardcore.background_task.duration`. |
| `crestapps.background_task.items` | Counter | `{item}` | `task`, `result` | Items a run processed, by what became of them. |
| `crestapps.background_task.last_success` | Gauge | `s` | `task` | The Unix time a task last finished a run without an error, including a run that found nothing to do (`skipped`). Alert when it falls too far behind the clock. |
| `crestapps.background_task.lock.utilization` | Histogram | `1` | `task` | A run's duration divided by its task's lock expiration. Near or above 1, the lock can expire while the run still works. |
| `crestapps.deferred_task.duration` | Histogram | `s` | `task`, `outcome` | Deferred tasks run after a request's scope ended (`DeferredWork.Add`); its count is the number of runs. |
| `crestapps.retry.attempts` | Counter | `{retry}` | `area`, `reason`, `attempt` (capped at 5) | Retries of the solution's own retry loops. |
| `crestapps.background.pacer.gate_wait` | Histogram | `s` | `result` (`acquired`, `timeout`) | The time a bulk batch waited for the shared pacing gate: the best measure of how busy bulk work keeps the database. `result=timeout` counts the batches that ran without it. |
| `crestapps.background.pacer.pause` | Histogram | `s` | | The pause a batch held the gate through after it ran. |

Orchard Core already starts a span (`Run background task {name}`) and records `orchardcore.background_task.duration` for every background task run, so the CrestApps background task helper adds metrics only.

| Span | What it covers |
| --- | --- |
| `crestapps.deferred.<name>` | One deferred task, linked to the request that queued it. |

## AI modules

Meter and activity source: `CrestApps.OrchardCore.AI` (`AIModuleTelemetry` in `src/Core/CrestApps.OrchardCore.AI.Core/Telemetry`).

The AI pipeline itself is measured once, by CrestApps.Core, on the `CrestApps.Core` meter and source: chat turns (`crestapps.chat.turn` span, `crestapps.core.chat.turn.duration`, `crestapps.core.ai.orchestration.*`, with the `SessionId`/`ProfileId` log scope), tools (`crestapps.tool.invoke`, `crestapps.core.ai.tool.duration`), retrieval (`crestapps.core.ai.retrieval.*`), document ingestion (`crestapps.core.ai.ingestion.*`, `crestapps.core.ai.documents.uploads.rejected`), file source and crawler runs (`crestapps.core.ai.ingestion.run.*`, `crestapps.core.ai.webcrawler.*`), memory (`crestapps.core.ai.memory.*`), MCP and A2A (`crestapps.core.mcp.*`, `crestapps.core.a2a.*`, plus the SDKs' `mcp.*` and `A2A` spans), realtime voice (`crestapps.core.realtime.*`) and usage (`crestapps.core.ai.usage.*`). The instruments below cover only what the Orchard Core modules do on their own.

### Instruments

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.ai.datasource.sync.duration` | Histogram | `s` | `provider`, `mode` (`full`, `incremental`), `outcome` (`ok`, `partial`, `skipped`, `error`, `cancelled`), `error.type` | Syncing one data source into its knowledge base index. A retried sync records one duration per attempt. `partial`: some documents could not be embedded or a batch could not be written; the sync went on. `skipped`: no index manager, no index, no embedding deployment or generator, or no source handler. |
| `crestapps.ai.datasource.documents` | Counter | `{document}` | `provider`, `mode`, `action` (`indexed`, `failed`, `removed`) | Source documents (not chunks) a sync indexed, could not index, or removed. |
| `crestapps.ai.tool.timeouts` | Counter | `{call}` | `tool.source` (`local`, `mcp`, `agent`, `a2a`, `system`, `unknown`) | Tool calls stopped by their time limit (`TimeLimitedAIFunction`, phone and SMS conversations). The call is also counted as `error` on CrestApps.Core's `crestapps.core.ai.tool.duration`. Never the tool name. |
| `crestapps.ai.eval.duration` | Histogram | `s` | `kind` (`judge`, `test_run`, `qa_cohort`), `outcome` (`ok`, `skipped`, `partial`, `error`, `cancelled`, `timeout`), `result` (`passed`, `failed`, `error` for judge verdicts and test runs), `reason` (`run_error` for a test run that could not finish), `error.type` | One LLM judge verdict, one simulation test run (simulation plus verdict), or one QA cohort pass. Its count by `result` is the number of verdicts and test runs. |
| `crestapps.ai.alerts.raised` | Counter | `{alert}` | `metric` (the `AIConversationMetric` name) | Alert rule incidents opened. |
| `crestapps.ai.alerts.resolved` | Counter | `{alert}` | `metric` | Alert rule incidents resolved. |
| `crestapps.ai.webhook.delivery.duration` | Histogram | `s` | `outcome` (`ok`, `error`), `status_class`, `reason` (`invalid_url`, `missing_secret`, `egress_refused`, `rejected`, `exhausted`) | One signed webhook delivery of the **Send Signed Webhook** workflow task, our own retry loop and the waits between attempts included; its count is the number of deliveries. The event name is free text and never a tag. |
| `crestapps.recipes.ai_imports` | Counter | `{import}` | `mode` (`inline`, `deferred`), `outcome` (`ok`, `error`, `invalid`) | Recipes the AI agent tools (`ImportRecipeBaseTool`) imported. A deferred import is counted once, when it runs, with how it really ended. |

Recorded on instruments owned elsewhere:

| Instrument | Owner | What the AI modules add |
|---|---|---|
| `crestapps.core.ai.tool.duration` | CrestApps.Core | A tool that hit its time limit calls `AIToolInvocation.ReportFailure()`, so it counts as `error` instead of `ok`. |
| `crestapps.retry.attempts` | Platform | `area` = `ai.datasource_indexing` (`reason` = `error`, `timeout`) for `DataSourceIndexingService` sync retries; `area` = `ai.signed_webhook` (`reason` = `network`, `timeout`, `status_429`, `status_5xx`, `other`) for Send Signed Webhook retries. |

### Spans

- `crestapps.ai.datasource.sync` (tags `provider`, `mode`, `outcome`; span only: `crestapps.data_source.id`, `crestapps.index.name`, `crestapps.data_source.documents.indexed`, `crestapps.data_source.documents.failed`)
- `crestapps.ai.eval.judge` (span only: `crestapps.profile.id`, `crestapps.session.id`, `crestapps.ai.eval.score`)
- `crestapps.ai.eval.test_run` (span only: `crestapps.ai.test_run.id`, `crestapps.ai.test_case.id`)
- `crestapps.ai.eval.qa_cohort` (span only: `crestapps.ai.qa_cohort.id`, `crestapps.ai.eval.count`)
- `crestapps.ai.webhook.deliver` (internal; span only: `crestapps.webhook.delivery.id`, `crestapps.webhook.attempts`, `http.response.status_code`)
- `crestapps.deferred.<task>` for the deferred work above (platform source).

### Log event ids

| EventId | Level | Class | Message |
|---|---|---|---|
| 11001 | Warning | `AICoreLog.ToolTimedOut` | A tool did not answer within its time limit and was stopped (replaces the existing ad hoc warning). |
| 11002 | Information | `AICoreLog.DataSourceSynced` | A full data source sync finished: chunks, source documents, failed documents and elapsed ms (replaces the old "Synced `{DocumentCount}` chunks" line). |
| 11003 | Warning | `AICoreLog.DataSourceSyncRetrying` | A data source sync failed and is retried, with the attempt number (same text as before, now with an id). |
| 11004 | Warning | `AICoreLog.DeferredRecipeImportFailed` | A recipe an AI tool imported failed when it ran after the request (it was silently ignored before). |
| 11100 | Debug | `AIModuleLog.SignedWebhookRetrying` | A signed webhook failed with a retryable status and is tried again, with the attempt number. |

Ranges: AI.Core 11000-11099, the AI module 11100-11199.

Log scope: `POST api/ai/completion/chat` adds `ProfileId` and `SessionId` to every log line of the request (the chat and interaction hubs get the same scope from CrestApps.Core).

### Behavior notes

- AI usage metrics (`crestapps.core.ai.usage.requests` / `.tokens` / `.duration`) are emitted by CrestApps.Core only while the **AI Usage Analytics** feature is enabled, because that feature is the only one that calls `AddCoreAIUsageTracking()`, and metering (and its observer) is off without it. With it off, only `gen_ai.client.token.usage` (chat only, `EnableOpenTelemetry` on) is available. Chart one or the other, not both.
- `AIChatSessionCloseBackgroundTask` has a 30 s lock expiration; expect `crestapps.background_task.lock.utilization` above 1 on large tenants.

## Reports and Report Builder

Meter and activity source: `CrestApps.OrchardCore.Reports` (`ReportTelemetry`, Reports.Core). The PDF export adds `crestapps.report.pdf.font_fallbacks` on a second meter with the same name.

The full contract below is also the Telemetry section of `src/CrestApps.Docs/docs/modules/report-builder/large-data.md`.

Spans and metrics cover designed report runs, coded reports, exports, views, subscriptions, the designer and the widgets.

### Tags

Metrics carry bounded tags only, so a site with thousands of reports still stores a handful of series:

- `crestapps.report.source`: what the run is for (`ReportRunSources`): `Admin`, `Shared` or `Link` for the report pages, `Preview` for the builder, `Widget`, `Workflow`, `Subscription`, `ViewRefresh` for the refresh of a scheduled view, `ViewRead` for a live view another report reads, and `Other` for anything else. A value that is not in this list is tagged `Other`.
- `crestapps.report.data_source`: the registered data source, such as `Contents` or `ReportViews`.
- `outcome`: `ok`, `error`, `cancelled` or `timeout`, plus the values an instrument lists below.
- `crestapps.report.name`: the technical name of a coded report in the report registry.
- `format`: the name of a registered export format; a format typed in an address that is not registered is `other`.

The report and query identifiers (`crestapps.report.id`, `crestapps.report.query.id`), the data set (`crestapps.report.data_set`), the view and subscription identifiers and the report title are **span tags only**: they no longer tag metrics. The old `crestapps.report.outcome` tag (`success`, `failure`, `canceled`) is gone; use `outcome`.

### Spans

| Span | One per | Tags besides the common ones |
| --- | --- | --- |
| `report.run` | Run of a designed report, including the preview and a click that refreshes some visuals | `crestapps.report.click_filtered`, `crestapps.report.refreshed_visuals` (the visuals a click refreshes), `crestapps.report.rows.returned`, `crestapps.report.warnings`, `crestapps.report.queries`, `crestapps.report.queries.failed`, `outcome` |
| `report.plan` | Run of a query (a query filtered by a click is a run of its own) | `crestapps.report.calculated_fields`, `crestapps.report.row_calculations` (the ones it computes), `crestapps.report.errors` |
| `report.schema` | Data set schema a plan reads, whoever plans: report runs, views, the builder | `crestapps.report.data_source`, `crestapps.report.data_set`, `outcome` (`ok`, `not_found`, `error`) |
| `report.query` | Run of a query | `crestapps.report.gate_wait_ms` (time waiting for `MaxConcurrentRuns`), `crestapps.report.rows.source`, `crestapps.report.rows.filtered`, `crestapps.report.rows.returned`, `crestapps.report.calculate_ms`, `crestapps.report.calc_errors`, `crestapps.report.grouped`, `crestapps.report.grouped_by_source`, `crestapps.report.pushdown_fallback`, `crestapps.report.sorted`, `crestapps.report.spills` |
| `report.stream` | Query whose rows stream to another report, such as a live view | `crestapps.report.rows.source`, `crestapps.report.rows.returned`, `outcome` |
| `report.read` | Read of a data set | `crestapps.report.data_source`, `crestapps.report.data_set`, `crestapps.report.fields`, `crestapps.report.rows.read`, `crestapps.report.read_shared` (read from the rows another query kept), `crestapps.report.truncated`, `crestapps.report.join_key_batches`, `crestapps.report.view.mode` (a view) |
| `report.join` | Join | `crestapps.report.data_source`, `crestapps.report.data_set`, `crestapps.report.join_type`, `crestapps.report.join_keys`, `crestapps.report.rows.joined` |
| `report.result` | Run of a query, while it builds its result and the groupings its totals, charts and pivots need | |
| `report.render` | Run of a query that shows visuals, while it builds their sections | `crestapps.report.visuals` |
| `report.present` | Report page, shared page or share link, around its run | `crestapps.report.id`, `crestapps.report.source` |
| `report.events` | Fan-out of a run or an export to the report event handlers, such as the workflow events | `crestapps.report.id`, `crestapps.report.event` (`ran`, `exported`) |
| `report.coded.run` | Run of a coded report | `crestapps.report.name`, `crestapps.report.mode`, `outcome` |
| `report.export` | File an export format writes | `format`, `crestapps.report.source`, `crestapps.report.export.size`, `crestapps.report.export.rows`, `outcome` |
| `report.view.refresh` | Refresh of a scheduled view that runs the view | `crestapps.report.view.id`, `trigger`, `crestapps.report.view.snapshot.rows`, `outcome` (`refreshed`, `failed`) |
| `report.view.sweep` | Run of the **Report view refresh** background task | `crestapps.report.views.due`, `.refreshed`, `.failed`, `.skipped` |
| `report.subscription.send` | Run of a subscription | `crestapps.report.subscription.id`, `trigger`, `reason`, `outcome` |
| `report.subscription.sweep` | Run of the **Report subscriptions** background task | `crestapps.report.subscriptions.due`, `.sent`, `.not_sent` |

The designed run spans carry `crestapps.report.id`, `crestapps.report.title`, `crestapps.report.source` and `crestapps.report.query.id` (empty for the main query). Rows stream through a query, so the read, join and calculation of one query overlap: `report.read` lasts while its rows are consumed, and `crestapps.report.calculate_ms` adds up the time spent on calculated fields.

### Metrics

Designed report runs:

| Instrument | Kind | Unit | Tags | What it measures |
| --- | --- | --- | --- | --- |
| `crestapps.report.run.duration` | Histogram | s | source, `outcome` | Designed report runs |
| `crestapps.report.run.failures` | Counter | `{run}` | source, `crestapps.report.level` (`run`, `query`) | Each failed run once at `run`, each failed query once at `query` (an invalid plan, or an exception in the engine). A cancellation isn't a failure. A run whose secondary query fails is `ok` and counts that query at `query`. |
| `crestapps.report.run.queries` | Histogram | `{query}` | source | Queries one run executed |
| `crestapps.report.run.queries.skipped` | Counter | `{query}` | source, `reason` (`invalid`, `failed`) | Queries of a run that did not run |
| `crestapps.report.run.rejected` | Counter | `{run}` | source, `reason` (`owner_missing`) | Runs refused before they started |
| `crestapps.report.query.duration` | Histogram | s | source, `outcome` | Queries |
| `crestapps.report.stream.duration` | Histogram | s | source, `outcome` | Queries streamed to another report |
| `crestapps.report.plan.duration` | Histogram | s | `outcome` (`ok`, `invalid`, `error`) | Query plans, whoever plans |
| `crestapps.report.schema.duration` | Histogram | s | data source, `outcome` (`ok`, `not_found`, `error`) | Data set schema reads of plans |
| `crestapps.report.rows.read` | Counter | `{row}` | source, data source | Rows read from data sources |
| `crestapps.report.rows.returned` | Counter | `{row}` | source | Rows queries return |
| `crestapps.report.reads.shared` | Counter | `{read}` | source, data source | Reads served from the rows another query kept |
| `crestapps.report.reads.shared.miss` | Counter | `{read}` | data source, `reason` (`disabled`, `join_key`, `conditions`, `too_many_rows`) | Reads of a data set several queries read that the rows kept could not serve |
| `crestapps.report.limit.hits` | Counter | `{hit}` | `crestapps.report.limit` (`rows_per_dataset`, `joined_rows`, `result_rows`, `join_keys`, `shared_read_rows`), source | Runs that reached a limit of [Configuration](modules/report-builder/index.md#configuration) |
| `crestapps.report.aggregate.pushdown` | Counter | `{query}` | `outcome` (`used`, `fallback`), `reason` (`multiple_data_sets`, `joins`, `row_calculations`, `not_supported`, `filter`, `dimension`, `measure`, `too_many_buckets`, `declined`), data source | Grouped queries the data source grouped, or why they read every row |
| `crestapps.report.aggregate.index` | Counter | `{statement}` | `result` (`built`, `unbuildable`, `too_many_groups`), `crestapps.report.index` | `ReportIndexAggregator` statements over a YesSql index |
| `crestapps.report.calc.errors` | Counter | `{error}` | source | Calculated field values that failed and were left empty, added once per query |
| `crestapps.report.spills` | Counter | `{file}` | source | Temporary files a query wrote because its rows didn't fit in `MemoryLimitMegabytes` |
| `crestapps.report.gate.wait` | Histogram | s | source | Time runs waited for `MaxConcurrentRuns` |
| `crestapps.report.gate.active` | UpDownCounter | `{run}` | source | Runs that hold the gate |
| `crestapps.report.gate.waiting` | UpDownCounter | `{run}` | source | Runs that wait for the gate |

Coded reports, exports, views and subscriptions:

| Instrument | Kind | Unit | Tags | What it measures |
| --- | --- | --- | --- | --- |
| `crestapps.report.coded.duration` | Histogram | s | `crestapps.report.name`, `crestapps.report.mode` (`display`, `export`), `outcome` | Coded report runs |
| `crestapps.report.coded.index_rows` | Histogram | `{row}` | `crestapps.report.name` | Index rows a coded report read at once; from 50,000 rows (`ReportTelemetry.CodedIndexRowsWarningThreshold`) a warning is logged |
| `crestapps.report.export.duration` | Histogram | s | `format`, `outcome` | Time export formats took to write their file |
| `crestapps.report.export.size` | Histogram | By | `format` | Size of the exported files |
| `crestapps.report.export.rows` | Histogram | `{row}` | `format` | Table rows of the exported files |
| `crestapps.report.exports` | Counter | `{export}` | source, `format`, `outcome` (`ok`, `error`, `not_found`, `forbidden`) | Export requests, including those refused for an unknown format or a missing permission |
| `crestapps.report.pdf.font_fallbacks` | Counter | `{export}` | | PDF exports written with the built-in Helvetica font, which shows Latin-1 text only; a warning is logged once |
| `crestapps.report.view.refresh.duration` | Histogram | s | `trigger` (`schedule`, `manual`), `outcome` (`refreshed`, `failed`) | Refreshes that ran a view |
| `crestapps.report.view.refreshes` | Counter | `{refresh}` | `trigger`, `outcome` (`refreshed`, `failed`, `busy`, `not_due`, `not_scheduled`, `not_found`) | Refresh attempts |
| `crestapps.report.view.snapshot.rows` | Histogram | `{row}` | | Rows a refresh stored |
| `crestapps.report.view.snapshot.age` | Gauge | s | | Age of the oldest stored rows of a scheduled view, at each run of the background task. Alert when it grows well past the longest refresh interval. |
| `crestapps.report.view.reads` | Counter | `{read}` | `crestapps.report.view.mode` (`snapshot`, `live`, `stale_snapshot`, `first_run`) | Reads of views by other reports |
| `crestapps.report.view.rejected` | Counter | `{read}` | `reason` (`loop`, `depth`) | View reads refused |
| `crestapps.report.subscription.sends` | Counter | `{email}` | `trigger`, `outcome` (`ok`, `skipped`, `error`), `reason` (`no_email`, `no_recipients`, `report_unavailable`, `export_denied`, `send_failed`, `exception`) | Subscription runs |
| `crestapps.report.subscription.duration` | Histogram | s | `trigger`, `outcome` | Subscription runs, from running the report to sending the email |

The builder and the widgets:

| Instrument | Kind | Unit | Tags | What it measures |
| --- | --- | --- | --- | --- |
| `crestapps.report.design.publishes` | Counter | `{publish}` | `outcome` (`saved`, `invalid`, `conflict`, `busy`, `not_found`) | Publishes |
| `crestapps.report.design.conflicts` | Counter | `{request}` | `reason` (`conflict`, `busy`) | Saves refused with a 409 |
| `crestapps.report.design.validation_errors` | Counter | `{error}` | | Problems the builder found in the queries it checked |
| `crestapps.report.shared_link.requests` | Counter | `{request}` | `result` (`opened`, `exported`, `not_found`, `export_denied`) | Requests of share links, which anonymous visitors can reach |
| `crestapps.report.workflow.tasks` | Counter | `{task}` | `crestapps.report.workflow_task` (`run`, `export`, `email`, `share_link`), `outcome` | Report workflow tasks |
| `crestapps.report.widget.requests` | Counter | `{request}` | `crestapps.report.kind` (`design`, `coded`), `crestapps.report.cache` (`hit`, `miss`, `off`), `outcome` | Dashboard widget renders |
| `crestapps.report.hub.denied` | Counter | `{call}` | `operation` | Presence hub calls refused |

Both background tasks also report `crestapps.background_task.items` (`refreshed`, `failed`, `skipped` for views; `sent`, `not_sent` for subscriptions), and the builder notifications run as the deferred task `report_design_notify`.

### Logs

The report engine, the Reports module and the PDF export log with event ids from 14000 to 14999:

| Event id | Level | When |
| --- | --- | --- |
| 14001 | Warning | A coded report read at least 50,000 index rows at once |
| 14002 | Debug | The temporary files of a query could not be deleted |
| 14101 | Warning | A designed report run ended with errors |
| 14102 | Warning | A designed report run took longer than 10 seconds |
| 14107 | Debug | The presence hub refused a call |
| 14110 | Debug | The builder found problems in a query |
| 14111, 14112 | Information | Summary of the report designs recipe step and deployment step |
| 14400 | Warning | The PDF export has no TrueType font (logged once) |

## Data connectors

Meter and activity source: `CrestApps.OrchardCore.DataConnectors` (`DataConnectorsTelemetry` in `CrestApps.OrchardCore.DataConnectors.Core`, namespace `CrestApps.OrchardCore.DataConnectors.Telemetry`). Instruments are named `crestapps.dataconnector.*`.

Outcome values follow the shared vocabulary: `ok`, `error`, `cancelled`, `timeout`, `skipped`. For refreshes they map to the run statuses as: Succeeded → `ok`, Failed → `error` (or `timeout` when the refresh ran past `RefreshTimeout`), Canceled → `cancelled`, Skipped → `skipped`. An `error` measurement also carries `error.type` (exception type name).

Bounded tag values:

- `connector`: the name of a registered connector (`HttpApi`, `Elasticsearch`, `MediaFiles`, `SqlServer`, ...), or `unknown` when the connection couldn't be resolved (and on refreshes skipped before the connection was read).
- `trigger`: `manual` or `scheduled`.
- `mode`: `full` or `incremental`, the mode the refresh **ended** with (an incremental refresh that falls back to a full read is tagged `full`).
- `purpose`: `live`, `import` or `preview`.
- `reason` (HTTP retries): `network`, `timeout`, `status_429`, `status_5xx`, `redirect`, `token_refresh`.
- `auth`: `oauth2` or `openid_connect`.
- `result` (tokens): `cache_hit`, `acquired`, `failed`.
- `pagination`: `none`, `page_number`, `offset`, `cursor`, `next_link`, `link_header`.
- `limit`: `max_pages`.
- `transient`: `true` / `false`.
- `format`: the registered file format name (`csv`, `json`, `jsonl`, `xlsx`, ...).

No metric carries a data set, connection or refresh id, a data set name, a URL, a host or a file name. Ids go on spans only.

### Instruments

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.dataconnector.refresh.duration` | Histogram (long-duration buckets) | `s` | `trigger`, `mode`, `connector`, `outcome` (+`error.type`) | Duration of a refresh of an imported data set, lock wait and cleanup included. |
| `crestapps.dataconnector.refresh.rows` | Histogram | `{row}` | `trigger`, `mode`, `connector` | Rows a successful refresh read from its source. |
| `crestapps.dataconnector.refresh.active` | UpDownCounter | `{refresh}` | `trigger` | Refreshes running now. |
| `crestapps.dataconnector.read.duration` | Histogram | `s` | `purpose`, `connector`, `outcome` (+`error.type`) | A read of a data set from its source through `ConnectedDataReader.ReadAsync` (live reads and previews), from the first batch asked for to the last one; includes the time the consumer spends between batches. |
| `crestapps.dataconnector.read.rows` | Histogram | `{row}` | `purpose`, `connector` | Rows a successful read returned. |
| `crestapps.dataconnector.read.failures` | Counter | `{read}` | `purpose`, `connector`, `transient` | Reads that failed (not counting reads the caller cancelled, nor timeouts). |
| `crestapps.dataconnector.read.timeouts` | Counter | `{read}` | `purpose`, `connector` | Reads stopped by `LiveReadTimeout`. |
| `crestapps.dataconnector.connection.tests` | Counter | `{test}` | `connector`, `outcome` (`ok`/`error`) | Admin checks of a data connection. |
| `crestapps.dataconnector.empty_reads` | Counter | `{read}` | none | Reads of an imported data set before its first refresh succeeded (the read returns no rows). |
| `crestapps.dataconnector.http.retries` | Counter | `{retry}` | `reason` | Requests of HTTP-based connectors sent again. |
| `crestapps.dataconnector.token.requests` | Counter | `{request}` | `auth`, `result` | Tokens HTTP-based connectors needed, by whether the cache served them. |
| `crestapps.dataconnector.network.blocked` | Counter | `{connection}` | none | Connections refused because every address of the host is on the site's own network (security event). |
| `crestapps.dataconnector.http.pages` | Counter | `{page}` | `pagination` | Pages HTTP API data sets read. |
| `crestapps.dataconnector.limit.hits` | Counter | `{hit}` | `limit` | Reads cut short by a limit; today `max_pages` (an HTTP API read stopped at its page limit, 1000 by default, while the API still had a next page). Field-inference sampling is not counted. |
| `crestapps.dataconnector.elasticsearch.scroll.clear_failures` | Counter | `{scroll}` | none | Scrolls that couldn't be cleared after a read (they stay open until their keep-alive passes). |
| `crestapps.dataconnector.file.parse.duration` | Histogram (long-duration buckets) | `s` | `format`, `outcome` | Time a file format spent producing rows from a media library file (not the consumer's time between batches). |

Shared platform instruments the area also feeds:

- `crestapps.retry.attempts` with `area=dataconnectors`, `reason` = `network|timeout|status_429|status_5xx|token_refresh`, `attempt` (redirects are not counted there, only on `http.retries`).
- `crestapps.background_task.outcomes` / `.items` / `.last_success` / `.lock.utilization` for `task=DataSetRefreshBackgroundTask` (`skipped` when nothing ran; items `result=processed` = refreshes run, succeeded or failed).
- `crestapps.deferred_task.duration` and span `crestapps.deferred.dataconnectors_refresh` for the refresh an administrator asks for from the admin (runs after the request through `HttpBackgroundJob`, linked to the request's trace).

### Spans

- `crestapps.dataconnector.refresh` — one refresh. Tags: `crestapps.dataconnector.data_set.id`, `crestapps.dataconnector.refresh.id`, `crestapps.dataconnector.rows`, `trigger`, `mode`, `connector`, `outcome`; `crestapps.dataconnector.refresh.schema_changed` when the source's fields changed; `crestapps.dataconnector.refresh.full_read_reason` (`unsupported|no_published_version|no_identifiers|no_incremental_field|fields_changed`) when an incremental refresh read every row.
  - `crestapps.dataconnector.refresh.describe` — reading the source's fields.
  - `crestapps.dataconnector.refresh.copy` — copying the published rows (incremental refresh only); `crestapps.dataconnector.rows`.
  - `crestapps.dataconnector.refresh.batches` — one span over the whole read/write loop; `crestapps.dataconnector.batches`, `crestapps.dataconnector.rows`.
  - `crestapps.dataconnector.refresh.count` — counting the new version's rows.
- `crestapps.dataconnector.read` — a read through `ConnectedDataReader.ReadAsync`. Tags: `crestapps.dataconnector.data_set.id`, `crestapps.dataconnector.connection.id`, `crestapps.dataconnector.rows`, `purpose`, `connector`, `outcome`.
- `crestapps.deferred.dataconnectors_refresh` (platform source) — see above.

Failed spans carry `error.type` and an error status, but not the exception message: a data connector's messages can name the server and path it called.

### Log messages (`[LoggerMessage]`, `CrestAppsEventIds.DataConnectors` = 15000)

| EventId | Level | Name | Notes |
|---|---|---|---|
| 15002 | Information | `RefreshSchemaChanged` | The field changes (previously only stored on the run record). |
| 15003 | Information | `RefreshDowngradedToFull` | Bounded reason an incremental refresh read every row (previously only stored on the run record). |
| 15010 | Debug | `HttpRetrying` | Reason, status, attempt N of 4, delay. Never the URL. |
| 15011 | Debug | `HttpResponded` | Method, URL without query string, status, duration, request number. |
| 15012 | Debug | `HttpNoAnswer` | Method, URL without query string, request number, reason. |
| 15013 | Warning | `NetworkBlocked` | A connection to the site's own network was refused (no host in the message; the exception the caller sees names it). |
| 15014 | Warning | `HttpPageLimitReached` | Data set id and page limit. |
| 15015 | Debug | `ElasticsearchScrollNotCleared` | With the exception. |
| 15020 | Warning | `ReadTimedOut` | Data set id and `LiveReadTimeout`. |
| 15021 | Warning | `ReadBeforeFirstImport` | Once per data set per process. |

Every log line of a refresh carries the scope `DataSetId` / `RefreshId`.

## Content transfer, imports and exports

Areas: Content Transfer, do-not-call registry, Omnichannel bulk loads and bulk actions, recipes and deployments, inventory. Every metric gets the `orchardcore.tenant.name` tag when tenant tags are on. No metric carries an entry, batch, list, contact or activity id, a file name, a row index, a user or a phone number; those go on spans and in logs only (phone numbers never). Reasons and results are bounded codes, never localized text.

### Content Transfer

Meter and activity source: `CrestApps.OrchardCore.ContentTransfer` (class `ContentTransferDiagnostics` in `CrestApps.OrchardCore.ContentTransfer.Core`, namespace `CrestApps.OrchardCore.ContentTransfer.Telemetry`).

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.contenttransfer.import.active` | UpDownCounter | `{run}` | none | Import runs at work on this node |
| `crestapps.contenttransfer.import.retries` | Counter | `{retry}` | `reason` (failed_run) | Times a run tried an import again after a failed run (also on `crestapps.retry.attempts` with `area=contenttransfer`) |
| `crestapps.contenttransfer.import.duration` | Histogram (long buckets) | s | `outcome`, `content_type`, `format` | One import run |
| `crestapps.contenttransfer.import.file.size` | Histogram | By | `format` | Size of an imported file, when its import starts from row 0 |
| `crestapps.contenttransfer.import.rows.total` | Histogram | `{row}` | `format` | Rows of an imported file, when its import starts from row 0 |
| `crestapps.contenttransfer.import.entries.failed` | Counter | `{entry}` | `reason` (content_type_removed, file_missing, repeated_failures) | Imports marked as failed |
| `crestapps.contenttransfer.import.failures` | Counter | `{run}` | `reason` (transient, resumable, final, unrecorded) | Failed import runs, by how the failure was handled |
| `crestapps.contenttransfer.import.stalled_rows` | Counter | `{row}` | `result` (recorded, unrecorded) | Rows an abandoned run was stuck on |
| `crestapps.contenttransfer.import.batch.duration` | Histogram | s | `outcome` (ok, stopped, error, cancelled, timeout) | One import batch, from the status check to the saved progress |
| `crestapps.contenttransfer.import.batch.size` | Histogram | `{row}` | none | Rows in one saved import batch |
| `crestapps.contenttransfer.import.rows` | Counter | `{row}` | `result` (imported, error, skipped_by_filter, skipped_stalled, empty) | Rows read, counted once their progress is saved |
| `crestapps.contenttransfer.import.rows.skipped` | Counter | `{row}` | `reason` (the filter's `SkipReasonCode`: duplicate, repeated_in_file, dnc_match, dnc_check_failed, unreadable_number; anything else other) | Rows row filters skipped, counted once saved |
| `crestapps.contenttransfer.import.row.errors` | Counter | `{row}` | `reason` (validation, handler_exception) | Rows that failed to import, counted once saved |
| `crestapps.contenttransfer.import.abandoned` | Counter | `{run}` | `reason` (row_timeout, run_timeout, host_stopping) | Runs the watchdog gave up on |
| `crestapps.contenttransfer.import.slow_rows` | Counter | `{row}` | none | Rows that ran past the slow-row threshold (1 min) |
| `crestapps.contenttransfer.import.stalled_row.elapsed` | Histogram (long buckets) | s | `reason` | How long the row an abandoned run was on had been running |
| `crestapps.contenttransfer.import.row_in_flight.age` | ObservableGauge | s | none | Age of the oldest import row in flight on this node (no value when none) |
| `crestapps.contenttransfer.import.filter.duration` | Histogram | s | `phase` (prepare, evaluate) | Row filters on one batch of rows (DNC lookups happen here) |
| `crestapps.contenttransfer.import.uploads` | Counter | `{upload}` | `outcome` (accepted, rejected), `reason` when rejected (not_found, unauthorized, disabled, missing_file, too_large, chunk_too_large, bad_format, invalid_model, invalid_request) | Import file uploads |
| `crestapps.contenttransfer.upload.size` | Histogram | By | `format` | Size of accepted import files |
| `crestapps.contenttransfer.upload.chunks` | Counter | `{chunk}` | `result` (stored, completed, rejected) | Chunks of chunked uploads |
| `crestapps.contenttransfer.upload.chunk.size` | Histogram | By | none | Size of upload chunks |
| `crestapps.contenttransfer.export.duration` | Histogram (long buckets) | s | `format`, `outcome`, `content_type` | One queued export |
| `crestapps.contenttransfer.export.inline.duration` | Histogram (long buckets) | s | `format`, `outcome`, `content_type` | One export run in the request that asked for it (request latency) |
| `crestapps.contenttransfer.export.rows` | Counter | `{row}` | `mode` (inline, queued), `result` (written, excluded) | Content items exports read |
| `crestapps.contenttransfer.export.page.duration` | Histogram | s | `mode` | One export page, query to last written row (exposes the O(n^2) `Skip/Take` paging) |
| `crestapps.contenttransfer.export.pages` | Counter | `{page}` | `mode` | Export pages read |
| `crestapps.contenttransfer.export.file.size` | Histogram | By | `format`, `mode` | Size of exported files |
| `crestapps.contenttransfer.export.failures` | Counter | `{export}` | `reason` (content_type_removed, no_format, exception, notification_failed), `mode` | Failed exports |
| `crestapps.contenttransfer.export.requests` | Counter | `{request}` | `mode` (inline, queued), `format` | Export requests and the queue decision |
| `crestapps.contenttransfer.export.lookup_fallbacks` | Counter | `{lookup}` | `lookup` (completed_by, last_activity) | Values an export handler looked up row by row because the page was not prepared |
| `crestapps.contenttransfer.jobs.triggered` | Counter | `{job}` | `job` (import, export, delete) | Jobs queued to run after the request |
| `crestapps.contenttransfer.file.operation.duration` | Histogram (long buckets) | s | `format`, `operation` (csv: count_rows, flush; xlsx: open, flush, close) | Work a file reader or writer does at once |

Spans:
- `crestapps.contenttransfer.import.run` (a new trace linked to the upload request through the traceparent stored on the entry; span tag `crestapps.contenttransfer.entry.id`)
- `crestapps.contenttransfer.import.batch` (span tags entry id, `crestapps.contenttransfer.rows`, `crestapps.contenttransfer.row.first`)
- `crestapps.contenttransfer.import.filter`
- `crestapps.contenttransfer.export.run` (linked to the request that queued it), `crestapps.contenttransfer.export.inline`, `crestapps.contenttransfer.export.page`
- `crestapps.deferred.contenttransfer_import`, `crestapps.deferred.contenttransfer_export`, `crestapps.deferred.contenttransfer_delete` (the jobs started after a request, linked to it)

Also feeds: `crestapps.background_task.*` (`ImportFilesBackgroundTask`, `ExportFilesBackgroundTask`), `crestapps.deferred_task.duration`, and `crestapps.retry.attempts` (`area=contenttransfer`, `reason=failed_run`).

EventIds (`ContentTransferLog`, module):

| Id | Name | Level |
|---|---|---|
| 16000 | ImportProgress (about once a minute: row n of total, rows/s) | Information |
| 16001 | ImportRowFailed (sampled: first 5 per run, then every 100th; exception type only) | Warning |
| 16002 | ImportRetrying (attempt number) | Warning |
| 16003 | ImportRunAbandoned (reason, run and row elapsed) | Warning |
| 16100 | ExportStarted | Information |
| 16101 | ExportLockTaken | Information |
| 16102 | ExportProgress | Information |
| 16103 | ExportFinished | Information |
| 16104 | ExportFailed | Error |
| 16107 | ExportEntryFailed | Error |
| 16200 | ImportQueued (entry id, content type, size; no file name) | Information |
| 16201 | ExportQueueDecision (count, threshold, RequiresQueue, mode) | Information |

The import run opens a log scope with `EntryId`, `ContentType` and `Direction`.

Behavior notes:
- New extension point: `ContentImportRowFilterContext.SkipReasonCode` (bounded snake_case code, never localized, never a row value). A filter that sets none counts as `other`.
- Rows are counted when the batch that holds them is saved (including the completion save), and never after the run's token is cancelled, so a batch read again after a resume or an abandoned run is not counted twice.
- The import and export tasks are measured with `lockExpiration: null`: each entry runs under its own 30-minute lock, so the task's 30 s lock expiration would make `crestapps.background_task.lock.utilization` a permanent false alarm.

### Do-not-call registry

Meter and activity source: `CrestApps.OrchardCore.DncRegistry` (class `DncRegistryTelemetry` in the DncRegistry module).

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.dnc.screening.duration` | Histogram | s | `registry` (usa_ftc, canada_dncl, local), `outcome` (ok, error, skipped, cancelled, timeout), `reason` (not_configured, only when skipped) | One screening of numbers against one registry |
| `crestapps.dnc.screening.size` | Histogram | `{number}` | `registry` | Numbers one screening asked about |
| `crestapps.dnc.screening.listed` | Counter | `{number}` | `registry` | Screened numbers reported as listed |
| `crestapps.dnc.import.duration` | Histogram (long buckets) | s | `outcome`, `reason` | One local list import run |
| `crestapps.dnc.import.rows` | Counter | `{row}` | `result` (imported, duplicate, invalid_number, blank, multiple_columns, header) | File rows, counted once their batch is saved |

Spans: `crestapps.dnc.screening` (numbers checked/listed as span tags), `crestapps.dnc.import.run` (list id, resuming, total rows as span tags), `crestapps.deferred.dnc_local_import`, `crestapps.deferred.dnc_local_delete`.

Also feeds: `crestapps.background_task.*` (`LocalDncImportBackgroundTask`, no lock utilization), `crestapps.retry.attempts` (`area=dnc`, `reason=failed_import`), `crestapps.deferred_task.duration`.

EventIds (`DncRegistryLog`): 16400 ImportRunEnded (Information). The import run opens a `DncListId` log scope.

### Omnichannel bulk work

Meter and activity source: `CrestApps.OrchardCore.Omnichannel` (class `OmnichannelTelemetry` in `CrestApps.OrchardCore.Omnichannel.Core`).

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.omnichannel.batch.load.duration` | Histogram (long buckets) | s | `outcome`, `trigger` | One load run |
| `crestapps.omnichannel.batch.load.page.duration` | Histogram | s | none | One committed page of a load |
| `crestapps.omnichannel.batch.load.records` | Counter | `{record}` | `result` (loaded, duplicate, opted_out, no_destination, not_in_service, lead_or_contact, over_limit) | Records a load went through, counted after commit; never in preview |
| `crestapps.omnichannel.batch.load.stalled` | Gauge | `{load}` | none | Stalled loads found on the watchdog's last sweep (recorded synchronously, no query in a callback) |
| `crestapps.omnichannel.batch.load.preview.duration` | Histogram | s | `outcome` (ok, stopped, error, cancelled) | Counting a load ahead of loading (CountMatches), in the request |

Spans: `crestapps.omnichannel.batch.load` (span tag `crestapps.omnichannel.batch.id`), `crestapps.omnichannel.batch.load.preview`, `crestapps.deferred.omnichannel_batch_load` (linked to the request that started the load), `crestapps.deferred.omnichannel_opportunity_contact_sync`, `crestapps.deferred.omnichannel_contact_memory_rebuild`.

Also feeds: `crestapps.background_task.*` for the five Omnichannel.Managements tasks, `crestapps.contenttransfer.export.lookup_fallbacks`, `crestapps.contenttransfer.import.rows.skipped` (codes set by `OmnichannelContactImportRowFilter`).

The load run opens a `BatchId` log scope.

### Recipes and deployments

Meter: `CrestApps.OrchardCore.Recipes` (the internal `RecipeStepTelemetry` in `CrestApps.OrchardCore.Core`). Orchard Core's own `Execute recipe` and `Execute recipe step` spans cover recipe execution, so the modules add only what those cannot see: what a named-catalog recipe step did with its entries.

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.recipes.step.entries` | Counter | `{entry}` | `step` (step name set in code), `result` (created, updated, invalid, skipped) | Entries a named-catalog recipe step read |

EventIds: 16710 EntriesFailedValidation (Warning; counts only, validation errors used to reach only `context.Errors`), 16711 StepImported (Debug).

Behavior notes: `updated` is computed as valid minus created (the base `CommitUpdateAsync` is sealed); `skipped` is counted only when the step succeeds. Recipes the AI agent tools import are counted on `crestapps.recipes.ai_imports` (see [AI modules](#ai-modules)).

### Inventory

Meter and activity source: `CrestApps.OrchardCore.Inventory` (class `InventoryTelemetry` in `CrestApps.OrchardCore.Inventory.Core`).

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.inventory.stock_count.duration` | Histogram | s | `outcome` (ok, truncated, error, cancelled) | A stock count import, from a file or a recipe (in-request, 10k-row cap) |
| `crestapps.inventory.stock_count.rows` | Counter | `{row}` | `result` (counted, unchanged, not_found, invalid_count, duplicate, rejected) | Stock count rows |
| `crestapps.inventory.movements` | Counter | `{movement}` | `type` (received, adjusted, counted, sold, returned, transferred, consumed, other), `direction` (in, out, none) | Stock movements written |
| `crestapps.inventory.movement.units` | Counter | `{unit}` | `type`, `direction` | Absolute units those movements moved |
| `crestapps.inventory.holds` | Counter | `{hold}` | `outcome` (placed, converted, committed_without_hold, released, expired) | Stock hold state changes |
| `crestapps.inventory.hold.refusals` | Counter | `{order}` | `reason` (shortage) | Orders refused a hold because a line was short |

Spans: `crestapps.inventory.stock_count.import` (row totals as span tags), `crestapps.deferred.inventory_stock_level_dispatch`, `crestapps.deferred.inventory_product_sync`, `crestapps.deferred.inventory_sync_all`, `crestapps.deferred.inventory_import_counts`.

Also feeds: `crestapps.background_task.*` (`ExpiredStockHoldsBackgroundTask`: `skipped` when nothing expired, `budget_exceeded` on a full 500-hold batch), `crestapps.deferred_task.duration`.

Behavior notes: inventory changes run in the caller's unit of work, so movements and holds are counted when made; a unit of work rolled back or retried later still counts (approximate).

## Contact center and webhooks

Meters/sources: `CrestApps.OrchardCore.ContactCenter` (existing `ContactCenterDiagnostics`, kept) and `CrestApps.OrchardCore.Webhooks` (new `WebhookTelemetry` in Webhooks.Core). The full instrument, span and EventId list below is also in src/CrestApps.Docs/docs/contact-center/production-support.md (section Telemetry contract).

### Instruments

Every instrument is recorded where the state changes (an event applied to a call, an offer settled, a caller leaving a queue), never in an outbox or projection handler, so a redelivered domain event is never counted twice. Metrics carry bounded tags only: no call, interaction, reservation, activity or queue id, no phone number, address or SIP URI, no tenant name unless tenant tags are on. Spans carry ids (`crestapps.contactcenter.interaction.id`, `crestapps.contactcenter.reservation.id`...) but never a phone number. Durations are in seconds.

#### Calls (every provider feeds the same funnel)

| Instrument | Kind | Tags | Meaning |
| --- | --- | --- | --- |
| `crestapps.contactcenter.provider_event` | Counter | `provider`, `state`, `result` | Provider voice events the call stream ingested. `result`: `applied`, `duplicate`, `foreign` (a provider that does not own the call), `unmatched` (no interaction), `conflict` (bound to another call), `retried` (lost a write race and ran again). Stale events are counted once on `crestapps.telephony.voice_event.discards` by `reason`, not here. |
| `crestapps.contactcenter.provider_event.lag` | Histogram (s) | `provider` | From the provider's timestamp to ingestion: the webhook and queueing lag. |
| `crestapps.contactcenter.call.setup.duration` | Histogram (s) | `direction`, `provider` | From dialing or ringing to answer. |
| `crestapps.contactcenter.call.duration` | Histogram (s) | `direction`, `provider`, `answered` | From start to end. |
| `crestapps.contactcenter.call.completed` | Counter | `direction`, `provider`, `hangup.cause`, `hangup.source` | Calls that ended. `hangup.source`: `caller`, `callee`, `agent`, `supervisor`, `system`, `unknown`, `other`. |
| `crestapps.contactcenter.call.abandoned` | Counter | `stage` (`offer`, `queue`, `pre_routing`), `kind` | Inbound callers who hung up before anybody answered. |
| `crestapps.contactcenter.amd.result` | Counter | `result` (`human`, `machine`, `fax`, `unknown`) | Answering machine detection, once per call. |
| `crestapps.contactcenter.call.quality.mos` / `.jitter` (s) / `.loss` (ratio) | Histogram | | One measurement per leg quality report. |
| `crestapps.contactcenter.call.quality.alerts` | Counter | | Poor calls that crossed an agent's alert threshold (the alert is published once per agent per window). |

#### Offers, queues and inbound routing

| Instrument | Kind | Tags | Meaning |
| --- | --- | --- | --- |
| `crestapps.contactcenter.offer.settled` | Counter | `outcome`, `channel`, `kind` | Offers settled: `accepted`, `declined`, `expired`, `cancelled`, `superseded`, `unreachable`. `kind`: `queue`, `direct`, `campaign`. Counted after the transition commits. |
| `crestapps.contactcenter.offer.ring.duration` | Histogram (s) | as above | How long the offer rang. |
| `crestapps.contactcenter.offer.keepalive` | Counter | `kind` | Preview offers an agent kept from ringing out. |
| `crestapps.contactcenter.reservation.reserve` | Counter | `result`, `kind` | Why work was or was not offered: `reserved`, `lock_timeout`, `queue_item_gone`, `agent_unavailable`. |
| `crestapps.contactcenter.cas.conflict` | Counter | `site` | Transitions that lost a compare-and-set race, such as `reservation.accept` or `provider_event.ingest`. |
| `crestapps.contactcenter.queue.item` | Counter | `channel`, `kind` | Work that entered a queue. |
| `crestapps.contactcenter.queue.wait.duration` | Histogram (s) | `outcome`, `channel`, `kind` | Time in a queue by how the caller left: `assigned`, `abandoned`, `voicemail`, `overflowed`, `timeout`, `completed`, `removed`. |
| `crestapps.contactcenter.queue.overflow` | Counter | `channel` | Callers moved on to an overflow queue. |
| `crestapps.contactcenter.queue.max_wait_action` | Counter | `action` (`overflow`, `voicemail`), `result` (`applied`, `no_target`) | The maximum-wait action applied. |
| `crestapps.contactcenter.queue.admission` | Counter | `result` | Admission to a queue with a size limit: `admitted`, `voicemail`, `overflowed`, `admitted_over_limit`. |
| `crestapps.contactcenter.inbound.routed` | Counter | `result` (`offered`, `queued`, `not_routed`, `duplicate`), `reason` | Inbound calls routed; `reason` is the routing reason code (`ai_agent`, `ivr_menu`, `entry_point_welcome`, `queue_full_voicemail`...). A redelivered inbound webhook for a call already tracked counts as `duplicate`. |
| `crestapps.contactcenter.inbound.route.duration` | Histogram (s) | `provider`, `result`, `reason`, `outcome` | Time spent routing an inbound call. |
| `crestapps.contactcenter.inbound.ai_answer.duration` | Histogram (s) | `provider` | From the provider receiving a call to routing handing it to the entry point's AI voice agent. |

#### Event outbox

| Instrument | Kind | Tags | Meaning |
| --- | --- | --- | --- |
| `crestapps.contactcenter.outbox.dispatches` | Counter | `delivery` (`first`, `retry`), `result` (`completed`, `retry`) | Dispatches of domain events to their handlers. |
| `crestapps.contactcenter.outbox.lag` | Histogram (s) | `delivery` | From the event being recorded to a dispatch of it starting. |
| `crestapps.contactcenter.outbox.handler.duration` | Histogram (s) | `handler`, `result` | One handler handling one event. |
| `crestapps.contactcenter.outbox.redelivered` | Counter | | Events redelivered by the retry pass. |
| `crestapps.contactcenter.outbox.dead_lettered` | Counter | `reason` | Events dead-lettered. |

These measure the delivery mechanism, so a redelivered event is a second dispatch; the domain counts above never are.

#### Provider commands and the dialer

| Instrument | Kind | Tags | Meaning |
| --- | --- | --- | --- |
| `crestapps.contactcenter.provider_command.dispatch.duration` | Histogram (s) | `provider`, `command.type`, `outcome` | One dispatch of a durable provider command. |
| `crestapps.contactcenter.provider_command.outcome` | Counter | `provider`, `command.type`, `outcome` | Commands that reached `confirmed`, `unknown`, `failed`, `compensated` or `paused`; counted on the change of state only. |
| `crestapps.contactcenter.provider_command.recovered` | Counter | | Commands the recovery pass picked up again. |
| `crestapps.contactcenter.dialer.pacing.cycle` | Counter | `outcome`, `dialer.mode` | Pacing cycles by how they ended (`DialerPacingCycleOutcome`). |
| `crestapps.contactcenter.dialer.pacing.cycle.duration` | Histogram (s) | `outcome`, `dialer.mode` | Time a cycle spent pacing under its queue's lock. |
| `crestapps.contactcenter.dialer.pacing.placed` | Counter | `dialer.mode` | Calls pacing cycles placed. |
| `crestapps.contactcenter.dialer.pacing.lock_starved` | Counter | | A campaign queue found not paced because its pacing lock stayed out of reach. |
| `crestapps.contactcenter.dialer.attempt` | Counter | `dialer.mode`, `result` (`dialed`, `suppressed`, `not_started`), `suppress.reason` | Dialer attempts. |
| `crestapps.contactcenter.dialer.attempt.completed` | Counter | `result` | Attempts the dialer dispositioned on its own, by terminal reason. |
| `crestapps.contactcenter.dialer.abandoned` | Counter | `reason` | Answered dialer calls abandoned (no agent in time). |
| `crestapps.contactcenter.dialer.connect.duration` | Histogram (s) | | From a person answering a dialer call to an agent being connected. |
| `crestapps.contactcenter.dialer.overdial` | Counter | | Calls predictive pacing placed beyond the agents free to take them. |
| `crestapps.contactcenter.dialer.eligibility` | Counter | `result`, `reason` | Eligibility checks; `reason` is the `DialerSuppressionReason`. |
| `crestapps.contactcenter.dialer.dnc.lookup.duration` | Histogram (s) | `result` (`listed`, `clear`, `error`) | National do-not-call lookups on the dial path. |

#### Agents, recovery and deadlines

| Instrument | Kind | Tags | Meaning |
| --- | --- | --- | --- |
| `crestapps.contactcenter.agent.state.duration` | Histogram (s) | `state` | Time an agent spent in a presence state before leaving it. |
| `crestapps.contactcenter.recovery.healed` | Counter | `kind` | Agent or work states a healing pass put right. Any steady rate is a state-machine leak: alert on it. |
| `crestapps.contactcenter.recovery.orphan_recovered` | Counter | `kind` (`activity`, `reservation`) | Orphaned work recovered. Alert on a steady rate. |
| `crestapps.contactcenter.hub.rejected` | Counter | `reason` | Hub connections refused. |
| `crestapps.contactcenter.realtime.broadcast.duration` | Histogram (s) | `event.type` | From a change to its broadcast to agent and supervisor screens: how long an offer takes to reach the agent. |
| `crestapps.contactcenter.realtime.notify.failures` | Counter | `event.type` | Broadcasts that failed. |
| `crestapps.contactcenter.deadline.fire.lag` | Histogram (s) | `kind` | From a deadline being due to its work starting. |
| `crestapps.contactcenter.deadline.conflict_retry` | Counter | `kind` | Deadlines run again after losing a write race. |
| `crestapps.contactcenter.deadline.failed` | Counter | `kind`, `reason` | Deadlines left to the background sweep. |
| `crestapps.contactcenter.shutdown.drain.duration` | Histogram (s) | `outcome` | The drain a node runs before shutting down. |

#### Features

| Instrument | Kind | Tags | Meaning |
| --- | --- | --- | --- |
| `crestapps.contactcenter.treatment.played` | Counter | `kind` | Queue treatment steps a waiting caller heard. |
| `crestapps.contactcenter.hold_music.stop_failed` | Counter | `error.type` | Failed hold-music stops (swallowed so the caller still moves on). |
| `crestapps.contactcenter.transfer` | Counter | `kind` (`blind_external`), `result` (`ok`, `error`, `unknown`) | Transfers of a call out of the contact center. |
| `crestapps.contactcenter.recording` | Counter | `action` (the requested `RecordingState`), `result` (`ok`, `error`, `unknown`, `refused`) | Recording state changes asked of the provider. |
| `crestapps.contactcenter.callback` | Counter | `operation` (`scheduled`, `promoted`) | Callbacks. |
| `crestapps.contactcenter.supervision` | Counter | `action`, `result` | Supervisor monitor-mode changes and stops. |
| `crestapps.contactcenter.handoff.duration` | Histogram (s) | `result` (`offered`, `queued`, `callback`, `refused`, `duplicate`) | Hand-offs from an AI voice agent to a person; its count is the number of hand-offs. A redelivered escalation of a call already handed off is a `duplicate`. |

#### Gauges

| Instrument | Kind | Tags | Meaning |
| --- | --- | --- | --- |
| `crestapps.contactcenter.queue.depth` | ObservableGauge | | Interactions waiting across every queue. |
| `crestapps.contactcenter.backlog` | ObservableGauge | `subsystem` (`outbox`, `provider_ingress`), `state` (`dead_lettered`, `overdue`) | The backlog the health checks read. |
| `crestapps.contactcenter.backlog.status` | ObservableGauge | `subsystem` | The health `BacklogHealthEvaluator` decided: 0 healthy, 1 degraded, 2 unhealthy. |
| `crestapps.contactcenter.outbox.pending.age` | ObservableGauge (s) | | Age of the oldest event due for redelivery at the last retry pass. |
| `crestapps.contactcenter.node.serving` | ObservableGauge | | 1 while the node serving gate lets this node take work, 0 once drained. |

A gauge callback cannot query a tenant's store, so these report what the Contact Center health checks and the outbox retry pass last read; they refresh only when the health endpoint is probed (or the pass runs) and stop reporting a value not refreshed for 15 minutes. Without the tenant tag, counts are summed across tenants and ages take the oldest. Every node reads the same shared store, so aggregate across nodes with `max`, not `sum`.

#### Webhooks (`CrestApps.OrchardCore.Webhooks`)

| Instrument | Kind | Tags | Meaning |
| --- | --- | --- | --- |
| `crestapps.webhook.inbox.accepted` | Counter | `provider`, `result` (`accepted`, `duplicate`, `busy`) | Provider webhook deliveries offered to the inbox. |
| `crestapps.webhook.inbox.dispatches` | Counter | `delivery`, `result` (`completed`, `retry`, `conflict`, `handler_unavailable`) | Inbox dispatches. |
| `crestapps.webhook.inbox.handler.duration` | Histogram (s) | `handler`, `outcome` | One inbox handler handling one delivery. |
| `crestapps.webhook.inbox.lag` | Histogram (s) | `delivery` | From acceptance to a dispatch starting. |
| `crestapps.webhook.inbox.dead_lettered` | Counter | `provider`, `handler` | Deliveries dead-lettered after their retries; also logged as an error (event id 17950). |
| `crestapps.webhook.inbox.pending.age` | Gauge (s) | | Age of the oldest delivery due at the last inbox pass. |
| `crestapps.webhook.deliveries` | Counter | `outcome` (`ok`, `retry`, `error`), `reason`, `status_class` | Outbound delivery attempts. |

Every background task reports on `crestapps.background_task.outcomes` (a failure a task logs and swallows is reported as `error`).

#### Spans

| Span | Links to |
| --- | --- |
| `crestapps.contactcenter.call.ingest` | |
| `crestapps.contactcenter.offer` (tag `operation`: `reserve`, `accept`, `reject`, `cancel`, `compensate`, `expire`, `release_unreachable`) | |
| `crestapps.contactcenter.inbound.route` | |
| `crestapps.contactcenter.outbox.dispatch` | The trace that published the event (its `traceparent` is stored with the event). |
| `crestapps.contactcenter.provider_command.dispatch` | The trace that registered the command, such as a dialer attempt. |
| `crestapps.contactcenter.dialer.attempt` | |
| `crestapps.contactcenter.shutdown.drain` | |
| `crestapps.webhook.inbox.dispatch` | The request that delivered the payload. |

Work that crosses a queue (the outbox, the provider command recovery pass, the webhook inbox retry pass) starts a span of its own that links to the trace that queued it, rather than joining a request that ended long ago.

#### Log event ids

New Contact Center log messages carry event ids in `17000`–`17899` (core services `17000`–`17599`, the module `17600`–`17899`); webhooks use `17900`–`17999`. Among them: `17100` a provider command settled, `17901` an outbound delivery failed for good, `17950` an inbox delivery dead-lettered.

### Behavior notes

- The provider webhook inbox lives in Webhooks.Core, so its instruments are `crestapps.webhook.inbox.*` on the Webhooks meter.
- Stale/out-of-order provider voice events are counted once, on `crestapps.telephony.voice_event.discards` (reason from `VoiceStreamOrdering.GetDiscardReason`); `crestapps.contactcenter.provider_event` does not count them (span tag only).
- Trace context: `DefaultContactCenterEventPublisher` stores the traceparent in `InteractionEvent.EntityProperties["traceparent"]`; the outbox dispatch span links to it. `ProviderCommand.TraceParent` (new, public) is captured in `ProviderCommandStateService.RegisterAsync`; `ProviderWebhookInboxMessage.TraceParent` (new) comes from `ProviderWebhookInboxDelivery.TraceParent` (new, optional) or the current trace at acceptance.
- Domain counters in `ActivityReservationService` are recorded after `CommitTransitionAsync` succeeds; a lost race counts on `cas.conflict{site}` instead. The queue counters and a few others are recorded in the caller's unit of work and are approximate (see [Rules every instrument follows](#rules-every-instrument-follows)).
- Observable gauges read an in-memory per-tenant snapshot that the health checks / outbox retry pass write (no store query in a callback); a snapshot not refreshed for 15 minutes is dropped; aggregate with max across nodes.
- `realtime.broadcast.duration` is a delivery-latency measure taken in the real-time outbox handler, so a redelivered event is measured again (with its redelivery delay).
- Exception messages are never put on spans (type only).

## AI voice, telephony, WebSockets and phone verification

| Area | Meter / activity source | Class |
|---|---|---|
| AI voice | `CrestApps.OrchardCore.Voice` (meter and source) | `CrestApps.OrchardCore.Omnichannel.Voice.Telemetry.VoiceTelemetry` (Omnichannel.Voice.Core) |
| Telnyx | `CrestApps.OrchardCore.Telnyx` (meter only) | `CrestApps.OrchardCore.Telnyx.Telemetry.TelnyxTelemetry` (Telnyx.Core) |
| Telephony (every provider) | `CrestApps.OrchardCore.Telephony` (meter and source) | `CrestApps.OrchardCore.Telephony.Telemetry.TelephonyTelemetry` (Telephony.Core) |
| Asterisk | `CrestApps.OrchardCore.Asterisk` (meter only) | `CrestApps.OrchardCore.Asterisk.Telemetry.AsteriskDiagnostics` (Asterisk module) |
| WebSockets | `CrestApps.OrchardCore.WebSockets` (meter only) | `CrestApps.OrchardCore.WebSockets.Telemetry.WebSocketsTelemetry` (WebSockets module) |
| Phone verification | `CrestApps.OrchardCore.PhoneNumbers` (meter and source) | `CrestApps.OrchardCore.PhoneNumbers.Verifications.Telemetry.PhoneNumberVerificationTelemetry` (PhoneNumbers.Verifications module) |

All of them match the `CrestApps.OrchardCore.*` wildcard. Metrics carry the tenant tag when tenant tags are on (the observable gauges are process-wide and do not).

Never on a metric: the call-control id, the provider call id, activity/interaction/session ids, the WebSocket rendezvous key, phone numbers, SIP URIs, request paths, transcripts.

### AI voice (`CrestApps.OrchardCore.Voice`)

The usage report's per-call measurements (`AIVoiceSessionTracker` / `AIVoiceSessionSummaryWriter`) are emitted where the one summary of a call is written, so a live call, which is handed over twice (by its session and by the provider's hangup), is counted once: the call by its first summary, its audio by the one summary that carries it. The latencies are the raw per-answer values the meter already measured, not recomputed.

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.voice.ai.sessions` | Counter | `{session}` | `engine` (`realtime`, `turn_based`), `outcome` (`completed_by_ai`, `handed_to_agent`, `voicemail`, `no_answer`, `caller_hung_up`, `failed`, `transferred`, `unknown`), `deployment`, `provider` | Automated calls summarized, once per call. Written only while the AI usage observer is registered (it always is with CrestApps.Core AI). |
| `crestapps.voice.ai.session.duration` | Histogram | `s` (long buckets) | `engine`, `deployment` | The assistant's part of a measured call. |
| `crestapps.voice.ai.response.latency` | Histogram | `s` | `engine`, `deployment` | Each answer: the caller's turn to the assistant's first audio. |
| `crestapps.voice.ai.model.latency` | Histogram | `s` | `engine`, `deployment` | Each answer's model time. |
| `crestapps.voice.ai.detector.wait` | Histogram | `s` | `engine`, `deployment` | How long the turn detector waited after the caller stopped, per answer where it was seen. |
| `crestapps.voice.ai.first_audio.latency` | Histogram | `s` | `engine`, `deployment` | The answer to the caller first hearing the assistant. |
| `crestapps.voice.ai.barge_ins` | Counter | `{barge_in}` | `engine`, `deployment` | Callers talking over the assistant (live sessions only; a turn-based call does not listen while it speaks). |
| `crestapps.voice.ai.session.summary.lost` | Counter | `{summary}` | `reason` (`scope`, `write`) | Usage summaries that could not be written. |
| `crestapps.voice.ai.session.reconnects` | Counter | `{attempt}` | `outcome` (`ok`, `error`, `cancelled`, `timeout`) | Attempts to replace a live session lost with the caller still on the line. |
| `crestapps.voice.ai.session.errors` | Counter | `{error}` | `kind` (`refused`, `unexpected`, `repeated`, `fatal`, `setup_refused`), `result` (`continued`, `ended`) | Errors a live session reported, and whether the call carried on. |
| `crestapps.voice.ai.voicemail.machine_detected` | Counter | `{call}` | `source` (`provider`, `transcript`), `result` (`voicemail`, `phone_menu`) | Calls a machine answered. Counted once, where the loop first marks the call, for both engines. |
| `crestapps.voice.ai.idle_prompts` | Counter | `{prompt}` | `engine`, `action` (`ask`, `goodbye`) | The assistant speaking up on a line quiet on both sides (live sessions and the turn-based loop). |
| `crestapps.voice.ai.unheard_prompts` | Counter | `{prompt}` | `engine` (`realtime`) | The assistant asking a caller to repeat an answer the live session never reported hearing. |
| `crestapps.voice.ai.guardrail.refusals` | Counter | `{refusal}` | `engine`, `direction` (`customer`, `reply`) | Caller lines or assistant replies a profile's guardrails refused on a call. |
| `crestapps.voice.ai.turn.duration` | Histogram | `s` | `outcome` (`ok` = a reply started playing, `ignored`, `duplicate`, `voicemail`, `overtaken`, `no_reply`, `error`, `cancelled`) | Turn-based loop: the caller's final transcript to the reply's text-to-speech starting. |
| `crestapps.voice.media.streams` | UpDownCounter | `{stream}` | `provider` | Call media streams open for live sessions. |
| `crestapps.voice.media.stream.lost` | Counter | `{stream}` | `provider` | Media streams lost with the caller still on the line (the call is handed on). |
| `crestapps.voice.audio.frames.dropped` | Counter | `{frame}` | `reason` (`no_session`) | Caller audio frames dropped while a lost session was being replaced. |
| `crestapps.voice.audio.send.stalls` | Counter | `{stall}` | `direction` (`to_model`) | Sending the caller's audio to the session took 250 ms or more. |

Spans (source `CrestApps.OrchardCore.Voice`): `crestapps.voice.ai.session` (root of a live session; `engine`, `provider`, `deployment`, `crestapps.omnichannel.activity.id`; outcome `ok`, `lost`, `not_held`, `error`), with children `crestapps.voice.ai.session.setup`, `crestapps.voice.ai.session.reconnect`, `crestapps.voice.ai.session.handover` and `crestapps.voice.ai.voicemail`; `crestapps.voice.ai.turn` (one turn-based turn; the completion's `gen_ai.*` spans nest under it). The live session also opens a log scope with `ActivityId` and `ProviderCallId`.

CrestApps.Core's `crestapps.core.realtime.*` instruments measure its own realtime session runner and provider clients; the instruments above are the phone call's: its outcome, the usage measurements and the call-handling decisions.

### Telnyx (`CrestApps.OrchardCore.Telnyx`)

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.telnyx.webhooks` | Counter | `{delivery}` | `kind` (`call`, `sms`), `type` (known event type, else `other`; `unknown` when not parsed), `result` (`accepted`, `duplicate`, `rejected_signature`, `rejected_stale`, `rejected_rate`, `rejected_size`, `disabled`, `parse_error`, `store_failed`, `error`) | Webhook deliveries received. `rejected_rate` covers the ingress concurrency/rate limits and a busy inbox. |
| `crestapps.telnyx.webhook.lag` | Histogram | `s` | `kind`, `type` | The event's own time to the delivery arriving (validated call deliveries). |
| `crestapps.telnyx.voicemail.record_start.latency` | Histogram | `s` | `outcome` | The greeting-ended delivery arriving to `record_start` being accepted, on the webhook's fast path. |
| `crestapps.telnyx.media.stream.failures` | Counter | `{failure}` | — | Telnyx reporting `streaming.failed` for a call's media stream. |
| `crestapps.telnyx.media.stream.reconnects` | Counter | `{reconnect}` | `result` (`reconnected`) | Broken media streams that came back (a lost stream is counted on `crestapps.voice.media.stream.lost`). |
| `crestapps.telnyx.media.stream.reconnect.duration` | Histogram | `s` | — | A broken stream to the replacement socket taking over. |

Retries of the REST client, the solution's own retry loop, are counted on the shared `crestapps.retry.attempts` with `area` = `telnyx`, `reason` = `status_429`, `status_408`, `status_5xx`, `network` or `timeout`. The requests themselves are on HttpClient's spans and `http.client.request.duration`.

The Telnyx module starts no spans of its own: each webhook delivery puts its `crestapps.telnyx.webhook.type` and `crestapps.telnyx.webhook.result` on the ASP.NET Core request span, and the queued call events are processed under the webhook inbox's linked `crestapps.webhook.inbox.dispatch` span.

`SoftPhoneHealthMetrics` (the health canary's in-memory counters) is bridged: credential issuance is also recorded on `crestapps.telephony.softphone.credentials`; webhook processing is not bridged because `crestapps.telnyx.webhooks` already counts every delivery.

### Telephony, every provider (`CrestApps.OrchardCore.Telephony`)

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.telephony.command.duration` | Histogram | `s` | `command` (hub action name such as `Dial`, `Hold`; `unknown` when the caller did not name it), `provider` (when the caller names it), `outcome` (`ok`, `error`, `timeout` = provider outcome unknown, `cancelled` = host stopping after dispatch, `not_admitted` = host stopping before dispatch), `reason` (`unknown` for a result with `OutcomeUnknown`), `error.type` | A provider mutation run by `DefaultTelephonyCommandExecutor` under the server-owned deadline. |
| `crestapps.telephony.dial.screenings` | Counter | `{dial}` | `stage` (`destination_policy`, `compliance`), `result` (`allowed`, `denied`), `reason` (on a denial: the `DialDestinationOutcome` or the screener's stable code, snake_cased, else `other`; `no_decision`) | Soft-phone dials screened in `DefaultTelephonyService.DialAsync`. Allowed dials are counted once, at the compliance stage. |
| `crestapps.telephony.softphone.credentials` | Counter | `{credential}` | `provider`, `operation` (`issue`, `revoke`), `outcome` | Browser SIP credential issuance (Telnyx via `SoftPhoneHealthMetrics`, Asterisk PJSIP) and revocation (both providers' revokers). |
| `crestapps.telephony.reconcile.corrections` | Counter | `{call}` | `provider`, `action` (`orphan_ended`) | Calls the Asterisk inbound channel sweep ended. Any steady rate means events are lost. (The Telnyx reconciler reports its corrections on `crestapps.background_task.items`.) |
| `crestapps.telephony.recording.ingest.duration` | Histogram | `s` (long buckets) | `provider`, `result` (`stored`, `retry`, `cancelled`, `dead_lettered`, `error`), `outcome` | One recording ingest job attempt (Telnyx and Asterisk). |
| `crestapps.telephony.recording.ingest.lag` | Histogram | `s` (long buckets) | `provider` | A job queued to its recording stored. |
| `crestapps.telephony.recording.ingest.backlog_age` | ObservableGauge | `s` | `provider` | The oldest job the runs found due (0 when none), the oldest across tenants, ageing between runs; read from the jobs each run loaded (kept per provider and tenant, the tenant as a key, never a tag; dropped after 15 minutes without a run), never a query in the callback. |
| `crestapps.telephony.voice_event.discards` | Counter | `{event}` | `provider`, `reason` (`after_terminal`, `phase_regression`, `unsequenced`, `sequence_regression`, `out_of_order`) | Defined, with `VoiceStreamOrdering.GetDiscardReason`; recorded by the contact center's `ProviderVoiceEventService` once it adopts it (see open items). |

Spans: `crestapps.telephony.command` (`command`, `provider`), `crestapps.telephony.recording.ingest` (`provider`, `crestapps.telephony.recording.id`).

### Asterisk (`CrestApps.OrchardCore.Asterisk`)

The old `asterisk.realtime.*` counters are no longer emitted; use the `crestapps.asterisk.realtime.*` ones below.

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.asterisk.realtime.ingestion.saturated` | Counter | `{episode}` | `provider` | The ingestion buffer filled and applied backpressure (once per episode). |
| `crestapps.asterisk.realtime.connections` | Counter | `{connection}` | `provider` | ARI event-stream connections established, first and every reconnection. |
| `crestapps.asterisk.realtime.reconnect_attempts` | Counter | `{attempt}` | `provider` | Attempts to re-establish the event stream. |
| `crestapps.asterisk.realtime.connected` | UpDownCounter | `{connection}` | `provider` | Listeners **currently** connected. Zero means call events are not being received. |
| `crestapps.asterisk.realtime.events` | Counter | `{event}` | `provider`, `event_type` (known ARI type such as `StasisStart`, `ChannelDestroyed`; else `other`; `unknown` when unread), `result` (`applied`, `ignored`, `failed`) | ARI events received. |
| `crestapps.asterisk.realtime.event.lag` | Histogram | `s` | `provider` | An event's own timestamp to its dispatch. |
| `crestapps.asterisk.realtime.last_event_age` | ObservableGauge | `s` | `provider` | Time since a connected listener last received an event (the stalest listener of the provider). |
| `crestapps.asterisk.realtime.ingestion.queue_depth` | ObservableGauge | `{event}` | `provider` | Events buffered for dispatch across the provider's connected listeners. |

### WebSockets (`CrestApps.OrchardCore.WebSockets`)

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.websockets.rendezvous` | Counter | `{rendezvous}` | `result` (`registered`, `conflict`, `claimed`, `miss`, `cross_node`, `removed`) | Rendezvous operations of the in-memory and the distributed registries. `cross_node`: the socket arrived on a node other than the one waiting for it. |
| `crestapps.websockets.rendezvous.wait.duration` | Histogram | `s` | — | Registration to the provider's socket arriving. |

### Phone verification (`CrestApps.OrchardCore.PhoneNumbers`)

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.phonenumbers.verification.duration` | Histogram | `s` | `provider` (configured provider key), `result` (`verified`, `invalid`, `failed`, `unverified`), `outcome`, `error.type` | One verification lookup. |

Span: `crestapps.phonenumbers.verification` (`crestapps.content_item.id`). A record whose last lookup failed and is looked up again is counted on `crestapps.retry.attempts` with `area` = `phone_verification`, `reason` = `previous_failure`.

### Recorded on shared platform instruments

| Instrument | What these projects add |
|---|---|
| `crestapps.retry.attempts` | `area` = `telnyx` (REST client), `phone_verification`. |

### `[LoggerMessage]` event ids

Telephony range 19000-19999 split per assembly: 19000-19199 Telnyx, 19200-19399 Asterisk (none added), 19400-19599 Telephony (none added), 19600-19699 WebSockets (none added), 19800-19999 phone verification (none added). AI voice uses 18500-18999.

| EventId | Class | Level | Message |
|---|---|---|---|
| 19011 | `TelnyxLog.VoicemailRecordStartFailed` | Warning | `record_start` was refused on the greeting-ended fast path. |
| 18501 | `VoiceLog.RealtimeTurn` | Debug | A realtime session event, by type and **text length** (the transcript is no longer logged). |
| 18502 | `VoiceLog.RealtimeSessionError` | Debug | A realtime session error event with the provider's error message. |
| 18503 | `VoiceLog.NotInService` | Information | A dialled number was not in service (sanitized, as before). |

### Behavior notes

- `ITelephonyCommandExecutor` gained a default-interface overload `ExecuteAsync(providerName, command, operation)`; the old overload still works and records `command` = `unknown` without a provider.
- `VoiceStreamOrdering.ShouldDiscard` now delegates to the new `GetDiscardReason`, which returns the bounded reason.
- Known skew: when the provider's hangup summary of a live call is written before the session's own summary, its draft says `TurnBased`, so that call is counted once on `crestapps.voice.ai.sessions` with `engine` = `turn_based` and the turn-based outcome; its measurements still arrive with `engine` = `realtime` from the session's summary.

## Messaging and SMS

Meter and activity source: `CrestApps.OrchardCore.Messaging` (`MessagingTelemetry` and `MessagingMeasurement` in `CrestApps.OrchardCore.Omnichannel.Messaging.Core`, namespace `CrestApps.OrchardCore.Omnichannel.Messaging.Core.Telemetry`). Instruments are named `crestapps.messaging.*`.

The Azure Event Grid webhook has its own meter and activity source, `CrestApps.OrchardCore.EventGrid` (`EventGridTelemetry` in the `CrestApps.OrchardCore.Omnichannel.EventGrid` module), because that module does not reference the messaging workspace. Its instruments are named `crestapps.eventgrid.*`.

Outcome values follow the shared vocabulary (`ok`, `error`, `skipped`, `cancelled`, `timeout`). The send, inbound and AI turn durations also carry their bounded `result`, so their count by `result` is the number of messages or turns; an `error` outcome also carries `error.type` (the exception type).

Bounded tag values:

- `channel`: the technical name of an enabled messaging channel in lower case (`sms`, ...), `unknown` when the message's channel is not served. Never the value a provider sent.
- `provider`: the technical name of the registered SMS provider that sent (`Twilio`, `Telnyx`, `AzureCommunicationServices`, ...), `unknown` when none resolved.
- `result` (sends): `sent`, `failed`, `opted_out`, `media_not_supported`, `no_provider`, `error`.
- `result` (outbox retries): `sent`, `rescheduled`, `failed`.
- `source` (opt-outs): `keyword`, `provider_refusal`, `automated_conversation`. `action`: `opt_out`, `opt_in`.
- `status` (delivery): `queued`, `sent`, `delivered`, `failed`, `undelivered`. `result` (delivery): `applied`, `unmatched`.
- `result` (inbound): `routed`, `duplicate`, `automated` (an AI conversation owns the contact), `ai_started`, `unknown_channel`, `missing_address`, `no_endpoint`, `lock_timeout`, `error`.
- `result` (AI turns): `replied`, `no_reply`, `not_owed`, `superseded`, `lock_timeout`, `completion_failed`, `empty_reply`, `send_failed`, `error`, `cancelled`.
- `result` (AI handoff): `handed_off`, `failed`, `error`. `decision` (respond decision): `respond`, `skip`, `fallback` (no deployment, or the evaluation failed, so the agent replies by default).
- `trigger` (routing): `inbound`, `handoff`, `reassignment`, `manual_transfer`; (assignment) those plus `claim`, `reply`, `assign`, `transfer`.
- `router`: `auto_reply`, `reassignment`, `handoff_queue`, `existing_conversation`, `routed_queue`, `endpoint_route`, `fallback`, `none` (no router claimed it), `other` (a router another module adds).
- `status` (assignment): `unassigned`, `assigned`, `pooled`, `other`.
- `result` (reassigned): `rerouted`, `pooled`, `unassigned`.
- `sla` (first response): `met` (a target was running and not breached), `breached`, `none` (no target).
- `result` (re-engagement and owed-reply recovery items on `crestapps.background_task.items`): `sent`, `send_failed`, `no_content`, `opted_out`, `outside_hours`, `busy`, `error`; `redriven`, `error`.
- `result` (broadcast): `started`, `rejected`, `completed`, `failed`; (broadcast recipient): `sent`, `failed`.
- `trigger` (activity start): `entry_point` (an inbound text to a number routed to an AI agent), `automated` (the activity processor's opening text). `result`: `started`, `already_active`, `quiet_period`, `human_conversation`, `profile_missing`, `declined`, `opted_out`, `send_failed`, `error`.
- Event Grid `type`: `subscription_validation`, `sms_received`, `sms_delivery_report`, `other`. `result` (event): `validated`, `invalid`, `accepted`, `duplicate`, `busy`, `failed`, `stored`, `malformed`.

No metric carries a phone number, a conversation, activity, message or session id, a broadcast id, a message body or an Event Grid event id or subject. Ids go on spans only; message bodies go nowhere.

### Instruments (`CrestApps.OrchardCore.Messaging`)

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.messaging.send.duration` | Histogram | `s` | `channel`, `provider`, `result`, `outcome` (+`error.type`) | Outbound messages handed to a provider and the time it took to accept or refuse them: every workspace send, keyword confirmation and outbox retry (`SmsDispatcher`), and every automated SMS (AI replies, re-engagement nudges, opening texts). |
| `crestapps.messaging.outbox.retry` | Counter | `{attempt}` | `channel`, `result` | Re-attempts the outbox made of queued outbound messages. |
| `crestapps.messaging.optout` | Counter | `{change}` | `channel`, `source`, `action` | Opt-outs and opt-ins recorded: every STOP keyword received (repeats included), START only when the contact was opted out, provider refusals that changed the contact or number, and opt-outs asked of an AI conversation that updated the contact. |
| `crestapps.messaging.delivery` | Counter | `{receipt}` | `channel`, `status`, `result` | Provider delivery receipts, and whether they matched a sent message. |
| `crestapps.messaging.delivery.latency` | Histogram (long-duration buckets) | `s` | `channel`, `status` | Time from the message being created to the receipt that settled it as delivered, failed or undelivered (recorded once per message, on the transition). |
| `crestapps.messaging.inbound.duration` | Histogram | `s` | `channel`, `result`, `outcome` (+`error.type`) | Inbound messages the workspace pipeline (`MessagingInboundProcessor`) processed, and the time each took (routing included). |
| `crestapps.messaging.inbound.lag` | Histogram (long-duration buckets) | `s` | `channel` | Time from the message's `CreatedUtc` to the workspace recording it in a thread. `CreatedUtc` is the provider's receive time where the webhook carries one (Event Grid), otherwise the time the webhook arrived (Twilio, Telnyx), so it measures the provider webhook inbox backlog plus processing. Recorded once per message, when it is recorded in a thread. |
| `crestapps.messaging.ai.turn.duration` | Histogram (long-duration buckets) | `s` | `channel`, `result`, `outcome` (+`error.type`) | Automated (AI) SMS conversation turns and their time, humanized reading and typing pauses included. |
| `crestapps.messaging.ai.handoff` | Counter | `{handoff}` | `channel`, `result` | AI conversations handed off to a person. |
| `crestapps.messaging.ai.respond_decision` | Counter | `{decision}` | `channel`, `decision` | The AI agent's should-respond decisions. |
| `crestapps.messaging.route` | Counter | `{routing}` | `channel`, `trigger`, `router` | Routing passes, by the router that claimed the conversation. |
| `crestapps.messaging.assignment` | Counter | `{assignment}` | `channel`, `trigger`, `status` | Changes of a conversation's assignment (a new conversation always counts once). |
| `crestapps.messaging.sla.breached` | Counter | `{conversation}` | `channel` | Conversations that missed their first-response target, counted once when announced. |
| `crestapps.messaging.reassigned` | Counter | `{conversation}` | `channel`, `result` | Routed conversations nobody picked up in time, placed again. |
| `crestapps.messaging.first_response.duration` | Histogram (long-duration buckets) | `s` | `channel`, `sla` | Time from a contact-started conversation being created to the first reply a person sent (a thread the agent opens is not measured). |
| `crestapps.messaging.broadcast` | Counter | `{broadcast}` | `channel`, `result` | Broadcasts handed to the outreach engine, rejected, and finished (counted on the transition). |
| `crestapps.messaging.broadcast.recipient` | Counter | `{recipient}` | `channel`, `result` | Broadcast recipients sent to. |
| `crestapps.messaging.activity.start` | Counter | `{activity}` | `channel`, `trigger`, `result` | What the SMS activity starters decided (the entry-point AI starter and the automated activity processor's SMS start). |

### Instruments (`CrestApps.OrchardCore.EventGrid`)

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.eventgrid.event` | Counter | `{event}` | `type`, `result` | Events the deliveries carried. |

Shared platform instruments recorded by these modules: `crestapps.background_task.outcomes` / `.items` / `.last_success` / `.lock.utilization` for `MessagingOutboxBackgroundTask` (items `accepted`), `MessagingRoutedReassignmentBackgroundTask` (items `reassigned`), `MessagingFirstResponseSlaBackgroundTask`, `MessagingBroadcastBackgroundTask`, `SmsReEngagementBackgroundTask` (items by re-engagement result; `budget_exceeded` when it stops on its time or item budget) and `SmsOwedReplyRecoveryBackgroundTask` (items `redriven`/`error`; `skipped` when the AI SMS handler is off); `crestapps.deferred_task.duration` with `task=sms_conversation_conclusion`.

### Spans

| Span | Source | Kind | Notes |
|---|---|---|---|
| `crestapps.messaging.send` | Messaging | Internal | One per message handed to a provider. Tags `channel`, `provider`, `crestapps.messaging.result`. |
| `crestapps.messaging.inbound` | Messaging | Internal | One per inbound message; `crestapps.messaging.provider_message.id`, `crestapps.messaging.conversation.id`. The routing span is its child. |
| `crestapps.messaging.route` | Messaging | Internal | One per routing pass; `trigger`, `router`, `crestapps.messaging.conversation.id`. |
| `crestapps.messaging.ai.turn` | Messaging | Internal | One per AI SMS turn; `crestapps.messaging.activity.id`, `crestapps.messaging.ai.session.id`, `crestapps.messaging.result`. The AI completion and send spans are its children. |
| `crestapps.deferred.sms_conversation_conclusion` | Platform | Internal | The deferred AI conclusion check after an AI turn, linked to the turn. |

### Log messages (`[LoggerMessage]`)

| EventId | Name | Level | Where |
|---|---|---|---|
| 18000 | `OutboxMessageFailed` | Warning | Outbox gave up on a message (`Reason` = `retries_exhausted`/`not_retryable`). |
| 18001 | `OutboxMessageRescheduled` | Debug | Outbox re-attempt refused, queued again. |
| 18003 | `DeliveryReceiptUnmatched` | Debug | A delivery receipt matched no conversation or no sent message. |
| 18004 | `FirstResponseBreached` | Information | A conversation missed its first-response target (overdue seconds). |
| 18005 | `ConversationReassigned` | Debug | A routed conversation was placed again. |
| 18006 | `BroadcastStarted` | Information | A broadcast was handed to the outreach engine. |
| 18201 | `OwedReplyRecoveryCompleted` | Information | Summary of an owed-reply recovery pass, when it did anything. |
| 18202 | `AITurnEnded` | Debug | Result and elapsed time of an AI SMS turn. |
| 18400 | `SubscriptionValidationWithoutCode` | Warning | Event Grid validation event without a code. |
| 18401 | `SubscriptionValidationReceived` | Information | Event Grid validation handshake answered. |
| 18402 | `EventReceived` | Information | Event Grid event received. |
| 18403 | `DeliveryReportStored` | Information | Event Grid SMS delivery report stored. |
| 18404 | `EventMalformed` | Warning | Event Grid event could not be mapped. |
| 18405 | `EventUnmapped` | Debug | Event Grid event type has no mapping. |
| 18406 | `EventMapped` | Information | Event Grid SMS mapped to the platform's SMS received event. |

Sub-ranges: 18000-18099 Messaging.Core, 18100-18199 Messaging.Sms.Core, 18200-18299 Omnichannel.Sms (AI SMS), 18400-18449 Event Grid. 18300-18399 and 18450-18499 are free.

### Behavior notes

- `SmsOmnichannelEventHandler`'s log scope now also carries `Channel` (`SMS`), `Direction` (`inbound`) and `ConversationId` (the AI chat session id, which is the automated conversation's id) next to the existing `ActivityId` and `SessionId`. The Event Grid endpoint opens a scope per event with `EventGridEventId` and `EventType` (never the subject, which is a phone number); its log templates still name the event id `{EventId}`.
- The Event Grid request itself is measured by ASP.NET Core (`http.server.request.duration`); the endpoint puts its `crestapps.eventgrid.result` on that request span.
- The AI conclusion check after an SMS turn now runs through `DeferredWork.Add("sms_conversation_conclusion", ...)` instead of `ShellScope.AddDeferredTask` because it runs an AI call after the turn and its failures were only a log line.
- A messaging span that ends with an exception records the exception type (`error.type`) but never its message or stack: provider and database errors can quote a phone number. The exception is still logged where it is handled.
- `crestapps.messaging.delivery` only covers providers that call `IMessagingConversationService.ApplyDeliveryReceiptAsync` (Telnyx today). Azure Communication Services delivery reports arriving through Event Grid are only counted as `crestapps.eventgrid.event{type=sms_delivery_report,result=stored}`, and Twilio status callbacks are not wired (both gaps predate this change).
- Messaging background tasks that log and swallow a failure now report it on `crestapps.background_task.outcomes` as `error`.
- The workspace sends are measured in `SmsDispatcher` and the automated sends at their call sites in the SMS module; the outbox records `outbox.retry` only, so a retried SMS is not measured twice on `crestapps.messaging.send.duration`.
- Delivery and assignment counters are recorded before the conversation's changes commit; a retried inbox delivery can count them twice (approximate).

## Commerce

One meter and one activity source per area, each named `CrestApps.OrchardCore.<Area>` and kept in the area's own `.Core` project (the marketplace has no `.Core`, so its class is in the module):

| Area | Meter / activity source | Class |
|---|---|---|
| Checkout | `CrestApps.OrchardCore.Checkout` | `CrestApps.OrchardCore.Checkout.Core.Telemetry.CheckoutTelemetry` (Checkout.Core) |
| Stripe | `CrestApps.OrchardCore.Stripe` | `CrestApps.OrchardCore.Stripe.Core.Telemetry.StripeTelemetry` (Stripe.Core) |
| Subscriptions | `CrestApps.OrchardCore.Subscriptions` | `CrestApps.OrchardCore.Subscriptions.Core.Telemetry.SubscriptionsTelemetry` (Subscriptions.Core) |
| Orders | `CrestApps.OrchardCore.Orders` (meter only) | `CrestApps.OrchardCore.Orders.Core.Telemetry.OrdersTelemetry` (Orders.Core) |
| Taxation | `CrestApps.OrchardCore.Taxation` (meter only) | `CrestApps.OrchardCore.Taxation.Core.Telemetry.TaxationTelemetry` (Taxation.Core) |
| Products | `CrestApps.OrchardCore.Products` | `CrestApps.OrchardCore.Products.Core.Telemetry.ProductsTelemetry` (Products.Core) |
| Marketplace | `CrestApps.OrchardCore.Marketplace` (meter only) | `CrestApps.OrchardCore.Marketplace.Telemetry.MarketplaceTelemetry` (Marketplace module) |

All of them match the `CrestApps.OrchardCore.*` wildcard. Metrics carry the tenant tag when tenant tags are on.

Tag policy for commerce: never an order, customer, payment, invoice, subscription, plan, job, vendor or event id, an amount, card data or a tenant name on a metric. `provider` is always the key or name of a registered provider after it is resolved (never a key a request named: an unresolved key is `unknown`). Spans carry the ids listed below.

### Checkout (`CrestApps.OrchardCore.Checkout`)

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.checkout.duration` | Histogram | `s` | `operation` (`start`, `complete`, `cancel`, `expire`), `outcome`, `result` | Each call of `DefaultCheckoutEngine` (`begin_payment` is counted on `crestapps.checkout.payment.begin` instead). For `complete`/`cancel`/`expire`, `result` is the returned status (`completed`, `already_completed`, `pending`, `blocked`, `failed`, `cancelled`, `not_found`). |
| `crestapps.checkout.session.duration` | Histogram (long buckets) | `s` | `outcome` = `completed`, `failed`, `cancelled`, `expired` | Checkouts that reached an end and the time from their start (`CreatedUtc`) to it; its count is the number of checkouts that ended. Counted once at the committed transition; polls that return `pending`/`already_completed` are never counted. |
| `crestapps.checkout.payment.begin` | Counter | `{payment}` | `provider`, `outcome` = `ok`, `requires_action`, `declined`, `requires_auth`, `error`, `refused`, `skipped`; `reason` = `busy`, `not_found`, `finalized`, `no_invoice`, `provider_unavailable`, `gate_refused`, `unsupported`, `nothing_due`, `provider_error` | One per `BeginPaymentAsync` call. |
| `crestapps.checkout.compensations` | Counter | `{payment}` | `provider`, `result` = `refunded`, `failed` | Settled payments given back because their checkout could not complete (`failed` = must be refunded by hand). |
| `crestapps.checkout.fulfillment.deferred` | Counter | `{session}` | (tenant only) | Paid checkouts whose completing handlers failed and were left to the reconciliation sweep. Any steady rate means buyers pay and wait. |
| `crestapps.checkout.reconcile.attempts` | Counter | `{attempt}` | `provider`, `result` = `settled`, `failed`, `still_pending`, `rejected`, `no_provider`, `error`, `timeout`, `cancelled` | Each check of a payment attempt against its provider. Polls are counted, not unique attempts. |
| `crestapps.checkout.reconcile.oldest_pending_age` | Gauge (synchronous) | `s` | (tenant only) | Age of the oldest checkout the reconciliation sweep found waiting on a provider or on its fulfillment; recorded by each sweep (every 5 minutes) from what the sweep already read, 0 when none. |
| `crestapps.checkout.reconcile.pending` | Gauge (synchronous) | `{session}` | (tenant only) | Number of checkouts the sweep found waiting. |

Spans: `crestapps.checkout.start`, `crestapps.checkout.begin_payment`, `crestapps.checkout.complete`, `crestapps.checkout.cancel`, `crestapps.checkout.expire` (tag `crestapps.checkout.session.id`). The provider's own API calls are on HttpClient's spans. A completion whose fulfillment failed sets `crestapps.checkout.fulfillment_deferred=true` on the `complete` span.

### Stripe (`CrestApps.OrchardCore.Stripe`)

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.stripe.webhook.events` | Counter | `{event}` | `event.type` (one of the 16 handled types, else `other`; `unknown` before the payload verified), `outcome` = `ok`, `queued`, `duplicate`, `busy`, `invalid_signature`, `not_configured`, `error`, `inbox_error` | Each webhook delivery, counted once when received (`queued` = stored in the provider webhook inbox). Inbox retries are not deliveries. |
| `crestapps.stripe.webhook.handler.duration` | Histogram | `s` | `event.type`, `outcome` | Time the payment handlers took for an event, in the inline path and the inbox path (both go through `CreateWebhookEndpoint.DispatchAsync`). An inbox retry is measured again. |

Spans: `crestapps.stripe.webhook.dispatch` (tags `event.type`, `crestapps.stripe.event.id`). The ASP.NET Core request span of the webhook also gets `crestapps.stripe.event.id` and `event.type`. Logs of a delivery run in a scope with `StripeEventId` and `StripeEventType`. HTTP client spans for the API calls come from `System.Net.Http`; no second span is added.

### Subscriptions (`CrestApps.OrchardCore.Subscriptions`)

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.subscriptions.transitions` | Counter | `{transition}` | `operation` = `renew`, `past_due`, `cancel`, `resume`, `pause`, `expire`, `sync`; `result` = `changed`, `no_change`, `busy`, `not_found`; `from`/`to` (status, snake case) only when `changed` | Every `ISubscriptionLifecycleService` transition. **Renewals** are `operation=renew,result=changed`; a cycle reported twice (webhook and sweep) is `no_change` and is not a renewal. Counted before the change commits (approximate). |
| `crestapps.subscriptions.installment.charge.duration` | Histogram | `s` | `outcome` | Installment charges and their duration through the checkout and its provider; its count is the number of charges. |
| `crestapps.subscriptions.provisioning.duration` | Histogram (long buckets) | `s` | `outcome` = `succeeded`, `failed`, `abandoned`, `busy`, `skipped`, `cancelled`, `error`; `attempt` (1-5) | Each attempt to create a site a customer paid for. |

Spans: `crestapps.subscriptions.transition` (tags `operation`, `result`, `crestapps.subscriptions.subscription.id`); `crestapps.subscriptions.installment.charge` (tag `crestapps.subscriptions.plan.id`); `crestapps.subscriptions.provisioning` (tag `crestapps.subscriptions.provisioning.job.id`); Orchard Core's own setup and recipe spans nest under it. The tenant name is not on any span or metric. Provisioning logs run in a scope with `ProvisioningJobId`; installment sweep logs in a scope with `InstallmentPlanId`.

### Orders (`CrestApps.OrchardCore.Orders`)

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.orders.created` | Counter | `{order}` | `channel` = `online`, `pos`, `draft`, `other`; `result` = `created`, `unavailable` | Orders made by `OrderService.CreateFromCartAsync` (`unavailable` = canceled at once because stock or similar could not be held). |
| `crestapps.orders.cancelled` | Counter | `{order}` | `reason` = `replaced`, `payment_failed`, `checkout_cancelled`, `checkout_expired`, `abandoned`, `unavailable`, `cancelled_by_store`, `declined`, `rejected` | Orders canceled by `OrderService.CancelAsync`. **Abandoned orders** are `reason=abandoned` (no separate counter). |

### Taxation (`CrestApps.OrchardCore.Taxation`)

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.taxation.determination.fallbacks` | Counter | `{determination}` | `provider`, `outcome` (`error`, `timeout`) | An external tax service could not answer and the store's rules worked out the tax. |

### Marketplace (`CrestApps.OrchardCore.Marketplace`)

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.marketplace.payouts` | Counter | `{payout}` | `provider`, `outcome` = `paid`, `failed`, `error` | Transfers that pay vendors (one per vendor and order). |

### Products (`CrestApps.OrchardCore.Products`)

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.products.search.rebuild.duration` | Histogram (long buckets) | `s` | `outcome` | `ProductSearchIndexer.RebuildAsync`, a full rebuild of the product search records. |
| `crestapps.products.search.rebuild.documents` | Histogram | `{document}` | (tenant only) | Products indexed by a finished rebuild. |
| `crestapps.products.search.last_rebuild` | Gauge (synchronous) | `s` | (tenant only) | Unix time the last full rebuild finished. |

Spans: `crestapps.products.search.rebuild`.

### Background tasks

Every `IBackgroundTask` in the commerce projects runs through `BackgroundTaskTelemetry.RunAsync` (`crestapps.background_task.outcomes/items/last_success/lock.utilization`, `task` = class name). Items by `result`:

| Task | Items (`result`) | Skip / budget |
|---|---|---|
| `CheckoutReconciliationBackgroundTask` (lock 60 s) | the engine's result per session (`completed`, `pending`, `already_completed`, `blocked`, `failed`, `cancelled`, `not_found`), `expired`, `error` | watch `lock.utilization` for this task: each session can wait on a provider's API inside a 60 s lock |
| `AbandonedOrdersBackgroundTask` | `cancelled` | |
| `CheckoutRecoveryBackgroundTask` | `sent` | |
| `ExpiredCartsBackgroundTask` | `deleted` | |
| `TenantProvisioningBackgroundTask` | `succeeded`, `failed`, `abandoned`, `busy`, `error` | `skipped` when nothing is due; `budget_exceeded` when more than 3 jobs are due |
| `SubscriptionLifecycleBackgroundTask` | `expired`, `renewed`, `skipped` (gateway-billed), `error` | |
| `InstallmentPlanBackgroundTask` | `processed`, `not_processed`, `error` | |
| `PayLaterRenewalBackgroundTask` | `renewed`, `skipped`, `error` | `skipped` when nothing is due |
| `TransactionReminderBackgroundTask` | `became_overdue`, `reminded`, `reminded_upcoming`, `error` | |
| `MarketplaceBackgroundTask` | `released` (payouts are on `crestapps.marketplace.payouts`) | |
| `BookingsBackgroundTask` | `released`, `charged`, `charge_failed`, `error` | |
| `MembershipsBackgroundTask` | `ended` | |
| `ClassifiedsBackgroundTask` | `expired` | |
| `CustomerRetentionBackgroundTask` | `guests_erased`, `orders_anonymized` | swallowed failure reported as `error` |
| `MarketingBackgroundTask` | `started`, `not_started` (campaigns) | `skipped` without a site base URL |
| `ReviewRequestBackgroundTask` | `sent` | |
| `ProductSearchIndexBackgroundTask` | `indexed` | `skipped` when the fingerprint is unchanged |
| `ProductSaleScheduleBackgroundTask` | `indexed` | |
| `OutreachBackgroundTask` | `batches` | `budget_exceeded` when it used all 100 batches of a run |

Deferred work (`DeferredWork.Add`, span `crestapps.deferred.<name>` linked to the request, `crestapps.deferred_task.duration`): `product_search_refresh`, `loyalty_award`, `referral_reward`, `order_webhooks`, `booking_payment_workflow`, `booking_workflow`, `stripe_request_failed`, `account_detach_children`, `account_list_sync`.

### Logging

Commerce owns EventIds 12000-12999, split by area: checkout 12000-12099, Stripe 12100-12199, subscriptions 12200-12299, orders 12300-12399, marketplace 12400-12499, shipping 12500-12599, taxation 12600-12699, products 12700-12799, bookings and pay later 12800-12899 (others unassigned).

| EventId | Class | Level | Message |
|---|---|---|---|
| 12000 | `CheckoutLog.ReconciliationSwept` (Checkout module) | Information | The checkout reconciliation sweep checked `{Sessions}` waiting checkouts: `{Completed}` completed, `{Failed}` failed to reconcile, `{Expired}` abandoned ones expired; the oldest had waited `{OldestPendingSeconds}` seconds. (only when there was something to do) |
| 12200 | `SubscriptionsLog.TenantProvisioned` (Subscriptions.Core) | Information | Provisioning job '`{JobId}`' created its site on attempt `{Attempt}` in `{ElapsedSeconds}` seconds. |

New log scopes: `CheckoutSessionId` (reconciliation sweep, per session), `StripeEventId`/`StripeEventType` (webhook), `ProvisioningJobId` (tenant provisioning), `InstallmentPlanId` (installment sweep).

### Behavior notes

- No existing commerce instrument was renamed (there were none).
- `DefaultCheckoutEngine` public methods are now thin wrappers over private `*CoreAsync` methods so every return is measured; behavior is unchanged.
- `TransactionReminderBackgroundTask.RaiseOverdueAsync` now returns the number of transactions that became overdue (was `Task`); `PayLaterRenewalBackgroundTask.RenewAsync` (private) returns whether it created a cycle.
- Not deferred-work wrapped on purpose: `SequentialFinancialDocumentNumberGenerator` lock release, and the `BookingMigrations`/`PosMigrations` index backfills (migrations).

## Scheduling, workforce, field service, geo and tenant hierarchy

Each area has its own meter and activity source, named `CrestApps.OrchardCore.<Area>`:

| Meter / source | Class | Project |
|---|---|---|
| `CrestApps.OrchardCore.Scheduling` (meter only) | `SchedulingTelemetry` | `src/Core/CrestApps.OrchardCore.Scheduling.Core/Telemetry` |
| `CrestApps.OrchardCore.Calendars` | `CalendarsTelemetry` | `src/Core/CrestApps.OrchardCore.Calendars.Core/Telemetry` |
| `CrestApps.OrchardCore.Workforce` | `WorkforceTelemetry` | `src/Core/CrestApps.OrchardCore.Workforce.Core/Telemetry` |
| `CrestApps.OrchardCore.Geo` (meter only) | `GeoTelemetry` | `src/Core/CrestApps.OrchardCore.Geo.Core/Telemetry` |
| `CrestApps.OrchardCore.Dispatch` (meter only) | `DispatchTelemetry` | `src/Core/CrestApps.OrchardCore.Dispatch.Core/Telemetry` |
| `CrestApps.OrchardCore.WorkOrders` (meter only) | `WorkOrdersTelemetry` | `src/Core/CrestApps.OrchardCore.WorkOrders.Core/Telemetry` |
| `CrestApps.OrchardCore.Solver` (meter only) | `SolverTelemetry`, `SolverRun` | `src/Modules/CrestApps.OrchardCore.OrTools/Telemetry` (no `.Core`) |
| `CrestApps.OrchardCore.TenantHierarchy` | `TenantHierarchyTelemetry` | `src/Core/CrestApps.OrchardCore.TenantHierarchy.Core/Telemetry` |
| `CrestApps.OrchardCore.NLWeb` (meter only) | `NLWebTelemetry` | `src/Modules/CrestApps.OrchardCore.NLWeb/Telemetry` (`NLWeb.Core` stays host-free) |

Ids (calendar, event, appointment, visit, work order, worker, timesheet, payroll batch, registry entry) go on spans and logs only. Names (calendar, worker, tenant), addresses, coordinates and subscription URLs never go on a metric. The tenant hierarchy instruments never add a tenant name, as a metric tag or a span attribute; their spans carry the child's registry entry id instead. (`OrchardCore.OpenTelemetry` still stamps the current tenant on every span, and Orchard Core's own work run inside a child shell is traced under that child.)

### Instruments

| Instrument | Type | Unit | Tags | What it measures |
|---|---|---|---|---|
| `crestapps.scheduling.observer.failures` | Counter | `{failure}` | `observer` (type name), `kind` (`AppointmentChangeKind`) | Appointment changes an `IAppointmentObserver` failed to handle. The failure is logged and swallowed, so the deferred run itself still ends `ok`. |
| `crestapps.calendars.sync.duration` | Histogram | `s` | `provider` (registered provider name, `unavailable` when the subscription names no registered provider), `sync.direction` (`one_way`, `two_way`), `mode` (`full`, `incremental`; absent when the run failed before reading), `outcome` (`ok`, `error`, `cancelled`, `unauthorized`), `error.type` | One external calendar synchronization (`CalendarSyncService.SyncAsync`). `unauthorized`: the external calendar refused access and the subscription is paused until reconnected. |
| `crestapps.calendars.sync.events` | Counter | `{event}` | `provider`, `result` (`created`, `updated`, `removed`, `conflict`, `refused`) | Events a successful synchronization changed, in both directions. |
| `crestapps.calendars.observer.failures` | Counter | `{failure}` | `observer`, `kind` (`CalendarEventChangeKind`) | Event changes an `ICalendarEventObserver` failed to handle (logged and swallowed). |
| `crestapps.workforce.notifications` | Counter | `{notification}` | `channel` (channel name: `Inbox`, `Push`, `Email`, or another module's channel such as SMS), `kind` (`WorkforceNotificationKind`), `result` (`sent`, `skipped`, `failed`) | Each notification offered to each channel by `DefaultWorkforceNotifier`. `skipped`: the worker turned the channel or the kind off, or it is their quiet hours. |
| `crestapps.workforce.adherence.alerts` | Counter | `{alert}` | `activity` (scheduled `ActivityKind`), `status` (`AgentPresenceStatus`) | Agent Out Of Adherence alerts raised (`AdherenceService.RaiseAlertsAsync`), once per episode. |
| `crestapps.workforce.payroll.export.duration` | Histogram | `s` | `provider` (registered export provider, `unknown`), `outcome` (`ok`, `rejected`, `error`, `cancelled`), `reason` when rejected (`no_provider`, `no_calendar`, `busy`, `none_locked`, `all_exported`), `error.type` | One payroll export (`DefaultPayrollExportService.RunAsync`), lock wait included. |
| `crestapps.workforce.payroll.export.rows` | Histogram | `{row}` | `provider` | Rows a successful payroll export wrote. |
| `crestapps.geo.provider.fallbacks` | Counter | `{call}` | `provider`, `operation` (`matrix`, `route`) | Failed travel-time and route calls answered with the built-in straight-line estimate. |
| `crestapps.geo.matrix.cache.lookups` | Counter | `{lookup}` | `provider`, `result` (`hit`, `miss`) | Travel-time matrix lookups in the distributed cache (only when the provider's terms allow caching). |
| `crestapps.geo.geocode.requests` | Counter | `{request}` | `result` (`done`, `no_match`, `gone`, `no_target`, `no_provider`, `retry`, `failed`) | Background geocoding requests processed by `DefaultGeocodeQueue`. `retry`: the provider failed and the request waits; `failed`: it failed `MaxAttempts` times and was given up. |
| `crestapps.dispatch.visit.events` | Counter | `{event}` | `event` (`created`, `assigned`, `unassigned`, `status_changed`, `at_risk`, `other`), `status` (`VisitStatus` after the event) | Visit transitions, recorded where they are raised (`DefaultDispatchService`, `VisitRiskMonitor`). |
| `crestapps.dispatch.observer.failures` | Counter | `{failure}` | `observer`, `event` | Visit events an `IVisitObserver` failed to handle (logged and swallowed). |
| `crestapps.workorders.sla.events` | Counter | `{event}` | `event` (`warning`, `breached`), `target` (`response`, `resolution`) | Service level warnings and breaches raised by `SlaMonitor`, each once per target. |
| `crestapps.solver.runs` | Counter | `{run}` | `solver`, `status` (`optimal`, `feasible`, `timed_out`, `infeasible`, `cancelled`, `skipped`, `error`), `size` (`le_10`, `le_100`, `le_1000`, `le_10000`, `le_100000`, `gt_100000`), `result` (`solver`: its own answer was returned; `builtin`: the built-in answer was kept), `outcome` | OR-Tools solves (the solve's duration is not measured: it runs to its time limit). Size is workers × days × templates for shifts and the number of visits for dispatch. The routing solver (guided local search) never proves optimality and runs to its limit, so an answer it found is `feasible` unless OR-Tools reports `RoutingOptimal`. `skipped`: nothing to solve. |
| `crestapps.tenanthierarchy.child.operation.duration` | Histogram | `s` (long buckets) | `operation` (`create`, `setup`, `update`, `suspend`, `resume`, `reload`, `remove`, `set_features`), `outcome` (`ok`, `failed`, `error`, `cancelled`), `error.type` | One operation a parent tenant ran on a child tenant through `TenantHierarchyBroker`. `failed`: the broker returned a failure (wrong state, setup errors, removal refused). Its count is the operation count. |
| `crestapps.nlweb.ask.duration` | Histogram | `s` | `mode` (`list`, `summarize`, `generate`, `other`), `outcome` (`answered`, `empty`, `elicitation`, `failed`), `reason` when failed (`invalid_request`, `forbidden`, `not_configured`, `unknown_promise`, `internal_error`, `other`) | One NLWeb question, until its results and text were ready (recorded by an `INLWebAskHandler`). |
| `crestapps.nlweb.ask.results` | Histogram | `{result}` | `mode` | Results an answered NLWeb question returned. |

### Spans

- `crestapps.calendars.sync` (`crestapps.calendars.calendar.id`)
- `crestapps.workforce.payroll.export` (`crestapps.workforce.pay_period_calendar.id`, `crestapps.workforce.payroll_batch.id`)
- `crestapps.tenanthierarchy.child.create`, `.setup`, `.update`, `.suspend`, `.resume`, `.reload`, `.remove`, `.set_features` (`crestapps.tenanthierarchy.entry.id`)
- `crestapps.deferred.appointment_changes`, `crestapps.deferred.calendar_event_changes`, `crestapps.deferred.dispatch_board_notification` (platform source, linked to the request)

### Log EventIds

The Scheduling range 13000-13999 is split per area so ids never collide: Scheduling 13000-13099, Calendars 13100-13199, Workforce 13200-13299, Geo 13300-13399, Dispatch 13400-13499, WorkOrders 13500-13599, Solver 13600-13699, YesSql 13700-13799, Skills 13800-13899. None of these areas adds a log message in this change.

### Behavior notes

- `NLWebAskContext` gained an `Elapsed` property (time since the pipeline started the question) so an `INLWebAskHandler` can measure it.

## Logs

New log messages use source-generated `[LoggerMessage]` methods with an event id from the area's range, so a log line can be counted and alerted on by its id rather than its text. CrestApps.Core uses the ids below 10000, so the modules start at 10000 (`CrestAppsEventIds`).

| Range | Area |
| --- | --- |
| 1000s | Phone number verification providers (existing) |
| 10000s | Shared platform helpers |
| 11000s | AI modules |
| 12000s | Commerce |
| 13000s | Scheduling, calendars, workforce, skills, work orders, dispatch and geo |
| 14000s | Reports |
| 15000s | Data connectors |
| 16000s | Content transfer, imports, exports, recipes and the do-not-call registry |
| 17000s | Contact center (17900s: webhooks) |
| 18000s | Messaging and SMS (18500s: AI voice) |
| 19000s | Telephony and its providers |
| 20000s | Startup and tenant management |

Ids, user input and the phone numbers a log line already carried go through `SanitizeLogValue()`, as before this change; no new log line adds a phone number, an email address or a SIP URI.

## Related

- [Configuration](configuration.md#observability): the `OrchardCore:OpenTelemetry` settings and the AI `EnableOpenTelemetry` default.
- [Report Builder: large data](modules/report-builder/large-data.md#telemetry): the report run telemetry in context.
- [Contact Center production support](contact-center/production-support.md): the contact center health checks and telemetry in context.
