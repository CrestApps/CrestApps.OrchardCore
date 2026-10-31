using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The activity codes: the list and the editor.
/// </summary>
public sealed class ActivityCodesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IActivityCodeManager _activityCodeManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityCodesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="activityCodeManager">The activity code manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ActivityCodesController(
        IAuthorizationService authorizationService,
        IActivityCodeManager activityCodeManager,
        INotifier notifier,
        IHtmlLocalizer<ActivityCodesController> htmlLocalizer,
        IStringLocalizer<ActivityCodesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _activityCodeManager = activityCodeManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the activity codes.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/activity-codes", "ActivityCodes")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageActivityCodes))
        {
            return Forbid();
        }

        var codes = (await _activityCodeManager.GetAllAsync(HttpContext.RequestAborted)).OrderBy(code => code.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new WorkforceListViewModel
        {
            List = new AdminListContext<ActivityCode>
            {
                Source = codes.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, codes.Count),
            },
        });
    }

    /// <summary>
    /// Shows the editor of a new or existing activity code.
    /// </summary>
    /// <param name="id">The code id, or empty for a new code.</param>
    /// <returns>The editor.</returns>
    [Admin("workforce/activity-codes/edit/{id?}", "ActivityCodesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageActivityCodes))
        {
            return Forbid();
        }

        var code = string.IsNullOrEmpty(id)
            ? await _activityCodeManager.NewAsync()
            : await _activityCodeManager.FindByIdAsync(id);

        if (code is null)
        {
            return NotFound();
        }

        var model = new ActivityCodeEditViewModel
        {
            Id = id,
            Name = code.Name,
            Code = code.Code,
            Kind = code.Kind.ToString(),
            Color = code.Color,
            IsPaid = code.IsPaid,
            CountsAsWork = code.CountsAsWork,
            CountsTowardCoverage = code.CountsTowardCoverage,
            Enabled = code.Enabled,
        };

        model.KindOptions = KindOptions(model.Kind);

        return View(model);
    }

    /// <summary>
    /// Saves an activity code.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/activity-codes/edit/{id?}", "ActivityCodesEdit")]
    public async Task<IActionResult> EditPost(ActivityCodeEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageActivityCodes))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var code = isNew
            ? await _activityCodeManager.NewAsync()
            : await _activityCodeManager.FindByIdAsync(model.Id);

        if (code is null)
        {
            return NotFound();
        }

        code.Name = model.Name;
        code.Code = model.Code;
        code.Kind = Enum.TryParse<ActivityKind>(model.Kind, out var kind) ? kind : ActivityKind.Work;
        code.Color = string.IsNullOrWhiteSpace(model.Color) ? null : model.Color.Trim();
        code.IsPaid = model.IsPaid;
        code.CountsAsWork = model.CountsAsWork;
        code.CountsTowardCoverage = model.CountsTowardCoverage;
        code.Enabled = model.Enabled;

        var validation = await _activityCodeManager.ValidateAsync(code);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            model.KindOptions = KindOptions(model.Kind);

            return View(model);
        }

        if (isNew)
        {
            await _activityCodeManager.CreateAsync(code);
        }
        else
        {
            await _activityCodeManager.UpdateAsync(code);
        }

        await _notifier.SuccessAsync(H["The activity code has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes an activity code.
    /// </summary>
    /// <param name="id">The code id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/activity-codes/delete/{id}", "ActivityCodesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageActivityCodes))
        {
            return Forbid();
        }

        var code = await _activityCodeManager.FindByIdAsync(id);

        if (code is not null)
        {
            await _activityCodeManager.DeleteAsync(code);
            await _notifier.SuccessAsync(H["The activity code has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private IList<SelectListItem> KindOptions(string selected)
    {
        return
        [
            new SelectListItem(S["Work"], nameof(ActivityKind.Work), selected == nameof(ActivityKind.Work)),
            new SelectListItem(S["Break"], nameof(ActivityKind.Break), selected == nameof(ActivityKind.Break)),
            new SelectListItem(S["Meal"], nameof(ActivityKind.Meal), selected == nameof(ActivityKind.Meal)),
            new SelectListItem(S["Meeting"], nameof(ActivityKind.Meeting), selected == nameof(ActivityKind.Meeting)),
            new SelectListItem(S["Training"], nameof(ActivityKind.Training), selected == nameof(ActivityKind.Training)),
            new SelectListItem(S["Coaching"], nameof(ActivityKind.Coaching), selected == nameof(ActivityKind.Coaching)),
            new SelectListItem(S["Offline work"], nameof(ActivityKind.OfflineWork), selected == nameof(ActivityKind.OfflineWork)),
            new SelectListItem(S["Travel"], nameof(ActivityKind.Travel), selected == nameof(ActivityKind.Travel)),
            new SelectListItem(S["On call"], nameof(ActivityKind.OnCall), selected == nameof(ActivityKind.OnCall)),
            new SelectListItem(S["Time off"], nameof(ActivityKind.TimeOff), selected == nameof(ActivityKind.TimeOff)),
            new SelectListItem(S["Other"], nameof(ActivityKind.Other), selected == nameof(ActivityKind.Other)),
        ];
    }
}
