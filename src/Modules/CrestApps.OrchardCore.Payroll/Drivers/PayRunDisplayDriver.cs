using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The rows of the pay run list and the editor of a new run: its name, the schedule, who is paid (everyone on the
/// schedule or the people chosen), the type, the period and the pay date (the schedule's next period by default).
/// </summary>
internal sealed class PayRunDisplayDriver : DisplayDriver<PayRun>
{
    internal const string PaidSchedule = "Schedule";
    internal const string PaidPeople = "People";

    private readonly IPayScheduleManager _schedules;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public PayRunDisplayDriver(
        IPayScheduleManager schedules,
        ILocalClock localClock,
        IStringLocalizer<PayRunDisplayDriver> stringLocalizer)
    {
        _schedules = schedules;
        _localClock = localClock;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(PayRun run, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{run.Title} {run.Number} {run.Type} {run.Status}".ToLowerInvariant();
        }

        return CombineAsync(
            View("PayRun_Fields_SummaryAdmin", run)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("PayRun_Tags_SummaryAdmin", run)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("PayRun_Buttons_SummaryAdmin", run).Location("Actions:5"));
    }

    public override IDisplayResult Edit(PayRun run, BuildEditorContext context)
        => Initialize<PayRunViewModel>("PayRunFields_Edit", async model =>
        {
            model.Title = run.Title;
            model.PayScheduleId = run.PayScheduleId;
            model.Paid = run.StaffMemberIds.Count > 0 ? PaidPeople : PaidSchedule;
            model.StaffMemberIds = [.. run.StaffMemberIds];
            model.Type = run.Type.ToString();
            model.SupplementalMethod = run.SupplementalMethod.ToString();
            model.PeriodStart = run.PeriodStart == default ? null : run.PeriodStart;
            model.PeriodEnd = run.PeriodEnd == default ? null : run.PeriodEnd;
            model.PayDate = run.PayDate == default ? null : run.PayDate;
            model.Schedules = [.. (await _schedules.ListEnabledAsync()).Select(schedule => new SelectListItem(schedule.Name, schedule.ItemId, schedule.ItemId == run.PayScheduleId))];

            // A new run on the only schedule starts with it chosen.
            if (context.IsNew && string.IsNullOrEmpty(model.PayScheduleId) && model.Schedules.Count == 1)
            {
                model.PayScheduleId = model.Schedules[0].Value;
            }
        }).Location(l => l.Zone("Content", "1").Card(S["Pay Run"], "10"));

    public override async Task<IDisplayResult> UpdateAsync(PayRun run, UpdateEditorContext context)
    {
        var model = new PayRunViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        run.Title = PayRunTitles.Trim(model.Title);
        run.PayScheduleId = string.IsNullOrEmpty(model.PayScheduleId) ? null : model.PayScheduleId;
        run.Type = Enum.TryParse<PayRunType>(model.Type, out var type) ? type : PayRunType.Regular;
        run.SupplementalMethod = Enum.TryParse<PayrollSupplementalMethod>(model.SupplementalMethod, out var method) ? method : PayrollSupplementalMethod.Aggregate;

        // Everyone on the schedule, or only the people chosen (whatever their schedule, such as someone's final pay).
        run.StaffMemberIds = model.Paid == PaidPeople
            ? [.. (model.StaffMemberIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal)]
            : [];

        if (model.Paid == PaidPeople && run.StaffMemberIds.Count == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.StaffMemberIds), S["Choose the people the run pays, or pay everyone on the pay schedule."]);
        }

        if (model.Paid != PaidPeople && string.IsNullOrEmpty(run.PayScheduleId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PayScheduleId), S["Choose the pay schedule whose people the run pays."]);
        }

        // An empty period takes the schedule's period of today, and its pay date.
        if ((model.PeriodStart is null || model.PeriodEnd is null || model.PayDate is null) && !string.IsNullOrEmpty(run.PayScheduleId)
            && await _schedules.FindByIdAsync(run.PayScheduleId) is { } schedule)
        {
            var (start, end) = schedule.PeriodOn(DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime));

            model.PeriodStart ??= start;
            model.PeriodEnd ??= end;
            model.PayDate ??= schedule.PayDateFor(model.PeriodEnd.Value);
        }

        if (model.PeriodStart is { } periodStart && model.PeriodEnd is { } periodEnd && model.PayDate is { } payDate)
        {
            run.PeriodStart = periodStart;
            run.PeriodEnd = periodEnd;
            run.PayDate = payDate;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PeriodStart), S["Give the period and the pay date, or choose a pay schedule to take its current period."]);
        }

        return Edit(run, context);
    }
}

/// <summary>
/// With Multiple Employers on: the employer of a new pay run, the company by default, and its name on the rows of the
/// pay run list.
/// </summary>
internal sealed class PayRunEmployerDisplayDriver : DisplayDriver<PayRun>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPayrollScopeService _scope;
    private readonly EmployerStore _employers;

    internal readonly IStringLocalizer S;

    public PayRunEmployerDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IPayrollScopeService scope,
        EmployerStore employers,
        IStringLocalizer<PayRunEmployerDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _scope = scope;
        _employers = employers;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(PayRun run, BuildDisplayContext context)
        => context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            ? View("PayrollEmployerName_SummaryAdmin", run.EmployerId)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:1")
            : null;

    public override IDisplayResult Edit(PayRun run, BuildEditorContext context)
        => Initialize<PayrollEmployerViewModel>("PayrollEmployer_Edit", async model =>
        {
            var scope = await _scope.GetScopeAsync(_httpContextAccessor.HttpContext?.User, PayrollPermissions.PreparePayRuns);

            model.EmployerId = run.EmployerId;
            model.Employers = [.. (await _employers.ListAsync()).Where(employer => scope.Allows(employer.ItemId)).Select(employer => new SelectListItem(employer.Name, employer.ItemId, employer.ItemId == run.EmployerId))];
            model.Hint = S["The legal entity that pays the people of the run."];
        }).Location(l => l.Zone("Content", "0").Card(S["Pay Run"], "10"));

    public override async Task<IDisplayResult> UpdateAsync(PayRun run, UpdateEditorContext context)
    {
        var model = new PayrollEmployerViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.EmployerId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.EmployerId), S["Choose the employer."]);
        }
        else
        {
            run.EmployerId = model.EmployerId;
        }

        return Edit(run, context);
    }
}

/// <summary>
/// The rows of a pay run's register: one per statement.
/// </summary>
internal sealed class PayStatementDisplayDriver : DisplayDriver<PayStatement>
{
    public override Task<IDisplayResult> DisplayAsync(PayStatement statement, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{statement.Snapshot?.EmployeeName} {statement.Snapshot?.EmployeeNumber}".ToLowerInvariant();
        }

        return CombineAsync(
            View("PayStatement_Fields_SummaryAdmin", statement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("PayStatement_Tags_SummaryAdmin", statement)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("PayStatement_Buttons_SummaryAdmin", statement).Location("Actions:5"));
    }
}
