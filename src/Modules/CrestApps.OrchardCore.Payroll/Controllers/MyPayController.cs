using System.Security.Claims;
using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll.Controllers;

/// <summary>
/// My Pay: a person's own pay statements and stubs, and their tax elections and net pay split. Every page reads only the
/// records of the staff member linked to the signed-in user: someone else's statement or profile is not found.
/// </summary>
[Admin]
[Feature(PayrollConstants.Features.SelfService)]
public sealed class MyPayController : Controller
{
    private readonly IStaffDirectory _staff;
    private readonly IPayProfileManager _profiles;
    private readonly DefaultPayProfileService _recorder;
    private readonly PayStatementStore _statements;
    private readonly EmployerStore _employers;
    private readonly IPayCodeManager _codes;
    private readonly PayProfileEditTracker _tracker;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<EmployeePayProfile> _displayManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyPayController"/> class.
    /// </summary>
    /// <param name="staff">The staff directory.</param>
    /// <param name="profiles">The pay profiles.</param>
    /// <param name="recorder">Tells the profile observers about a change.</param>
    /// <param name="statements">The statements.</param>
    /// <param name="employers">The employers.</param>
    /// <param name="codes">The pay codes.</param>
    /// <param name="tracker">The changes the editor made.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The model updater.</param>
    /// <param name="displayManager">The profile editor.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MyPayController(
        IStaffDirectory staff,
        IPayProfileManager profiles,
        DefaultPayProfileService recorder,
        PayStatementStore statements,
        EmployerStore employers,
        IPayCodeManager codes,
        PayProfileEditTracker tracker,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<EmployeePayProfile> displayManager,
        INotifier notifier,
        IHtmlLocalizer<MyPayController> htmlLocalizer,
        IStringLocalizer<MyPayController> stringLocalizer)
    {
        _staff = staff;
        _profiles = profiles;
        _recorder = recorder;
        _statements = statements;
        _employers = employers;
        _codes = codes;
        _tracker = tracker;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// The person's statements, newest first, and their employers.
    /// </summary>
    /// <returns>The page.</returns>
    [Admin("my-pay", "PayrollMyPay")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, PayrollPermissions.ViewOwnPay))
        {
            return Forbid();
        }

        var me = await MeAsync();
        var model = new MyPayIndexViewModel { IsStaff = me is not null };

        if (me is null)
        {
            return View(model);
        }

        var meId = me.ItemId;
        var draft = nameof(PayStatementStatus.Draft);
        var statements = await _statements.ListAsync(index => index.StaffMemberId == meId && index.Status != draft, HttpContext.RequestAborted);
        var employers = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var employerId in statements.Select(statement => statement.EmployerId).Distinct(StringComparer.Ordinal))
        {
            employers[employerId] = (await _employers.FindByIdAsync(employerId))?.Name ?? employerId;
        }

        model.Statements = new AdminListContext<MyPayStatementRow>
        {
            Source = [.. statements
                .OrderByDescending(statement => statement.PayDate)
                .ThenByDescending(statement => statement.Number, StringComparer.Ordinal)
                .Select(statement => new MyPayStatementRow { Statement = statement, EmployerName = employers.GetValueOrDefault(statement.EmployerId) ?? statement.Snapshot?.EmployerName })],
        };

        foreach (var profile in await _profiles.ListByStaffMemberAsync(meId, HttpContext.RequestAborted))
        {
            model.Profiles.Add((profile, (await _employers.FindByIdAsync(profile.EmployerId))?.Name ?? profile.EmployerId));
        }

        model.CanManageDetails = await _authorizationService.AuthorizeAsync(User, PayrollPermissions.ManageOwnPayDetails);

        // Coming pay to review before the run is submitted, when employees review their pay.
        if (HttpContext.RequestServices.GetService<PayStatementReviewService>() is { } reviews)
        {
            foreach (var (statement, _) in await reviews.ListOpenAsync(meId, HttpContext.RequestAborted))
            {
                model.ToReview.Add((statement, (await _employers.FindByIdAsync(statement.EmployerId))?.Name ?? statement.Snapshot?.EmployerName));
            }
        }

        // The person's filed tax forms, such as wage statements.
        if (HttpContext.RequestServices.GetService<IPayrollFilingManager>() is { } filings && model.Profiles.Count > 0)
        {
            var forms = (await filings.ListByEmployersAsync([.. model.Profiles.Select(pair => pair.Profile.EmployerId).Distinct(StringComparer.Ordinal)], HttpContext.RequestAborted))
                .Where(filing => filing.Status == PayrollFilingStatus.Filed)
                .SelectMany(filing => filing.People.Where(person => person.StaffMemberId == meId).Select(person => new MyPayTaxFormViewModel
                {
                    Filing = filing,
                    Person = person,
                    EmployerName = model.Profiles.FirstOrDefault(pair => pair.Profile.EmployerId == filing.EmployerId).EmployerName,
                }))
                .OrderByDescending(form => form.Filing.Year)
                .ToList();

            model.HasTaxForms = forms.Count > 0;
            model.TaxForms = new AdminListContext<MyPayTaxFormViewModel> { Source = forms };
        }

        model.ComingPay = new AdminListContext<MyPayComingPayRow> { Source = [.. model.ToReview.Select(pair => new MyPayComingPayRow { Statement = pair.Statement, EmployerName = pair.EmployerName })] };
        model.PayDetails = new AdminListContext<MyPayProfileRow> { Source = [.. model.Profiles.Select(pair => new MyPayProfileRow { Profile = pair.Profile, EmployerName = pair.EmployerName, CanManageDetails = model.CanManageDetails })] };

        // Banking keeps the person's own bank accounts: link to it when it is on.
        model.CanManageBankAccounts = HttpContext.RequestServices.GetService<IBankAccountManager>() is not null
            && await _authorizationService.AuthorizeAsync(User, BankingPermissions.ManageOwnBankAccounts);

        return View(model);
    }

    /// <summary>
    /// One of the person's statements.
    /// </summary>
    /// <param name="id">The statement's id.</param>
    /// <returns>The page.</returns>
    [Admin("my-pay/statements/{id}", "PayrollMyPayStatement")]
    public async Task<IActionResult> Statement(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PayrollPermissions.ViewOwnPay))
        {
            return Forbid();
        }

        if (await OwnStatementAsync(id) is not { } statement)
        {
            return NotFound();
        }

        return View(new PayStatementViewModel
        {
            Statement = statement,
            Codes = (await _codes.GetAllAsync()).ToDictionary(code => code.ItemId, code => code.Code, StringComparer.Ordinal),
        });
    }

    /// <summary>
    /// The stub of one of the person's statements, as a PDF.
    /// </summary>
    /// <param name="id">The statement's id.</param>
    /// <param name="stubs">Writes the stub.</param>
    /// <returns>The file.</returns>
    [Admin("my-pay/statements/{id}/pdf", "PayrollMyPayStatementPdf")]
    public async Task<IActionResult> StatementPdf(string id, [FromServices] PayStubService stubs)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PayrollPermissions.ViewOwnPay))
        {
            return Forbid();
        }

        if (await OwnStatementAsync(id) is not { } statement)
        {
            return NotFound();
        }

        return File(await stubs.WriteAsync(statement, HttpContext.RequestAborted), "application/pdf", PayStubService.FileName(statement));
    }

    /// <summary>
    /// The person's tax elections and net pay split with one employer.
    /// </summary>
    /// <param name="id">The pay profile's id.</param>
    /// <returns>The page.</returns>
    /// <summary>
    /// One of the person's filed tax forms, as a PDF.
    /// </summary>
    /// <param name="id">The filing's id.</param>
    /// <param name="documents">The documents.</param>
    /// <returns>The PDF.</returns>
    [Admin("my-pay/tax-forms/{id}", "PayrollMyPayTaxForm")]
    public async Task<IActionResult> TaxForm(string id, [FromServices] PayrollTaxFormDocuments documents)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PayrollPermissions.ViewOwnPay))
        {
            return Forbid();
        }

        var me = await MeAsync();

        // Only a filed form, and only the person's own statement in it.
        if (me is null || string.IsNullOrEmpty(id)
            || HttpContext.RequestServices.GetService<IPayrollFilingManager>() is not { } filings
            || await filings.FindByIdAsync(id) is not { Status: PayrollFilingStatus.Filed } filing
            || filing.People.FirstOrDefault(person => person.StaffMemberId == me.ItemId) is not { } person)
        {
            return NotFound();
        }

        return File(await documents.WriteAsync(filing, person), "application/pdf", PayrollTaxFormDocuments.FileName(filing));
    }

    [Admin("my-pay/details/{id}", "PayrollMyPayDetails")]
    public async Task<IActionResult> Details(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PayrollPermissions.ManageOwnPayDetails))
        {
            return Forbid();
        }

        if (await OwnProfileAsync(id) is not { } profile)
        {
            return NotFound();
        }

        return View(await DetailsModelAsync(profile, await _displayManager.BuildEditorAsync(profile, _updateModelAccessor.ModelUpdater, isNew: false, PayrollConstants.MyPayEditorGroup)));
    }

    /// <summary>
    /// Saves the person's tax elections and net pay split with one employer.
    /// </summary>
    /// <param name="id">The pay profile's id.</param>
    /// <returns>The page, or My Pay.</returns>
    [HttpPost]
    [ActionName(nameof(Details))]
    [Admin("my-pay/details/{id}", "PayrollMyPayDetails")]
    public async Task<IActionResult> DetailsPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PayrollPermissions.ManageOwnPayDetails))
        {
            return Forbid();
        }

        if (await OwnProfileAsync(id) is not { } profile)
        {
            return NotFound();
        }

        var editor = await _displayManager.UpdateEditorAsync(profile, _updateModelAccessor.ModelUpdater, isNew: false, PayrollConstants.MyPayEditorGroup);

        if (await CatalogEntryValidation.ValidateAsync(_profiles, profile, _updateModelAccessor.ModelUpdater, string.Empty) && ModelState.IsValid)
        {
            await _profiles.UpdateAsync(profile);

            foreach (var kind in _tracker.Changes.Distinct())
            {
                await _recorder.TellAsync(profile, kind, S["Changed in My Pay"], HttpContext.RequestAborted);
            }

            await _notifier.SuccessAsync(H["Your pay details have been saved."]);

            return RedirectToAction(nameof(Index));
        }

        return View(await DetailsModelAsync(profile, editor));
    }

    private async Task<MyPayDetailsViewModel> DetailsModelAsync(EmployeePayProfile profile, IShape editor)
        => new()
        {
            EmployerName = (await _employers.FindByIdAsync(profile.EmployerId))?.Name ?? profile.EmployerId,
            Editor = editor,
        };

    /// <summary>
    /// The person's coming pay, to review before the run is submitted.
    /// </summary>
    /// <param name="id">The draft statement's id.</param>
    /// <param name="reviews">The reviews.</param>
    /// <returns>The page.</returns>
    [Admin("my-pay/review/{id}", "PayrollMyPayReview")]
    public async Task<IActionResult> Review(string id, [FromServices] PayStatementReviewService reviews)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PayrollPermissions.ViewOwnPay))
        {
            return Forbid();
        }

        if (await OwnDraftAsync(id, reviews) is not { } open)
        {
            return NotFound();
        }

        return View(await ReviewModelAsync(open.Statement, open.Run, null));
    }

    /// <summary>
    /// Records the person's answer about their coming pay.
    /// </summary>
    /// <param name="id">The draft statement's id.</param>
    /// <param name="confirmed"><see langword="true"/> when it is right.</param>
    /// <param name="note">What the person asks.</param>
    /// <param name="reviews">The reviews.</param>
    /// <returns>My Pay, or the page again with the reason.</returns>
    [HttpPost]
    [ActionName(nameof(Review))]
    [Admin("my-pay/review/{id}", "PayrollMyPayReviewPost")]
    public async Task<IActionResult> ReviewPost(string id, bool confirmed, string note, [FromServices] PayStatementReviewService reviews)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PayrollPermissions.ViewOwnPay))
        {
            return Forbid();
        }

        if (await OwnDraftAsync(id, reviews) is not { } open)
        {
            return NotFound();
        }

        var result = await reviews.ReviewAsync(open.Statement, User, confirmed, note, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(await ReviewModelAsync(open.Statement, open.Run, note));
        }

        await _notifier.SuccessAsync(confirmed ? H["Thanks: payroll knows your pay looks right."] : H["Thanks: payroll was told about your question before the run is approved."]);

        return RedirectToAction(nameof(Index));
    }

    // The person's draft statement of a run that takes reviews, or null.
    private async Task<(PayStatement Statement, PayRun Run)?> OwnDraftAsync(string id, PayStatementReviewService reviews)
    {
        if (string.IsNullOrEmpty(id) || await MeAsync() is not { } me)
        {
            return null;
        }

        foreach (var open in await reviews.ListOpenAsync(me.ItemId, HttpContext.RequestAborted))
        {
            if (open.Statement.ItemId == id)
            {
                return open;
            }
        }

        return null;
    }

    private async Task<MyPayReviewViewModel> ReviewModelAsync(PayStatement statement, PayRun run, string note)
        => new()
        {
            Statement = new PayStatementViewModel
            {
                Statement = statement,
                Run = run,
                Codes = (await _codes.GetAllAsync()).ToDictionary(code => code.ItemId, code => code.Code, StringComparer.Ordinal),
            },
            EmployerName = (await _employers.FindByIdAsync(statement.EmployerId))?.Name ?? statement.Snapshot?.EmployerName,
            Review = statement.Review,
            IsCurrent = statement.Review?.IsCurrentFor(statement.Net) == true,
            Note = note ?? (statement.Review?.Status == PayStatementReviewStatus.Questioned ? statement.Review.Note : null),
        };

    // The staff member linked to the signed-in user, or null.
    private async Task<StaffMember> MeAsync()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } userId ? await _staff.FindByUserIdAsync(userId, HttpContext.RequestAborted) : null;

    // A statement of the person that is not a draft, or null: someone else's is never found.
    private async Task<PayStatement> OwnStatementAsync(string id)
        => !string.IsNullOrEmpty(id) && await MeAsync() is { } me && await _statements.FindByIdAsync(id, HttpContext.RequestAborted) is { } statement
            && statement.StaffMemberId == me.ItemId && statement.Status != PayStatementStatus.Draft
                ? statement
                : null;

    // A pay profile of the person, or null.
    private async Task<EmployeePayProfile> OwnProfileAsync(string id)
        => !string.IsNullOrEmpty(id) && await MeAsync() is { } me && await _profiles.FindByIdAsync(id, HttpContext.RequestAborted) is { } profile
            && profile.StaffMemberId == me.ItemId
                ? profile
                : null;
}
