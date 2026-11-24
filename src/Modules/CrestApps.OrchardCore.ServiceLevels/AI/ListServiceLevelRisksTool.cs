using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ServiceLevels.AI;

/// <summary>
/// Lists the work whose service-level targets are past due, past their warning point or due soon, the most overdue
/// first, across every kind of record with service levels (tickets, work orders...), within what the user may see:
/// those who manage service level policies see every timer, anyone else the timers of the records each kind's access
/// provider lets them see, as on the Service-level timers report.
/// </summary>
internal sealed class ListServiceLevelRisksTool : ServiceLevelAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listServiceLevelRisks";

    // How many index rows are read at a time, and at most, for a user who sees only some records.
    private const int BatchSize = 200;
    private const int MaxScanned = 1000;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "status": {
          "type": "string",
          "enum": ["any", "breached", "atRisk"],
          "description": "breached: past their target. atRisk: past their warning point but not yet due. any (the default): both, plus those due within dueWithinMinutes."
        },
        "dueWithinMinutes": {
          "type": "integer",
          "minimum": 1,
          "maximum": 10080,
          "description": "With status any, also lists the targets due within this many minutes that have not reached their warning point yet."
        },
        "recordKind": {
          "type": "string",
          "description": "Only this kind of record, such as Ticket or WorkOrder."
        },
        "metric": {
          "type": "string",
          "description": "Only this metric, such as FirstResponse, NextResponse or Resolution."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page, from 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many to return, 20 by default and at most 50."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => TheName;

    /// <inheritdoc />
    public override string Description => "Lists the work (tickets, work orders...) whose service-level targets are past due or past their warning point, or due within the minutes given, most overdue first: the record, the metric (first response, resolution...), the policy and priority, and when it is or was due. Paused targets are left out. Only records the user may see.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var providers = ServiceLevelTimerVisibility.ByKind(services.GetServices<IServiceLevelTimerAccessProvider>());
        var seesAll = await ServiceLevelTimerVisibility.SeesEveryTimerAsync(services.GetRequiredService<IAuthorizationService>(), user);

        // As the Service-level timers report: without the permission, only the kinds whose module says who sees them.
        if (!seesAll && providers.Count == 0)
        {
            return "The user may not see any service-level timers.";
        }

        if (!TryReadStatus(arguments, out var status))
        {
            return "Give status as any, breached or atRisk.";
        }

        var kind = ReadText(arguments, "recordKind");

        // Kinds are matched as stored; the provider's spelling wins, so "ticket" finds Ticket records.
        if (kind is not null && providers.Keys.FirstOrDefault(key => string.Equals(key, kind, StringComparison.OrdinalIgnoreCase)) is { } providerKind)
        {
            kind = providerKind;
        }

        if (kind is not null && !seesAll && !providers.ContainsKey(kind))
        {
            var visibleKinds = string.Join(", ", providers.Keys.Order(StringComparer.Ordinal));

            return $"The user may not see the service-level timers of '{kind}' records. Kinds they may see: {visibleKinds}.";
        }

        var page = Math.Max(1, arguments.GetFirstValueOrDefault("page", 1));
        var pageSize = Math.Clamp(arguments.GetFirstValueOrDefault("pageSize", DefaultPageSize), 1, MaxPageSize);
        var within = arguments.GetFirstValueOrDefault<int?>("dueWithinMinutes", null) is int minutes and > 0
            ? TimeSpan.FromMinutes(Math.Min(minutes, 10080))
            : (TimeSpan?)null;
        var now = services.GetRequiredService<IClock>().UtcNow;
        var filter = new ServiceLevelDueFilter
        {
            NowUtc = now,
            Status = status,
            DueWithin = within,
            SubjectKind = kind,
            Metric = ReadText(arguments, "metric"),
        };
        var due = services.GetRequiredService<ServiceLevelDueTimers>();
        var skip = (page - 1) * pageSize;
        List<ServiceLevelTimer> timers;
        int? total = null;
        var more = false;
        var truncated = false;

        if (seesAll)
        {
            total = await due.CountAsync(filter, cancellationToken);
            timers = [.. await due.ListAsync(filter, skip, pageSize, cancellationToken)];
            more = skip + timers.Count < total;
        }
        else
        {
            // A page of what the user may see: read the index in batches and keep the visible timers until the page and
            // one more are found, or the scan reaches its cap. No total is given, as the rows not read are not counted.
            var known = new Dictionary<(string, string), bool>();
            var visible = new List<ServiceLevelTimer>();
            var scanned = 0;
            var exhausted = false;

            while (visible.Count <= skip + pageSize && scanned < MaxScanned)
            {
                var batch = await due.ListAsync(filter, scanned, BatchSize, cancellationToken);
                scanned += batch.Count;
                visible.AddRange(await ServiceLevelTimerVisibility.KeepVisibleAsync(user, providers, known, [.. batch], cancellationToken));

                if (batch.Count < BatchSize)
                {
                    exhausted = true;

                    break;
                }
            }

            timers = [.. visible.Skip(skip).Take(pageSize)];
            more = visible.Count > skip + pageSize;
            truncated = !exhausted && !more;

            if (exhausted)
            {
                total = visible.Count;
            }
        }

        var describer = new ServiceLevelTimerDescriber(services);
        var summaries = await describer.GetSummariesAsync(user, providers, timers.Select(timer => timer.Subject), cancellationToken);
        var items = new List<object>(timers.Count);

        foreach (var timer in timers)
        {
            var (policyName, metricName, priorityName) = await describer.GetNamesAsync(timer, cancellationToken);
            var dueUtc = DateTime.SpecifyKind(timer.DueUtc, DateTimeKind.Utc);
            var warnUtc = DateTime.SpecifyKind(timer.WarnAtUtc, DateTimeKind.Utc);

            items.Add(new
            {
                record = describer.DescribeRecord(timer.Subject, summaries),
                metric = timer.Metric,
                metricName,
                policyId = timer.PolicyId,
                policyName,
                priority = string.IsNullOrEmpty(timer.Priority) ? null : timer.Priority,
                priorityName,
                status = dueUtc <= now ? "breached" : warnUtc <= now ? "atRisk" : "dueSoon",
                due = await describer.ToLocalAsync(timer.DueUtc),
                warnAt = await describer.ToLocalAsync(timer.WarnAtUtc),
                breachedAt = await describer.ToLocalAsync(timer.BreachedUtc),
                minutesUntilDue = (int)Math.Round((dueUtc - now).TotalMinutes),
                targetMinutes = ServiceLevelTimerDescriber.Minutes(timer.TargetSeconds),
                noBusinessHours = timer.NoBusinessHours ? true : (bool?)null,
            });
        }

        return Json(new
        {
            items,
            page,
            pageSize,
            total,
            more,
            truncated,
            note = UntrustedNote,
        });
    }

    private static bool TryReadStatus(AIFunctionArguments arguments, out ServiceLevelDueStatus status)
    {
        var text = ReadText(arguments, "status");

        if (string.Equals(text, "breached", StringComparison.OrdinalIgnoreCase))
        {
            status = ServiceLevelDueStatus.Breached;

            return true;
        }

        if (string.Equals(text, "atRisk", StringComparison.OrdinalIgnoreCase))
        {
            status = ServiceLevelDueStatus.AtRisk;

            return true;
        }

        status = ServiceLevelDueStatus.Any;

        return text is null || string.Equals(text, "any", StringComparison.OrdinalIgnoreCase);
    }
}
