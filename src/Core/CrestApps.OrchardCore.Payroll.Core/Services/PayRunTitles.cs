using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Makes the name of a pay run people recognize, instead of its number: what it pays, for whom and when, such as
/// "Pay for Nov 16 – Nov 29, 2026 (Weekly)", "Pay for Nov 16 – Nov 29, 2026: Jane Doe" or "Final pay for Jane Doe, paid
/// Dec 5, 2026".
/// </summary>
internal static class PayRunTitles
{
    /// <summary>
    /// The most characters a title keeps.
    /// </summary>
    public const int MaxLength = 200;

    /// <summary>
    /// Makes a run's title.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="scheduleName">The name of the run's pay schedule, or <see langword="null"/>.</param>
    /// <param name="staffNames">The names of the people the run names, in order; empty when it pays the schedule.</param>
    /// <param name="S">The localizer.</param>
    /// <param name="culture">The culture of the dates.</param>
    /// <returns>The title.</returns>
    public static string Build(PayRun run, string scheduleName, IReadOnlyList<string> staffNames, IStringLocalizer S, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(S);

        culture ??= CultureInfo.CurrentCulture;
        staffNames ??= [];

        var who = staffNames.Count switch
        {
            0 => null,
            1 => staffNames[0],
            _ => S["{0} people", staffNames.Count].Value,
        };

        string title;

        if (run.Type == PayRunType.Regular)
        {
            var period = Period(run.PeriodStart, run.PeriodEnd, S, culture);

            title = who is not null
                ? S["Pay for {0}: {1}", period, who]
                : string.IsNullOrWhiteSpace(scheduleName) ? S["Pay for {0}", period] : S["Pay for {0} ({1})", period, scheduleName.Trim()];
        }
        else
        {
            var kind = run.Type switch
            {
                PayRunType.Supplemental => S["Bonus pay"],
                PayRunType.OffCycle => S["Off-cycle pay"],
                PayRunType.Final => S["Final pay"],
                PayRunType.Correction => S["Correction"],
                PayRunType.Contractor => S["Contractor pay"],
                PayRunType.Historical => S["Pay history"],
                PayRunType.Reversal => S["Reversal"],
                _ => S["Pay"],
            };

            var paid = Day(run.PayDate, culture);

            title = who is not null
                ? S["{0} for {1}, paid {2}", kind, who, paid]
                : string.IsNullOrWhiteSpace(scheduleName) ? S["{0}, paid {1}", kind, paid] : S["{0} ({1}), paid {2}", kind, scheduleName.Trim(), paid];
        }

        return Trim(title);
    }

    /// <summary>
    /// Gets the name of a run: its title, or "Pay run" and its number for a run added before runs had titles.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="S">The localizer.</param>
    /// <returns>The name.</returns>
    public static string Name(PayRun run, IStringLocalizer S)
        => run is null ? null : string.IsNullOrWhiteSpace(run.Title) ? S["Pay run {0}", run.Number].Value : run.Title;

    /// <summary>
    /// Cleans a title someone typed: trimmed and cut to <see cref="MaxLength"/>, or <see langword="null"/> when empty.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <returns>The title kept.</returns>
    public static string Trim(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        title = title.Trim();

        return title.Length > MaxLength ? title[..MaxLength] : title;
    }

    // "Nov 16 – Nov 29, 2026", or "Dec 21, 2026 – Jan 3, 2027" across a year.
    private static string Period(DateOnly start, DateOnly end, IStringLocalizer S, CultureInfo culture)
    {
        if (start == default || end == default)
        {
            return Day(start == default ? end : start, culture);
        }

        return start.Year == end.Year
            ? S["{0} – {1}", start.ToString("MMM d", culture), Day(end, culture)]
            : S["{0} – {1}", Day(start, culture), Day(end, culture)];
    }

    private static string Day(DateOnly day, CultureInfo culture)
        => day.ToString("MMM d, yyyy", culture);
}

/// <summary>
/// Names a pay run added without a title (<see cref="PayRunTitles"/>), from its schedule, type, people and dates.
/// </summary>
internal sealed class PayRunTitleHandler : CatalogEntryHandlerBase<PayRun>
{
    private readonly IPayScheduleManager _schedules;
    private readonly IStaffDirectory _staff;

    internal readonly IStringLocalizer S;

    public PayRunTitleHandler(IPayScheduleManager schedules, IStaffDirectory staff, IStringLocalizer<PayRunTitleHandler> stringLocalizer)
    {
        _schedules = schedules;
        _staff = staff;
        S = stringLocalizer;
    }

    public override async Task CreatingAsync(CreatingContext<PayRun> context, CancellationToken cancellationToken = default)
    {
        var run = context.Model;

        run.Title = PayRunTitles.Trim(run.Title);

        if (run.Title is not null)
        {
            return;
        }

        var schedule = string.IsNullOrEmpty(run.PayScheduleId) ? null : await _schedules.FindByIdAsync(run.PayScheduleId, cancellationToken);
        var names = run.StaffMemberIds.Count == 0 ? new Dictionary<string, string>() : await _staff.GetNamesAsync(run.StaffMemberIds, cancellationToken);

        run.Title = PayRunTitles.Build(run, schedule?.Name, [.. run.StaffMemberIds.Select(id => names.GetValueOrDefault(id, id))], S, CultureInfo.CurrentCulture);
    }

    public override Task UpdatingAsync(UpdatingContext<PayRun> context, CancellationToken cancellationToken = default)
    {
        context.Model.Title = PayRunTitles.Trim(context.Model.Title);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Checks the people a pay run names: each needs a pay profile with the run's employer.
/// </summary>
internal sealed class PayRunPeopleHandler : CatalogEntryHandlerBase<PayRun>
{
    private readonly PayProfileStore _profiles;
    private readonly IStaffDirectory _staff;

    internal readonly IStringLocalizer S;

    public PayRunPeopleHandler(PayProfileStore profiles, IStaffDirectory staff, IStringLocalizer<PayRunPeopleHandler> stringLocalizer)
    {
        _profiles = profiles;
        _staff = staff;
        S = stringLocalizer;
    }

    public override async Task ValidatingAsync(ValidatingContext<PayRun> context, CancellationToken cancellationToken = default)
    {
        var run = context.Model;

        run.StaffMemberIds = [.. (run.StaffMemberIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal)];

        if (run.StaffMemberIds.Count == 0 || string.IsNullOrEmpty(run.EmployerId))
        {
            return;
        }

        var missing = new List<string>();

        foreach (var id in run.StaffMemberIds)
        {
            if (await _profiles.FindAsync(id, run.EmployerId, cancellationToken) is null)
            {
                missing.Add(id);
            }
        }

        if (missing.Count > 0)
        {
            var names = await _staff.GetNamesAsync(missing, cancellationToken);

            context.Result.Fail(new ValidationResult(
                S["Not paid through payroll yet, so the run can't pay them: {0}. Add their pay profile first.", string.Join(", ", missing.Select(id => names.GetValueOrDefault(id, id)))],
                [nameof(PayRun.StaffMemberIds)]));
        }
    }
}
