using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Tells Pay Setup what the time clock uses: a pay schedule its policies and timesheets name, a pay code its time off
/// types and timesheet lines name. Such a record is turned off, never deleted.
/// </summary>
internal sealed class TimeClockPaySetupUsage : IPaySetupUsage
{
    private const int PageSize = 200;

    private readonly TimeClockPolicyStore _policies;
    private readonly ISession _session;
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    public TimeClockPaySetupUsage(
        TimeClockPolicyStore policies,
        ISession session,
        IServiceProvider services,
        IStringLocalizer<TimeClockPaySetupUsage> stringLocalizer)
    {
        _policies = policies;
        _session = session;
        _services = services;
        S = stringLocalizer;
    }

    public async Task<IReadOnlyList<string>> GetUsagesAsync(PaySchedule schedule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var uses = new List<string>();

        foreach (var policy in (await _policies.ListAsync(cancellationToken)).Where(policy => policy.PayPeriodCalendarId == schedule.ItemId))
        {
            uses.Add(S["the time clock policy {0}", policy.Name].Value);
        }

        var timesheets = await _session.QueryIndex<TimesheetIndex>(index => index.PayPeriodCalendarId == schedule.ItemId, collection: WorkforceConstants.CollectionName)
            .CountAsync(cancellationToken);

        if (timesheets > 0)
        {
            uses.Add(S.Plural(timesheets, "one timesheet", "{0} timesheets").Value);
        }

        return uses;
    }

    public async Task<IReadOnlyList<string>> GetUsagesAsync(PayCode code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        var uses = new List<string>();

        if (_services.GetService<ITimeOffTypeManager>() is { } timeOffTypes)
        {
            foreach (var type in (await timeOffTypes.GetAllAsync(cancellationToken)).Where(type => string.Equals(type.PayCode, code.Code, StringComparison.OrdinalIgnoreCase)))
            {
                uses.Add(S["the time off type {0}", type.Name].Value);
            }
        }

        // Lines are inside the timesheets, so they are read a page at a time until one names the code.
        for (var skip = 0; ; skip += PageSize)
        {
            var page = await _session.Query<Timesheet, TimesheetIndex>(collection: WorkforceConstants.CollectionName)
                .OrderBy(index => index.ItemId)
                .Skip(skip)
                .Take(PageSize)
                .ListAsync(cancellationToken);
            var count = 0;

            foreach (var timesheet in page)
            {
                count++;

                if (timesheet.Lines.Any(line => line.PayCodeId == code.ItemId))
                {
                    uses.Add(S["timesheets"].Value);

                    return uses;
                }
            }

            if (count < PageSize)
            {
                return uses;
            }
        }
    }
}
