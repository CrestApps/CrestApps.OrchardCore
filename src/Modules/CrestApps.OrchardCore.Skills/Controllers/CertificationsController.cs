using System.Globalization;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Skills.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Skills.Controllers;

/// <summary>
/// The certification catalog: the list and the editor.
/// </summary>
public sealed class CertificationsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICertificationManager _certificationManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="certificationManager">The certification manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CertificationsController(
        IAuthorizationService authorizationService,
        ICertificationManager certificationManager,
        INotifier notifier,
        IHtmlLocalizer<CertificationsController> htmlLocalizer,
        IStringLocalizer<CertificationsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _certificationManager = certificationManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the certifications.
    /// </summary>
    /// <param name="search">The text the name must contain.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("certifications", "Certifications")]
    public async Task<IActionResult> Index(string search, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.ManageSkills))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, certifications) = await _certificationManager.SearchAsync(search, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        return View(new SkillsListViewModel
        {
            Search = search,
            List = new AdminListContext<Certification>
            {
                Source = certifications.ToList(),
                Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { { "search", search } }),
            },
        });
    }

    /// <summary>
    /// Shows the editor of a new or existing certification.
    /// </summary>
    /// <param name="id">The certification id, or empty for a new one.</param>
    /// <returns>The editor.</returns>
    [Admin("certifications/edit/{id?}", "CertificationsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.ManageSkills))
        {
            return Forbid();
        }

        var certification = string.IsNullOrEmpty(id)
            ? await _certificationManager.NewAsync()
            : await _certificationManager.FindByIdAsync(id);

        if (certification is null)
        {
            return NotFound();
        }

        return View(new CertificationEditViewModel
        {
            Id = id,
            Name = certification.Name,
            Issuer = certification.Issuer,
            Description = certification.Description,
            ValidityMonths = certification.ValidityMonths,
            RequiresDocument = certification.RequiresDocument,
            ReminderDays = string.Join(", ", certification.ReminderDays),
            Enabled = certification.Enabled,
        });
    }

    /// <summary>
    /// Saves a certification.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("certifications/edit/{id?}", "CertificationsEdit")]
    public async Task<IActionResult> EditPost(CertificationEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.ManageSkills))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var certification = isNew
            ? await _certificationManager.NewAsync()
            : await _certificationManager.FindByIdAsync(model.Id);

        if (certification is null)
        {
            return NotFound();
        }

        var reminderDays = new List<int>();

        foreach (var part in (model.ReminderDays ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days))
            {
                reminderDays.Add(days);
            }
            else
            {
                ModelState.AddModelError(nameof(model.ReminderDays), S["'{0}' is not a number of days.", part]);
            }
        }

        certification.Name = model.Name;
        certification.Issuer = model.Issuer;
        certification.Description = model.Description;
        certification.ValidityMonths = model.ValidityMonths;
        certification.RequiresDocument = model.RequiresDocument;
        certification.ReminderDays = reminderDays;
        certification.Enabled = model.Enabled;

        var validation = await _certificationManager.ValidateAsync(certification);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (isNew)
        {
            await _certificationManager.CreateAsync(certification);
        }
        else
        {
            await _certificationManager.UpdateAsync(certification);
        }

        await _notifier.SuccessAsync(H["The certification has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a certification. Its certificates are kept but no longer count.
    /// </summary>
    /// <param name="id">The certification id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("certifications/delete/{id}", "CertificationsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.ManageSkills))
        {
            return Forbid();
        }

        var certification = await _certificationManager.FindByIdAsync(id);

        if (certification is not null)
        {
            await _certificationManager.DeleteAsync(certification);
            await _notifier.SuccessAsync(H["The certification has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
