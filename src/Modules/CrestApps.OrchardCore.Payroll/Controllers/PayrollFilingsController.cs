using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll.Controllers;

/// <summary>
/// Payroll &gt; Liabilities and Filings: the taxes owed by period with the deposits made, the filings prepared from the
/// final statements, each filing's boxes and wage statements, and year end. For <c>ManagePayrollFilings</c> per employer.
/// </summary>
[Admin]
[Feature(PayrollConstants.Features.Payroll)]
public sealed class PayrollFilingsController : Controller
{
    private readonly ITaxLiabilityService _liabilities;
    private readonly ITaxDepositManager _deposits;
    private readonly IPayrollFilingManager _filings;
    private readonly IPayrollFilingService _service;
    private readonly IEmployerManager _employers;
    private readonly IPayrollScopeService _scope;
    private readonly PayrollTaxFormDocuments _documents;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public PayrollFilingsController(
        ITaxLiabilityService liabilities,
        ITaxDepositManager deposits,
        IPayrollFilingManager filings,
        IPayrollFilingService service,
        IEmployerManager employers,
        IPayrollScopeService scope,
        PayrollTaxFormDocuments documents,
        ILocalClock localClock,
        INotifier notifier,
        IStringLocalizerFactory localizerFactory,
        IHtmlLocalizer<PayrollFilingsController> htmlLocalizer,
        IStringLocalizer<PayrollFilingsController> stringLocalizer)
    {
        _liabilities = liabilities;
        _deposits = deposits;
        _filings = filings;
        _service = service;
        _employers = employers;
        _scope = scope;
        _documents = documents;
        _localClock = localClock;
        _notifier = notifier;
        _localizerFactory = localizerFactory;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("payroll/liabilities", "PayrollLiabilities")]
    public async Task<IActionResult> Liabilities(string employerId, int? year)
    {
        var (employers, employer) = await EmployersAsync(employerId);

        if (employers is null)
        {
            return Forbid();
        }

        var chosenYear = year ?? await TodayYearAsync();
        var periods = employer is null ? [] : await _liabilities.ListAsync(employer.ItemId, new DateOnly(chosenYear, 1, 1), new DateOnly(chosenYear, 12, 31), HttpContext.RequestAborted);
        var deposits = employer is null ? [] : await _deposits.ListAsync(employer.ItemId, new DateOnly(chosenYear, 1, 1), new DateOnly(chosenYear + 1, 1, 31), HttpContext.RequestAborted);

        return View(new PayrollLiabilitiesViewModel
        {
            EmployerId = employer?.ItemId,
            Year = chosenYear,
            Employers = Options(employers, employer?.ItemId),
            Currency = employer?.Currency,
            Periods = periods,
            Deposits = deposits,
            PeriodList = new AdminListContext<TaxLiabilityPeriod> { Source = periods },
            DepositList = new AdminListContext<TaxDeposit> { Source = deposits },
        });
    }

    [HttpPost]
    [Admin("payroll/liabilities/deposits", "PayrollLiabilitiesDeposit")]
    public async Task<IActionResult> RecordDeposit([Bind(Prefix = nameof(PayrollLiabilitiesViewModel.NewDeposit))] TaxDepositViewModel model)
    {
        if (string.IsNullOrEmpty(model?.EmployerId) || await _employers.FindByIdAsync(model.EmployerId) is not { } employer)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayrollFilings, employer.ItemId))
        {
            return Forbid();
        }

        // An amount that can't be read is refused as such, never recorded as 0 or another number.
        if (ModelState.GetFieldValidationState(nameof(PayrollLiabilitiesViewModel.NewDeposit) + "." + nameof(model.Amount)) == ModelValidationState.Invalid)
        {
            await _notifier.ErrorAsync(H["Enter the amount as a number, such as 1250.00."]);

            return RedirectToAction(nameof(Liabilities), new { employerId = employer.ItemId });
        }

        var deposit = await _deposits.NewAsync();

        deposit.EmployerId = employer.ItemId;
        deposit.Jurisdiction = model.Jurisdiction?.Trim().ToUpperInvariant();
        deposit.Group = model.Group is TaxLiabilityGroups.Unemployment or TaxLiabilityGroups.NonPayrollWithholding ? model.Group : TaxLiabilityGroups.Withholding;
        deposit.PeriodStart = model.PeriodStart ?? default;
        deposit.PeriodEnd = model.PeriodEnd ?? default;
        deposit.Amount = model.Amount ?? 0;
        deposit.PaidOn = model.PaidOn ?? default;
        deposit.Reference = string.IsNullOrWhiteSpace(model.Reference) ? null : model.Reference.Trim();
        deposit.RecordedBy = User.Identity?.Name;

        var validation = await _deposits.ValidateAsync(deposit);

        if (!validation.Succeeded)
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))]);
        }
        else
        {
            await _deposits.CreateAsync(deposit);
            await _notifier.SuccessAsync(H["The deposit has been recorded."]);
        }

        return RedirectToAction(nameof(Liabilities), new { employerId = employer.ItemId, year = deposit.PeriodEnd == default ? (int?)null : deposit.PeriodEnd.Year });
    }

    [HttpPost]
    [Admin("payroll/liabilities/deposits/delete/{id}", "PayrollLiabilitiesDepositDelete")]
    public async Task<IActionResult> DeleteDeposit(string id)
    {
        if (string.IsNullOrEmpty(id) || await _deposits.FindByIdAsync(id) is not { } deposit)
        {
            return RedirectToAction(nameof(Liabilities));
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayrollFilings, deposit.EmployerId))
        {
            return Forbid();
        }

        // Rule C: a deposit counted in a filed return is part of it.
        var quarter = ((deposit.PeriodEnd.Month - 1) / 3) + 1;
        var filed = (await _filings.ListByEmployersAsync([deposit.EmployerId]))
            .Any(filing => filing.Status != PayrollFilingStatus.Draft && filing.Year == deposit.PeriodEnd.Year && (filing.Quarter == 0 || filing.Quarter == quarter));

        if (filed)
        {
            await _notifier.ErrorAsync(H["The deposit is counted in a filed return, so it is kept: amend the return to correct it."]);
        }
        else if (await _deposits.DeleteAsync(deposit))
        {
            await _notifier.SuccessAsync(H["The deposit has been removed."]);
        }

        return RedirectToAction(nameof(Liabilities), new { employerId = deposit.EmployerId, year = deposit.PeriodEnd.Year });
    }

    [Admin("payroll/filings", "PayrollFilings")]
    public async Task<IActionResult> Index(string employerId)
    {
        var (employers, employer) = await EmployersAsync(employerId);

        if (employers is null)
        {
            return Forbid();
        }

        var filings = (await _filings.ListByEmployersAsync([.. employers.Select(candidate => candidate.ItemId)], HttpContext.RequestAborted))
            .OrderByDescending(filing => filing.Year)
            .ThenByDescending(filing => filing.Quarter)
            .ThenBy(filing => filing.Form, StringComparer.Ordinal)
            .ToList();

        return View(new PayrollFilingsIndexViewModel
        {
            EmployerId = employer?.ItemId,
            Year = await TodayYearAsync(),
            Employers = Options(employers, employer?.ItemId),
            Forms = employer is null ? [] : [.. _service.FormsFor(employer).Select(form => new SelectListItem(_localizerFactory.Localize(form.DisplayName).Value, form.Name))],
            Filings = filings,
            List = new AdminListContext<PayrollFiling> { Source = filings },
        });
    }

    [HttpPost]
    [Admin("payroll/filings/prepare", "PayrollFilingsPrepare")]
    public async Task<IActionResult> Prepare(string employerId, string form, int year, int quarter)
    {
        if (string.IsNullOrEmpty(employerId) || await _employers.FindByIdAsync(employerId) is not { } employer)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayrollFilings, employer.ItemId))
        {
            return Forbid();
        }

        var result = await _service.PrepareAsync(employer, form, year, quarter, User, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", result.Errors)]);

            return RedirectToAction(nameof(Index), new { employerId });
        }

        await _notifier.SuccessAsync(H["The filing has been prepared from the final statements."]);

        return RedirectToAction(nameof(Details), new { id = result.Filing.ItemId });
    }

    [Admin("payroll/filings/{id}", "PayrollFilingsDetails")]
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id) || await _filings.FindByIdAsync(id) is not { } filing)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayrollFilings, filing.EmployerId))
        {
            return Forbid();
        }

        var employer = await _employers.FindByIdAsync(filing.EmployerId);
        var form = employer is null ? null : _service.FormsFor(employer).FirstOrDefault(candidate => candidate.Name == filing.Form);

        return View(new PayrollFilingDetailsViewModel
        {
            Filing = filing,
            EmployerName = employer?.Name,
            FormName = form is null ? filing.Form : _localizerFactory.Localize(form.DisplayName).Value,
            FiledOn = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime),
            Amends = string.IsNullOrEmpty(filing.AmendsId) ? null : await _filings.FindByIdAsync(filing.AmendsId),
        });
    }

    [HttpPost]
    [Admin("payroll/filings/{id}/prepare", "PayrollFilingsPrepareAgain")]
    public async Task<IActionResult> PrepareAgain(string id)
    {
        if (string.IsNullOrEmpty(id) || await _filings.FindByIdAsync(id) is not { } filing)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayrollFilings, filing.EmployerId))
        {
            return Forbid();
        }

        var employer = await _employers.FindByIdAsync(filing.EmployerId);
        var result = await _service.PrepareAsync(employer, filing.Form, filing.Year, filing.Quarter, User, HttpContext.RequestAborted);

        await (result.Succeeded ? _notifier.SuccessAsync(H["The filing has been prepared again."]) : _notifier.ErrorAsync(H["{0}", string.Join(" ", result.Errors)]));

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Admin("payroll/filings/{id}/filed", "PayrollFilingsFiled")]
    public async Task<IActionResult> MarkFiled(string id, DateOnly? filedOn, string reference)
    {
        if (string.IsNullOrEmpty(id) || await _filings.FindByIdAsync(id) is not { } filing)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayrollFilings, filing.EmployerId))
        {
            return Forbid();
        }

        var result = await _service.MarkFiledAsync(filing, filedOn ?? default, reference, User, HttpContext.RequestAborted);

        await (result.Succeeded ? _notifier.SuccessAsync(H["The filing is marked as filed."]) : _notifier.ErrorAsync(H["{0}", string.Join(" ", result.Errors)]));

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Admin("payroll/filings/{id}/amend", "PayrollFilingsAmend")]
    public async Task<IActionResult> Amend(string id)
    {
        if (string.IsNullOrEmpty(id) || await _filings.FindByIdAsync(id) is not { } filing)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayrollFilings, filing.EmployerId))
        {
            return Forbid();
        }

        var result = await _service.AmendAsync(filing, User, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", result.Errors)]);

            return RedirectToAction(nameof(Details), new { id });
        }

        await _notifier.SuccessAsync(H["An amended filing has been prepared: check it, file it, then mark it as filed."]);

        return RedirectToAction(nameof(Details), new { id = result.Filing.ItemId });
    }

    [HttpPost]
    [Admin("payroll/filings/{id}/delete", "PayrollFilingsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrEmpty(id) || await _filings.FindByIdAsync(id) is not { } filing)
        {
            return RedirectToAction(nameof(Index));
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayrollFilings, filing.EmployerId))
        {
            return Forbid();
        }

        // Rule C: a filed form is a record of what the business filed.
        if (filing.Status != PayrollFilingStatus.Draft)
        {
            await _notifier.ErrorAsync(H["A filed form is kept; amend it to correct it."]);

            return RedirectToAction(nameof(Details), new { id });
        }

        await _filings.DeleteAsync(filing);
        await _notifier.SuccessAsync(H["The draft has been removed."]);

        return RedirectToAction(nameof(Index), new { employerId = filing.EmployerId });
    }

    [Admin("payroll/filings/{id}/people/{profileId}/pdf", "PayrollFilingsPersonPdf")]
    public async Task<IActionResult> PersonPdf(string id, string profileId)
    {
        if (string.IsNullOrEmpty(id) || await _filings.FindByIdAsync(id) is not { } filing || filing.People.FirstOrDefault(person => person.ProfileId == profileId) is not { } person)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayrollFilings, filing.EmployerId))
        {
            return Forbid();
        }

        return File(await _documents.WriteAsync(filing, person), "application/pdf", PayrollTaxFormDocuments.FileName(filing));
    }

    [Admin("payroll/year-end", "PayrollYearEnd")]
    public async Task<IActionResult> YearEnd(string employerId, int? year)
    {
        var (employers, employer) = await EmployersAsync(employerId);

        if (employers is null)
        {
            return Forbid();
        }

        var chosenYear = year ?? await TodayYearAsync() - 1;

        return View(new PayrollYearEndViewModel
        {
            EmployerId = employer?.ItemId,
            Year = chosenYear,
            Employers = Options(employers, employer?.ItemId),
            Closed = employer?.ClosedYears.Contains(chosenYear) == true,
            Blockers = employer is null ? [] : await _service.YearEndBlockersAsync(employer, chosenYear, HttpContext.RequestAborted),
        });
    }

    [HttpPost]
    [Admin("payroll/year-end/close", "PayrollYearEndClose")]
    public async Task<IActionResult> CloseYear(string employerId, int year, bool reopen = false)
    {
        if (string.IsNullOrEmpty(employerId) || await _employers.FindByIdAsync(employerId) is not { } employer)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ManagePayrollFilings, employer.ItemId))
        {
            return Forbid();
        }

        var result = reopen
            ? await _service.ReopenYearAsync(employer, year, User, HttpContext.RequestAborted)
            : await _service.CloseYearAsync(employer, year, User, HttpContext.RequestAborted);

        await (result.Succeeded
            ? _notifier.SuccessAsync(reopen ? H["The year {0} has been reopened.", year] : H["The year {0} has been closed; its wage statements are prepared under Filings.", year])
            : _notifier.ErrorAsync(H["{0}", string.Join(" ", result.Errors)]));

        return RedirectToAction(nameof(YearEnd), new { employerId, year });
    }

    // The employers the user files for, and the one chosen (the only one, or the one asked for).
    private async Task<(IReadOnlyList<Employer> Employers, Employer Chosen)> EmployersAsync(string employerId)
    {
        var scope = await _scope.GetScopeAsync(User, PayrollPermissions.ManagePayrollFilings, HttpContext.RequestAborted);

        if (scope.IsEmpty)
        {
            return (null, null);
        }

        var employers = (await _employers.GetAllAsync(HttpContext.RequestAborted))
            .Where(employer => scope.All || scope.EmployerIds.Contains(employer.ItemId))
            .OrderBy(employer => employer.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var chosen = employers.FirstOrDefault(employer => employer.ItemId == employerId) ?? (employers.Count == 1 ? employers[0] : null);

        return (employers, chosen);
    }

    private static List<SelectListItem> Options(IEnumerable<Employer> employers, string selected)
        => [.. employers.Select(employer => new SelectListItem(employer.Name, employer.ItemId, employer.ItemId == selected))];

    private async Task<int> TodayYearAsync()
        => (await _localClock.GetLocalNowAsync()).Year;
}
