using CrestApps.OrchardCore.Payroll.Core.Calculation;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll.Controllers;

/// <summary>
/// Shows what a pay profile would be paid for the current period of its schedule, without keeping anything: the
/// lines, the taxes, net pay and its split, and the warnings of the calculation.
/// </summary>
[Admin]
[Feature(PayrollConstants.Features.Payroll)]
// Pay is private: the signed-in person confirms their password before seeing it.
[ConfirmPassword]
public sealed class PayPreviewsController : Controller
{
    private readonly IPayProfileManager _profiles;
    private readonly IPayScheduleManager _schedules;
    private readonly IPayrollScopeService _scope;
    private readonly PayCalculationContextFactory _contexts;
    private readonly IPayCalculator _calculator;
    private readonly PayrollNames _names;
    private readonly ILocalClock _localClock;

    public PayPreviewsController(
        IPayProfileManager profiles,
        IPayScheduleManager schedules,
        IPayrollScopeService scope,
        PayCalculationContextFactory contexts,
        IPayCalculator calculator,
        PayrollNames names,
        ILocalClock localClock)
    {
        _profiles = profiles;
        _schedules = schedules;
        _scope = scope;
        _contexts = contexts;
        _calculator = calculator;
        _names = names;
        _localClock = localClock;
    }

    [Admin("payroll/employees/preview/{id}", "PayrollPayPreview")]
    public async Task<IActionResult> Index(string id, DateOnly? on)
    {
        if (string.IsNullOrEmpty(id) || await _profiles.FindByIdAsync(id) is not { } profile)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ViewPayProfiles, profile.EmployerId))
        {
            return Forbid();
        }

        var model = new PayPreviewViewModel
        {
            ProfileId = profile.ItemId,
            StaffName = await _names.StaffAsync(profile.StaffMemberId),
        };

        if (string.IsNullOrEmpty(profile.PayScheduleId) || await _schedules.FindByIdAsync(profile.PayScheduleId) is not { } schedule)
        {
            model.MissingSchedule = true;

            return View(model);
        }

        var day = on ?? DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
        var (start, end) = schedule.PeriodOn(day);
        var context = await _contexts.CreateAsync(profile, new PayCalculationRequest
        {
            PeriodStart = start,
            PeriodEnd = end,
            PayDate = schedule.PayDateFor(end),
        }, HttpContext.RequestAborted);

        await _calculator.CalculateAsync(context, HttpContext.RequestAborted);

        model.PeriodStart = start;
        model.PeriodEnd = end;
        model.PayDate = context.PayDate;
        model.Currency = context.Currency;
        model.Lines = [.. context.Lines.Select(line => new PayPreviewLine(line, line.PayCodeId is null ? null : context.Code(line.PayCodeId)?.Code))];
        model.Gross = context.Gross;
        model.Net = context.Round(context.Net);
        model.Payments = [.. context.Payments];
        model.Warnings.AddRange(context.Warnings);

        return View(model);
    }
}
