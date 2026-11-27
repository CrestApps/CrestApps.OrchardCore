using System.Globalization;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll.Controllers;

/// <summary>
/// Payroll &gt; Pay Runs: adding a run, entering its inputs, calculating it, reviewing its register, and submitting it
/// for approval. Every action follows the payroll scope of the run's employer.
/// </summary>
[Admin]
[Feature(PayrollConstants.Features.Payroll)]
public sealed class PayRunsController : Controller
{
    private readonly IPayRunManager _runs;
    private readonly IPayRunService _service;
    private readonly IPayRunInputManager _inputs;
    private readonly IPayrollScopeService _scope;
    private readonly IPayProfileService _profileService;
    private readonly EmployerStore _employers;
    private readonly IPayScheduleManager _schedules;
    private readonly IPayCodeManager _codes;
    private readonly IPayProfileManager _profiles;
    private readonly PayrollNames _names;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<PayRun> _displayManager;
    private readonly IDisplayManager<PayrollEmployerFilter> _filterDisplay;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public PayRunsController(
        IPayRunManager runs,
        IPayRunService service,
        IPayRunInputManager inputs,
        IPayrollScopeService scope,
        IPayProfileService profileService,
        EmployerStore employers,
        IPayScheduleManager schedules,
        IPayCodeManager codes,
        IPayProfileManager profiles,
        PayrollNames names,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<PayRun> displayManager,
        IDisplayManager<PayrollEmployerFilter> filterDisplay,
        INotifier notifier,
        IHtmlLocalizer<PayRunsController> htmlLocalizer,
        IStringLocalizer<PayRunsController> stringLocalizer)
    {
        _runs = runs;
        _service = service;
        _inputs = inputs;
        _scope = scope;
        _profileService = profileService;
        _employers = employers;
        _schedules = schedules;
        _codes = codes;
        _profiles = profiles;
        _names = names;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _filterDisplay = filterDisplay;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("payroll/pay-runs", "PayrollPayRunsIndex")]
    public async Task<IActionResult> Index(string employerId)
    {
        var view = await _scope.GetScopeAsync(User, PayrollPermissions.ViewPayRuns, HttpContext.RequestAborted);

        if (view.IsEmpty)
        {
            return Forbid();
        }

        var employers = (await _employers.ListAsync(HttpContext.RequestAborted)).Where(employer => view.Allows(employer.ItemId)).ToList();
        var chosen = employers.Any(employer => employer.ItemId == employerId) ? [employerId] : employers.Select(employer => employer.ItemId).ToArray();
        var model = new PayRunsIndexViewModel
        {
            EmployerId = chosen.Length == 1 && employers.Count > 1 ? chosen[0] : null,
            CanAdd = !(await _scope.GetScopeAsync(User, PayrollPermissions.PreparePayRuns, HttpContext.RequestAborted)).IsEmpty,
        };

        // Multiple Employers places its employer picker in the toolbar; the company's own payroll shows none.
        model.List = new AdminListContext<PayRun>
        {
            Source = await _runs.ListByEmployersAsync(chosen, HttpContext.RequestAborted),
            ToolbarActions = await _filterDisplay.BuildDisplayAsync(new PayrollEmployerFilter
            {
                EmployerId = model.EmployerId,
                FormId = "payroll-runs-filter",
                Employers = [.. employers.Select(employer => new SelectListItem(employer.Name, employer.ItemId, employer.ItemId == model.EmployerId))],
            }, _updateModelAccessor.ModelUpdater, PayrollEmployerFilterDisplayTypes.Toolbar),
        };

        return View(model);
    }

    [Admin("payroll/pay-runs/create", "PayrollPayRunsCreate")]
    public async Task<IActionResult> Create()
    {
        if ((await _scope.GetScopeAsync(User, PayrollPermissions.PreparePayRuns, HttpContext.RequestAborted)).IsEmpty)
        {
            return Forbid();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = S["New pay run"],
            Editor = await _displayManager.BuildEditorAsync(await _runs.NewAsync(), _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("payroll/pay-runs/create", "PayrollPayRunsCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if ((await _scope.GetScopeAsync(User, PayrollPermissions.PreparePayRuns, HttpContext.RequestAborted)).IsEmpty)
        {
            return Forbid();
        }

        var run = await _runs.NewAsync();
        var model = new EditCatalogEntryViewModel
        {
            DisplayName = S["New pay run"],
            Editor = await _displayManager.UpdateEditorAsync(run, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.PreparePayRuns, run.EmployerId))
        {
            return Forbid();
        }

        if (await CatalogEntryValidation.ValidateAsync(_runs, run, _updateModelAccessor.ModelUpdater, string.Empty) && ModelState.IsValid)
        {
            await _service.OpenAsync(run, User, HttpContext.RequestAborted);
            await _notifier.SuccessAsync(H["{0} has been added. Enter its inputs, then calculate it.", _names.Run(run)]);

            return RedirectToAction(nameof(Details), new { id = run.ItemId });
        }

        return View(model);
    }

    [ConfirmPassword]
    [Admin("payroll/pay-runs/{id}", "PayrollPayRunsDetails")]
    public async Task<IActionResult> Details(string id)
    {
        if (await FindAsync(id) is not { } run)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ViewPayRuns, run.EmployerId))
        {
            return Forbid();
        }

        return View(await DetailsModelAsync(run));
    }

    [HttpPost]
    [Admin("payroll/pay-runs/{id}/calculate", "PayrollPayRunsCalculate")]
    public async Task<IActionResult> Calculate(string id)
    {
        if (await FindAsync(id) is not { } run)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.PreparePayRuns, run.EmployerId))
        {
            return Forbid();
        }

        await NotifyAsync(await _service.CalculateAsync(run, User, cancellationToken: HttpContext.RequestAborted), H["{0} has been calculated.", _names.Run(run)]);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Admin("payroll/pay-runs/{id}/submit", "PayrollPayRunsSubmit")]
    public async Task<IActionResult> Submit(string id)
    {
        if (await FindAsync(id) is not { } run)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.PreparePayRuns, run.EmployerId))
        {
            return Forbid();
        }

        await NotifyAsync(await _service.SubmitAsync(run, User, HttpContext.RequestAborted), H["{0} has been submitted for approval.", _names.Run(run)]);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Admin("payroll/pay-runs/{id}/decide", "PayrollPayRunsDecide")]
    public async Task<IActionResult> Decide(string id, string decision, string note)
    {
        if (await FindAsync(id) is not { } run)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ApprovePayRuns, run.EmployerId))
        {
            return Forbid();
        }

        if (run.Status != PayRunStatus.PendingApproval || string.IsNullOrEmpty(run.ApprovalRequestId)
            || HttpContext.RequestServices.GetService<IApprovalRequestManager>() is not { } requests
            || HttpContext.RequestServices.GetService<IApprovalService>() is not { } approvals
            || await requests.FindByIdAsync(run.ApprovalRequestId) is not { } request
            || !Enum.TryParse<CrestApps.OrchardCore.Approvals.Models.ApprovalDecisionKind>(decision, out var kind))
        {
            await _notifier.ErrorAsync(H["The pay run is not waiting for approval."]);

            return RedirectToAction(nameof(Details), new { id });
        }

        // The decision goes through Approvals: its policy says who may decide, and the preparer may not.
        var result = await approvals.DecideAsync(request, User, kind, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(kind == CrestApps.OrchardCore.Approvals.Models.ApprovalDecisionKind.Approve ? H["Your approval has been recorded."] : H["Your decision has been recorded."]);
        }
        else
        {
            foreach (var error in result.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Admin("payroll/pay-runs/{id}/cancel", "PayrollPayRunsCancel")]
    public async Task<IActionResult> Cancel(string id)
    {
        if (await FindAsync(id) is not { } run)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.PreparePayRuns, run.EmployerId))
        {
            return Forbid();
        }

        await NotifyAsync(await _service.CancelAsync(run, S["Cancelled on the pay run's page."], HttpContext.RequestAborted), H["{0} has been cancelled.", _names.Run(run)]);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Admin("payroll/pay-runs/{id}/inputs", "PayrollPayRunsAddInput")]
    public async Task<IActionResult> AddInput(string id, [Bind(Prefix = nameof(PayRunDetailsViewModel.NewInput))] PayRunInputViewModel model)
    {
        if (await FindAsync(id) is not { } run)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.PreparePayRuns, run.EmployerId))
        {
            return Forbid();
        }

        // Nobody enters their own pay, unless the settings allow it.
        if (!string.IsNullOrEmpty(model?.StaffMemberId) && !await _profileService.MayChangePayOfAsync(User, model.StaffMemberId, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        if (!run.IsOpen)
        {
            await _notifier.ErrorAsync(H["The pay run was processed or cancelled; it takes no more inputs."]);

            return RedirectToAction(nameof(Details), new { id });
        }

        var input = await _inputs.NewAsync();

        input.RunId = run.ItemId;
        input.StaffMemberId = model?.StaffMemberId;
        input.PayCodeId = model?.PayCodeId;
        input.Date = model?.Date;
        input.Note = string.IsNullOrWhiteSpace(model?.Note) ? null : model.Note.Trim();
        input.Origin = S["Typed on the pay run's page"];

        if (!TryNumber(model?.Hours, out var hours) || !TryNumber(model?.Amount, out var amount) || !TryNumber(model?.Rate, out var rate))
        {
            await _notifier.ErrorAsync(H["Hours, amounts and rates are numbers, with a dot for decimals."]);

            return RedirectToAction(nameof(Details), new { id });
        }

        input.Hours = hours;
        input.Amount = amount;
        input.Rate = rate;
        input.IsNet = model?.IsNet == true;
        input.EarnedFrom = model?.EarnedFrom;
        input.EarnedTo = model?.EarnedTo;

        if ((input.EarnedFrom is null) != (input.EarnedTo is null) || input.EarnedTo < input.EarnedFrom || input.EarnedTo >= run.PeriodStart)
        {
            await _notifier.ErrorAsync(H["Give both days of the weeks the amount was earned in, before the run's period, or neither."]);

            return RedirectToAction(nameof(Details), new { id });
        }

        if (input.IsNet && input.Amount is not > 0)
        {
            await _notifier.ErrorAsync(H["A take-home amount is an amount: the gross is worked out from it."]);

            return RedirectToAction(nameof(Details), new { id });
        }

        var validation = await _inputs.ValidateAsync(input);

        if (!validation.Succeeded)
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))]);

            return RedirectToAction(nameof(Details), new { id });
        }

        await _inputs.CreateAsync(input);
        await RecalculateAsync(run, input.StaffMemberId);
        await _notifier.SuccessAsync(H["The input has been added."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Admin("payroll/pay-runs/{id}/inputs/{inputId}/delete", "PayrollPayRunsDeleteInput")]
    public async Task<IActionResult> DeleteInput(string id, string inputId)
    {
        if (await FindAsync(id) is not { } run || string.IsNullOrEmpty(inputId) || await _inputs.FindByIdAsync(inputId) is not { } input || input.RunId != run.ItemId)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.PreparePayRuns, run.EmployerId)
            || !await _profileService.MayChangePayOfAsync(User, input.StaffMemberId, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        if (!run.IsOpen)
        {
            await _notifier.ErrorAsync(H["The pay run was processed or cancelled; its inputs are kept as they were."]);

            return RedirectToAction(nameof(Details), new { id });
        }

        await _inputs.DeleteAsync(input);
        await RecalculateAsync(run, input.StaffMemberId);
        await _notifier.SuccessAsync(H["The input has been removed."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    [ConfirmPassword]
    [Admin("payroll/pay-runs/statements/{id}", "PayrollPayRunsStatement")]
    public async Task<IActionResult> Statement(string id, [FromServices] PayStatementStore statements)
    {
        if (string.IsNullOrEmpty(id) || await statements.FindByIdAsync(id) is not { } statement)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ViewPayRuns, statement.EmployerId))
        {
            return Forbid();
        }

        var linkedId = statement.VoidOf ?? statement.VoidedBy;

        return View(new PayStatementViewModel
        {
            Statement = statement,
            Run = string.IsNullOrEmpty(statement.RunId) ? null : await _runs.FindByIdAsync(statement.RunId),
            Codes = (await _codes.GetAllAsync()).ToDictionary(code => code.ItemId, code => code.Code, StringComparer.Ordinal),
            CanVoid = await _scope.AuthorizeAsync(User, PayrollPermissions.VoidPayStatements, statement.EmployerId),
            LinkedNumber = string.IsNullOrEmpty(linkedId) ? null : (await statements.FindByIdAsync(linkedId))?.Number,
        });
    }

    /// <summary>
    /// Downloads a statement's stub as a PDF.
    /// </summary>
    /// <param name="id">The statement's id.</param>
    /// <param name="statements">The statements.</param>
    /// <param name="stubs">Writes the stub.</param>
    /// <returns>The file.</returns>
    [ConfirmPassword]
    [Admin("payroll/pay-runs/statements/{id}/pdf", "PayrollPayRunsStatementPdf")]
    public async Task<IActionResult> StatementPdf(string id, [FromServices] PayStatementStore statements, [FromServices] PayStubService stubs)
    {
        if (string.IsNullOrEmpty(id) || await statements.FindByIdAsync(id) is not { } statement)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ViewPayRuns, statement.EmployerId))
        {
            return Forbid();
        }

        return File(await stubs.WriteAsync(statement, HttpContext.RequestAborted), "application/pdf", PayStubService.FileName(statement));
    }

    /// <summary>
    /// Processes an approved run: its statements become final and net pay is paid.
    /// </summary>
    /// <param name="id">The run's id.</param>
    /// <returns>The run page.</returns>
    [HttpPost]
    [Admin("payroll/pay-runs/{id}/process", "PayrollPayRunsProcess")]
    public async Task<IActionResult> Process(string id)
    {
        if (await FindAsync(id) is not { } run)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.ProcessPayRuns, run.EmployerId))
        {
            return Forbid();
        }

        await NotifyAsync(await _service.ProcessAsync(run, User, HttpContext.RequestAborted), H["{0} has been processed.", _names.Run(run)]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Voids a final statement.
    /// </summary>
    /// <param name="id">The statement's id.</param>
    /// <param name="reason">Why.</param>
    /// <param name="statements">The statements.</param>
    /// <returns>The statement page.</returns>
    [HttpPost]
    [ConfirmPassword]
    [Admin("payroll/pay-runs/statements/{id}/void", "PayrollPayRunsVoidStatement")]
    public async Task<IActionResult> VoidStatement(string id, string reason, [FromServices] PayStatementStore statements)
    {
        if (string.IsNullOrEmpty(id) || await statements.FindByIdAsync(id) is not { } statement)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.VoidPayStatements, statement.EmployerId))
        {
            return Forbid();
        }

        await NotifyAsync(await _service.VoidStatementAsync(statement, User, reason, HttpContext.RequestAborted), H["Statement {0} has been voided.", statement.Number]);

        return RedirectToAction(nameof(Statement), new { id });
    }

    /// <summary>
    /// Pays a payment of a final statement again, such as a lost check.
    /// </summary>
    /// <param name="id">The statement's id.</param>
    /// <param name="index">The payment's place.</param>
    /// <param name="reason">Why.</param>
    /// <param name="statements">The statements.</param>
    /// <returns>The statement page.</returns>
    [HttpPost]
    [ConfirmPassword]
    [Admin("payroll/pay-runs/statements/{id}/payments/{index}/reissue", "PayrollPayRunsReissuePayment")]
    public async Task<IActionResult> ReissuePayment(string id, int index, string reason, bool byCheck, [FromServices] PayStatementStore statements)
    {
        if (string.IsNullOrEmpty(id) || await statements.FindByIdAsync(id) is not { } statement)
        {
            return NotFound();
        }

        if (!await _scope.AuthorizeAsync(User, PayrollPermissions.VoidPayStatements, statement.EmployerId))
        {
            return Forbid();
        }

        var reissued = byCheck
            ? await _service.ReissuePaymentAsync(statement, index, User, reason, NetPayMethod.Check, HttpContext.RequestAborted)
            : await _service.ReissuePaymentAsync(statement, index, User, reason, HttpContext.RequestAborted);

        await NotifyAsync(reissued, H["The payment has been reissued."]);

        return RedirectToAction(nameof(Statement), new { id });
    }

    private async Task<PayRunDetailsViewModel> DetailsModelAsync(PayRun run)
    {
        var employer = await _employers.FindByIdAsync(run.EmployerId);
        var codes = (await _codes.GetAllAsync()).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
        var profiles = await _profiles.ListByEmployersAsync([run.EmployerId]);
        var staffNames = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var staffMemberId in profiles.Select(profile => profile.StaffMemberId).Distinct(StringComparer.Ordinal))
        {
            staffNames[staffMemberId] = await _names.StaffAsync(staffMemberId);
        }

        string NameOf(string staffMemberId)
            => staffMemberId is null ? null : staffNames.GetValueOrDefault(staffMemberId) ?? staffMemberId;

        var model = new PayRunDetailsViewModel
        {
            Run = run,
            EmployerName = employer?.Name ?? run.EmployerId,
            ScheduleName = string.IsNullOrEmpty(run.PayScheduleId) ? null : (await _schedules.FindByIdAsync(run.PayScheduleId))?.Name,
            Currency = employer?.Currency,
            CanPrepare = await _scope.AuthorizeAsync(User, PayrollPermissions.PreparePayRuns, run.EmployerId),
            CanProcess = await _scope.AuthorizeAsync(User, PayrollPermissions.ProcessPayRuns, run.EmployerId),
            Inputs = [.. (await _inputs.ListByRunAsync(run.ItemId)).OrderBy(input => NameOf(input.StaffMemberId), StringComparer.CurrentCultureIgnoreCase)
                .Select(input => (input, NameOf(input.StaffMemberId), codes.GetValueOrDefault(input.PayCodeId)?.Code ?? input.PayCodeId))],
            Issues = [.. run.Issues.Select(issue => (issue, NameOf(issue.StaffMemberId)))],
            StaffOptions = [.. staffNames.OrderBy(pair => pair.Value, StringComparer.CurrentCultureIgnoreCase).Select(pair => new SelectListItem(pair.Value, pair.Key))],
            CodeOptions = [.. codes.Values.Where(code => code.Enabled).OrderBy(code => code.Code, StringComparer.OrdinalIgnoreCase).Select(code => new SelectListItem($"{code.Code} · {code.Name}", code.ItemId))],
            Statements = new AdminListContext<PayStatement>
            {
                Source = [.. (await _service.ListStatementsAsync(run)).OrderBy(statement => statement.Snapshot?.EmployeeName, StringComparer.CurrentCultureIgnoreCase)],
            },
        };

        model.Questions = [.. ((AdminListContext<PayStatement>)model.Statements).Source
            .Where(statement => statement.Status == PayStatementStatus.Draft && statement.Review is { Status: PayStatementReviewStatus.Questioned } review && review.IsCurrentFor(statement.Net))
            .Select(statement => (NameOf(statement.StaffMemberId) ?? statement.Snapshot?.EmployeeName, statement.Review.Note))];

        if (!string.IsNullOrEmpty(run.ApprovalRequestId) && HttpContext.RequestServices.GetService<IApprovalRequestManager>() is { } requests
            && await requests.FindByIdAsync(run.ApprovalRequestId) is { } request)
        {
            model.ApprovalStatus = request.Status.ToString();
            model.ApprovalUrl = Url.Action("Decide", "ApprovalRequests", new { area = "CrestApps.OrchardCore.Approvals", id = request.ItemId });

            if (request.Status == CrestApps.OrchardCore.Approvals.Models.ApprovalStatus.Pending
                && await _scope.AuthorizeAsync(User, PayrollPermissions.ApprovePayRuns, run.EmployerId)
                && HttpContext.RequestServices.GetService<IApprovalService>() is { } approvals)
            {
                var eligibility = await approvals.GetEligibilityAsync(request, User, HttpContext.RequestAborted);

                model.CanDecide = eligibility.CanDecide;
                model.DecideRefusal = eligibility.Reason;
            }
        }

        return model;
    }

    private async Task RecalculateAsync(PayRun run, string staffMemberId)
    {
        // A run already calculated is calculated again for that person; an approval is for one calculation.
        if (run.Status != PayRunStatus.Draft)
        {
            var result = await _service.CalculateAsync(run, User, [staffMemberId], HttpContext.RequestAborted);

            foreach (var error in result.Errors)
            {
                await _notifier.WarningAsync(H["{0}", error]);
            }
        }
    }

    private async Task NotifyAsync(PayRunResult result, LocalizedHtmlString success)
    {
        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(success);

            return;
        }

        foreach (var error in result.Errors)
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }
    }

    private async Task<PayRun> FindAsync(string id)
        => string.IsNullOrEmpty(id) ? null : await _runs.FindByIdAsync(id);

    private static bool TryNumber(string text, out decimal? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
        {
            value = number;

            return true;
        }

        return false;
    }
}
