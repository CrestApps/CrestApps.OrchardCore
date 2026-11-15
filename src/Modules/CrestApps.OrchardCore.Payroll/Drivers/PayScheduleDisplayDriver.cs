using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The rows of the pay schedule list and the pay schedule editor, with the next periods and pay dates it gives.
/// </summary>
internal sealed class PayScheduleDisplayDriver : DisplayDriver<PaySchedule>
{
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public PayScheduleDisplayDriver(ILocalClock localClock, IStringLocalizer<PayScheduleDisplayDriver> stringLocalizer)
    {
        _localClock = localClock;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(PaySchedule schedule, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = schedule.Name?.ToLowerInvariant();
        }

        return CombineAsync(
            View("PaySchedule_Fields_SummaryAdmin", schedule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("PaySchedule_Tags_SummaryAdmin", schedule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("PaySchedule_Buttons_SummaryAdmin", schedule).Location("Actions:5"),
            View("PaySchedule_ActionsMenu_SummaryAdmin", schedule).Location("ActionsMenu:10"));
    }

    public override IDisplayResult Edit(PaySchedule schedule, BuildEditorContext context)
    {
        return Initialize<PayScheduleViewModel>("PayScheduleFields_Edit", async model =>
        {
            model.Name = schedule.Name;
            model.Frequency = schedule.Frequency.ToString();
            model.AnchorDate = schedule.AnchorDate;
            model.WeekStart = schedule.WeekStart.ToString();
            model.SecondPeriodStartDay = schedule.SecondPeriodStartDay;
            model.PayDateRule = schedule.PayDateRule.ToString();
            model.PayDateOffsetDays = schedule.PayDateOffsetDays;
            model.FirstPayDay = schedule.FirstPayDay;
            model.SecondPayDay = schedule.SecondPayDay;
            model.NonBusinessDayShift = schedule.NonBusinessDayShift.ToString();
            model.ApprovalLeadDays = schedule.ApprovalLeadDays;
            model.ExtraPayPeriod = schedule.ExtraPayPeriod.ToString();
            model.Enabled = schedule.Enabled;

            // The next periods from today in the site's time zone, so the schedule can be checked before saving.
            if (!context.IsNew)
            {
                var day = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

                for (var i = 0; i < 4; i++)
                {
                    var (start, end) = schedule.PeriodOn(day);

                    model.NextPeriods.Add(S["{0} to {1}, paid {2}", start.ToString("d"), end.ToString("d"), schedule.PayDateFor(end).ToString("d")].Value);
                    day = end.AddDays(1);
                }
            }
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(PaySchedule schedule, UpdateEditorContext context)
    {
        var model = new PayScheduleViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        schedule.Name = model.Name;
        schedule.Frequency = Enum.TryParse<PayFrequency>(model.Frequency, out var frequency) ? frequency : PayFrequency.Biweekly;
        schedule.WeekStart = Enum.TryParse<DayOfWeek>(model.WeekStart, out var weekStart) ? weekStart : DayOfWeek.Sunday;
        schedule.SecondPeriodStartDay = model.SecondPeriodStartDay;
        schedule.PayDateRule = Enum.TryParse<PayDateRule>(model.PayDateRule, out var rule) ? rule : PayDateRule.DaysAfterPeriodEnd;
        schedule.PayDateOffsetDays = model.PayDateOffsetDays;
        schedule.FirstPayDay = model.FirstPayDay;
        schedule.SecondPayDay = model.SecondPayDay;
        schedule.NonBusinessDayShift = Enum.TryParse<PayDateShift>(model.NonBusinessDayShift, out var shift) ? shift : PayDateShift.Before;
        schedule.ApprovalLeadDays = model.ApprovalLeadDays;
        schedule.ExtraPayPeriod = Enum.TryParse<ExtraPayPeriodRule>(model.ExtraPayPeriod, out var extra) ? extra : ExtraPayPeriodRule.Standard;
        schedule.Enabled = model.Enabled;

        // A weekly kind of schedule without a day a period starts on is the handler's to refuse, as for a recipe.
        if (model.AnchorDate is { } anchor)
        {
            schedule.AnchorDate = anchor;
        }
        else if (schedule.Frequency is PayFrequency.Weekly or PayFrequency.Biweekly or PayFrequency.FourWeekly)
        {
            schedule.AnchorDate = default;
        }

        return Edit(schedule, context);
    }
}
