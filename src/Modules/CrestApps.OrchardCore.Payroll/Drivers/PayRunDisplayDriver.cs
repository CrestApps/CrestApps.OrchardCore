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
/// The rows of the pay run list and the editor of a new run: the employer, the schedule, the type, the period and the
/// pay date (the schedule's next period by default).
/// </summary>
internal sealed class PayRunDisplayDriver : DisplayDriver<PayRun>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPayrollScopeService _scope;
    private readonly EmployerStore _employers;
    private readonly IPayScheduleManager _schedules;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public PayRunDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IPayrollScopeService scope,
        EmployerStore employers,
        IPayScheduleManager schedules,
        ILocalClock localClock,
        IStringLocalizer<PayRunDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _scope = scope;
        _employers = employers;
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
            rowShape.Attributes["data-filter-value"] = $"{run.Number} {run.Type} {run.Status}".ToLowerInvariant();
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
            model.EmployerId = run.EmployerId;
            model.PayScheduleId = run.PayScheduleId;
            model.Type = run.Type.ToString();
            model.SupplementalMethod = run.SupplementalMethod.ToString();
            model.PeriodStart = run.PeriodStart == default ? null : run.PeriodStart;
            model.PeriodEnd = run.PeriodEnd == default ? null : run.PeriodEnd;
            model.PayDate = run.PayDate == default ? null : run.PayDate;

            var scope = await _scope.GetScopeAsync(_httpContextAccessor.HttpContext?.User, PayrollPermissions.PreparePayRuns);

            model.Employers = [.. (await _employers.ListAsync()).Where(employer => scope.Allows(employer.ItemId)).Select(employer => new SelectListItem(employer.Name, employer.ItemId, employer.ItemId == run.EmployerId))];
            model.Schedules = [.. (await _schedules.ListEnabledAsync()).Select(schedule => new SelectListItem(schedule.Name, schedule.ItemId, schedule.ItemId == run.PayScheduleId))];

            // A new run of the only employer, or on the only schedule, starts with it chosen.
            if (context.IsNew)
            {
                if (string.IsNullOrEmpty(model.EmployerId) && model.Employers.Count == 1)
                {
                    model.EmployerId = model.Employers[0].Value;
                }

                if (string.IsNullOrEmpty(model.PayScheduleId) && model.Schedules.Count == 1)
                {
                    model.PayScheduleId = model.Schedules[0].Value;
                }
            }
        }).Location("Content:1");

    public override async Task<IDisplayResult> UpdateAsync(PayRun run, UpdateEditorContext context)
    {
        var model = new PayRunViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        run.EmployerId = model.EmployerId;
        run.PayScheduleId = string.IsNullOrEmpty(model.PayScheduleId) ? null : model.PayScheduleId;
        run.Type = Enum.TryParse<PayRunType>(model.Type, out var type) ? type : PayRunType.Regular;
        run.SupplementalMethod = Enum.TryParse<PayrollSupplementalMethod>(model.SupplementalMethod, out var method) ? method : PayrollSupplementalMethod.Aggregate;

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
