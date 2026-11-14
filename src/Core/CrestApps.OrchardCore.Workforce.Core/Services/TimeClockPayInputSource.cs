using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Hands the hours of locked timesheets to payroll: one input per pay code, day, activity and job of a timesheet, with
/// the rate from the staff member's pay. A timesheet a pay run or an export took is marked with the taker's id, as an
/// export did before, and the predictability pay it pays is marked exported.
/// </summary>
internal sealed class TimeClockPayInputSource : IPayInputSource
{
    /// <summary>
    /// The kind of record the time clock's inputs come from.
    /// </summary>
    public const string Kind = "Timesheet";

    private const int PageSize = 200;

    private readonly ISession _session;
    private readonly TimesheetStore _timesheets;
    private readonly StaffMemberStore _workers;
    private readonly IPayCodeManager _codes;
    private readonly IServiceProvider _services;

    public TimeClockPayInputSource(ISession session, TimesheetStore timesheets, StaffMemberStore workers, IPayCodeManager codes, IServiceProvider services)
    {
        _session = session;
        _timesheets = timesheets;
        _workers = workers;
        _codes = codes;
        _services = services;
    }

    public string SourceKind => Kind;

    public async Task<IReadOnlyList<PayInput>> GetInputsAsync(PayInputQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var timesheets = new List<Timesheet>();

        await foreach (var timesheet in ReadLockedAsync(query.PayScheduleId, cancellationToken))
        {
            if ((query.PeriodStart.HasValue && timesheet.PeriodStart < query.PeriodStart.Value) ||
                (query.PeriodEnd.HasValue && timesheet.PeriodStart > query.PeriodEnd.Value) ||
                (query.StaffMemberIds is { Count: > 0 } && !query.StaffMemberIds.Contains(timesheet.WorkerId)) ||
                (!query.IncludeConsumed && timesheet.ExportBatchIds.Count > 0))
            {
                continue;
            }

            timesheets.Add(timesheet);
        }

        if (timesheets.Count == 0)
        {
            return [];
        }

        var workers = (await _workers.GetAsync(timesheets.Select(timesheet => timesheet.WorkerId).Distinct(StringComparer.Ordinal).ToArray(), cancellationToken))
            .ToDictionary(worker => worker.ItemId, StringComparer.Ordinal);
        var codes = (await _codes.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, code => code.Code, StringComparer.Ordinal);
        var inputs = new List<PayInput>();

        foreach (var timesheet in timesheets.OrderBy(timesheet => workers.GetValueOrDefault(timesheet.WorkerId)?.DisplayName, StringComparer.CurrentCultureIgnoreCase).ThenBy(timesheet => timesheet.PeriodStart))
        {
            var worker = workers.GetValueOrDefault(timesheet.WorkerId);
            var rates = worker is not null && worker.TryGet<StaffPayRates>(out var stored) ? stored : new StaffPayRates();

            foreach (var group in timesheet.Lines
                .Where(line => !string.IsNullOrEmpty(line.PayCodeId) && line.Hours != 0)
                .GroupBy(line => (line.PayCodeId, line.Date, line.ActivityCodeId, line.Job))
                .OrderBy(group => group.Key.Date)
                .ThenBy(group => codes.GetValueOrDefault(group.Key.PayCodeId), StringComparer.Ordinal))
            {
                inputs.Add(new PayInput
                {
                    SourceKind = Kind,
                    SourceId = timesheet.ItemId,
                    StaffMemberId = timesheet.WorkerId,
                    StaffMemberName = worker?.DisplayName,
                    EmployeeNumber = worker?.EmployeeNumber,
                    PayrollId = worker?.ExternalId ?? worker?.EmployeeNumber,
                    PayScheduleId = timesheet.PayPeriodCalendarId,
                    PeriodStart = timesheet.PeriodStart,
                    PeriodEnd = timesheet.PeriodEnd,
                    PayCodeId = group.Key.PayCodeId,
                    Date = group.Key.Date,
                    Hours = group.Sum(line => line.Hours),
                    Rate = rates.RateOn(group.Key.Date, group.Key.ActivityCodeId),
                    Allocation = new PayInputAllocation
                    {
                        DepartmentTeamId = timesheet.TeamId,
                        ActivityCode = group.Key.ActivityCodeId,
                        Job = group.Key.Job,
                    },
                    Consumed = timesheet.ExportBatchIds.Count > 0,
                });
            }
        }

        return inputs;
    }

    public async Task MarkConsumedAsync(string consumerId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(consumerId);
        ArgumentNullException.ThrowIfNull(sourceIds);

        var marked = new List<Timesheet>();

        foreach (var timesheet in await _timesheets.GetAsync(sourceIds.ToArray(), cancellationToken))
        {
            if (!timesheet.ExportBatchIds.Contains(consumerId))
            {
                timesheet.ExportBatchIds.Add(consumerId);
                await _timesheets.UpdateAsync(timesheet, cancellationToken);
            }

            marked.Add(timesheet);
        }

        await SetPredictabilityPayAsync(marked, PredictabilityPayStatus.Approved, PredictabilityPayStatus.Exported, cancellationToken);
    }

    public async Task ReleaseAsync(string consumerId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(consumerId);
        ArgumentNullException.ThrowIfNull(sourceIds);

        var released = new List<Timesheet>();

        foreach (var timesheet in await _timesheets.GetAsync(sourceIds.ToArray(), cancellationToken))
        {
            if (timesheet.ExportBatchIds.Remove(consumerId))
            {
                await _timesheets.UpdateAsync(timesheet, cancellationToken);
            }

            // Its predictability pay is open again only when nothing else took the timesheet.
            if (timesheet.ExportBatchIds.Count == 0)
            {
                released.Add(timesheet);
            }
        }

        await SetPredictabilityPayAsync(released, PredictabilityPayStatus.Exported, PredictabilityPayStatus.Approved, cancellationToken);
    }

    // Locked timesheets are read a page at a time, so a site with years of them is never loaded at once.
    private async IAsyncEnumerable<Timesheet> ReadLockedAsync(string payScheduleId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var skip = 0; ; skip += PageSize)
        {
            var search = _session.Query<Timesheet, TimesheetIndex>(index => index.Status == TimesheetStatus.Locked, collection: WorkforceConstants.CollectionName);

            if (!string.IsNullOrEmpty(payScheduleId))
            {
                search = search.Where(index => index.PayPeriodCalendarId == payScheduleId);
            }

            var page = (await search.OrderBy(index => index.PeriodStart).ThenBy(index => index.ItemId).Skip(skip).Take(PageSize).ListAsync(cancellationToken)).ToList();

            foreach (var timesheet in page)
            {
                yield return timesheet;
            }

            if (page.Count < PageSize)
            {
                yield break;
            }
        }
    }

    private async Task SetPredictabilityPayAsync(IEnumerable<Timesheet> timesheets, PredictabilityPayStatus from, PredictabilityPayStatus to, CancellationToken cancellationToken)
    {
        // Predictability pay is a Labor Rules record; without that feature there is none.
        var store = _services.GetService<PredictabilityPayStore>();

        if (store is null)
        {
            return;
        }

        var ids = timesheets
            .SelectMany(timesheet => timesheet.Lines)
            .Where(line => line.Source == TimesheetLineSource.PredictabilityPay)
            .SelectMany(line => line.SourceIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ids.Length == 0)
        {
            return;
        }

        foreach (var entry in await store.GetAsync(ids, cancellationToken))
        {
            if (entry.Status == from)
            {
                entry.Status = to;
                await store.UpdateAsync(entry, cancellationToken);
            }
        }
    }
}
