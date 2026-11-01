using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services.LaborRules;

/// <summary>
/// A change the employer makes to a published shift needs at least a number of days' notice before the shift.
/// </summary>
internal sealed class ScheduleNoticeRule : LaborRuleBase
{
    public ScheduleNoticeRule(IStringLocalizer<ScheduleNoticeRule> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Key => LaborRuleKeys.ScheduleNotice;

    public override string DisplayName => S["Schedule notice"];

    public override string Description => S["A change the employer makes to a published shift needs at least a number of days' notice before the shift."];

    public override IReadOnlyList<LaborRuleParameter> Parameters =>
        [
            Parameter("Days", S["Days of notice"], LaborRuleParameterKind.Days, "14"),
        ];

    public override Task EvaluateAsync(LaborRuleEvaluationContext context, LaborRuleInstance instance, CancellationToken cancellationToken = default)
    {
        var days = instance.Number("Days", 14);

        foreach (var change in context.Changes.Where(change => change.InitiatedBy == ScheduleChangeInitiator.Employer))
        {
            var starts = new[] { change.BeforeStartUtc, change.AfterStartUtc }.Where(value => value.HasValue).Select(value => value.Value).DefaultIfEmpty(DateTime.MaxValue).Min();

            if (starts == DateTime.MaxValue)
            {
                continue;
            }

            var notice = (decimal)(starts - context.NowUtc).TotalDays;

            if (notice < days)
            {
                var date = DateOnly.FromDateTime(Calendars.Core.CalendarTime.ToLocal(starts, context.TimeZoneId));

                context.Report(this, instance, S["The change to the shift of {0} on {1} comes with {2} days' notice; {3} are needed.", context.Worker.DisplayName, Day(context, date), Format(Math.Max(0, Math.Round(notice, 1))), Format(days)], date, [change.ShiftKey]);
            }
        }

        return Task.CompletedTask;
    }
}
