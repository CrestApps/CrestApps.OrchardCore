using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Skills.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Skills.Controllers;

/// <summary>
/// The certificates people hold: the list with its filters, the editor, checking and withdrawing.
/// </summary>
public sealed class CertificationRecordsController : Controller
{
    private const int ExpiringDays = 60;

    private readonly IAuthorizationService _authorizationService;
    private readonly ICertificationRecordService _recordService;
    private readonly ICertificationManager _certificationManager;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationRecordsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="recordService">The certification record service.</param>
    /// <param name="certificationManager">The certification manager.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="displayNameProvider">The display name provider.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CertificationRecordsController(
        IAuthorizationService authorizationService,
        ICertificationRecordService recordService,
        ICertificationManager certificationManager,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<CertificationRecordsController> htmlLocalizer,
        IStringLocalizer<CertificationRecordsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _recordService = recordService;
        _certificationManager = certificationManager;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists certificates.
    /// </summary>
    /// <param name="model">The filters.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="siteService">The site service.</param>
    /// <returns>The list.</returns>
    [Admin("certificates", "CertificationRecords")]
    public async Task<IActionResult> Index(
        SkillsListViewModel model,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory,
        [FromServices] ISiteService siteService)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.ViewSkills))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var query = new CertificationRecordQuery
        {
            Search = model.Search,
            CertificationId = string.IsNullOrEmpty(model.CertificationId) ? null : model.CertificationId,
            Status = Enum.TryParse<CertificationStatus>(model.Status, out var status) ? status : null,
        };

        if (model.Expiring)
        {
            var today = await TodayAsync(siteService);

            query.ExpiresOnOrBefore = today.AddDays(ExpiringDays);
            query.Status ??= CertificationStatus.Valid;
        }

        var (count, records) = await _recordService.PageAsync(query, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        model.List = new AdminListContext<CertificationRecord>
        {
            Source = records.ToList(),
            Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary
            {
                { "search", model.Search },
                { "certificationId", model.CertificationId },
                { "status", model.Status },
                { "expiring", model.Expiring },
            }),
        };

        model.CertificationOptions = await CertificationOptionsAsync(model.CertificationId);
        model.StatusOptions = StatusOptions(model.Status);

        return View(model);
    }

    /// <summary>
    /// Shows the editor of a new or existing certificate.
    /// </summary>
    /// <param name="id">The record id, or empty for a new one.</param>
    /// <param name="userId">The user a new certificate belongs to.</param>
    /// <returns>The editor.</returns>
    [Admin("certificates/edit/{id?}", "CertificationRecordsEdit")]
    public async Task<IActionResult> Edit(string id, string userId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.AssignSkills))
        {
            return Forbid();
        }

        var record = string.IsNullOrEmpty(id)
            ? new CertificationRecord { SubjectKind = SkillSubjectKind.User, SubjectId = userId }
            : await _recordService.FindByIdAsync(id);

        if (record is null)
        {
            return NotFound();
        }

        var model = new CertificationRecordEditViewModel
        {
            Id = record.ItemId,
            UserId = record.SubjectKind == SkillSubjectKind.User ? record.SubjectId : null,
            HeldByWorker = record.SubjectKind == SkillSubjectKind.Worker,
            SubjectName = record.SubjectName,
            CertificationId = record.CertificationId,
            Number = record.Number,
            IssuedOn = record.IssuedOn,
            ExpiresOn = record.ExpiresOn,
            DocumentPath = record.DocumentPath,
            Status = record.Status.ToString(),
            Notes = record.Notes,
        };

        await FillAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves a certificate.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("certificates/edit/{id?}", "CertificationRecordsEdit")]
    public async Task<IActionResult> EditPost(CertificationRecordEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.AssignSkills))
        {
            return Forbid();
        }

        var record = string.IsNullOrEmpty(model.Id)
            ? new CertificationRecord { SubjectKind = SkillSubjectKind.User }
            : await _recordService.FindByIdAsync(model.Id);

        if (record is null)
        {
            return NotFound();
        }

        if (record.SubjectKind == SkillSubjectKind.User)
        {
            var user = string.IsNullOrWhiteSpace(model.UserId) ? null : await _userManager.FindByIdAsync(model.UserId);

            if (user is null)
            {
                ModelState.AddModelError(nameof(model.UserId), S["Choose who holds the certificate."]);
            }
            else
            {
                record.SubjectId = model.UserId;
                record.SubjectName = await _displayNameProvider.GetAsync(user, HttpContext.RequestAborted);
            }
        }

        var newStatus = Enum.TryParse<CertificationStatus>(model.Status, out var parsed) ? parsed : CertificationStatus.Pending;

        if (newStatus == CertificationStatus.Valid && record.Status != CertificationStatus.Valid)
        {
            record.VerifiedBy = User.Identity?.Name;
            record.VerifiedUtc = _clock.UtcNow;
        }

        record.CertificationId = model.CertificationId;
        record.Number = model.Number;
        record.IssuedOn = model.IssuedOn;
        record.ExpiresOn = model.ExpiresOn;
        record.DocumentPath = string.IsNullOrWhiteSpace(model.DocumentPath) ? null : model.DocumentPath.Trim();
        record.Status = newStatus;
        record.Notes = model.Notes;

        if (ModelState.IsValid)
        {
            var result = await _recordService.SaveAsync(record, User.Identity?.Name, HttpContext.RequestAborted);

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        if (!ModelState.IsValid)
        {
            model.HeldByWorker = record.SubjectKind == SkillSubjectKind.Worker;
            model.SubjectName = record.SubjectName;
            await FillAsync(model);

            return View(model);
        }

        await _notifier.SuccessAsync(H["The certificate has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Marks a certificate as checked.
    /// </summary>
    /// <param name="id">The record id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("certificates/verify/{id}", "CertificationRecordsVerify")]
    public async Task<IActionResult> Verify(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.AssignSkills))
        {
            return Forbid();
        }

        var result = await _recordService.VerifyAsync(id, User.Identity?.Name, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The certificate is marked as checked."]);
        }
        else
        {
            foreach (var error in result.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Withdraws a certificate.
    /// </summary>
    /// <param name="id">The record id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("certificates/revoke/{id}", "CertificationRecordsRevoke")]
    public async Task<IActionResult> Revoke(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.AssignSkills))
        {
            return Forbid();
        }

        var result = await _recordService.RevokeAsync(id, User.Identity?.Name, null, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The certificate has been withdrawn."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a certificate.
    /// </summary>
    /// <param name="id">The record id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("certificates/delete/{id}", "CertificationRecordsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.AssignSkills))
        {
            return Forbid();
        }

        if (await _recordService.DeleteAsync(id, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["The certificate has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task FillAsync(CertificationRecordEditViewModel model)
    {
        model.CertificationOptions = await CertificationOptionsAsync(model.CertificationId);
        model.StatusOptions = StatusOptions(model.Status);
    }

    private async Task<IList<SelectListItem>> CertificationOptionsAsync(string selected)
    {
        return (await _certificationManager.GetEnabledAsync(HttpContext.RequestAborted))
            .Select(certification => new SelectListItem(certification.Name, certification.ItemId, certification.ItemId == selected))
            .ToList();
    }

    private IList<SelectListItem> StatusOptions(string selected)
    {
        return
        [
            new SelectListItem(S["Pending check"], nameof(CertificationStatus.Pending), selected == nameof(CertificationStatus.Pending)),
            new SelectListItem(S["Valid"], nameof(CertificationStatus.Valid), selected == nameof(CertificationStatus.Valid)),
            new SelectListItem(S["Expired"], nameof(CertificationStatus.Expired), selected == nameof(CertificationStatus.Expired)),
            new SelectListItem(S["Withdrawn"], nameof(CertificationStatus.Revoked), selected == nameof(CertificationStatus.Revoked)),
        ];
    }

    private async Task<DateOnly> TodayAsync(ISiteService siteService)
    {
        var site = await siteService.GetSiteSettingsAsync();
        var zone = _clock.GetTimeZone(site.TimeZoneId);

        return DateOnly.FromDateTime(_clock.ConvertToTimeZone(_clock.UtcNow, zone).DateTime);
    }
}
