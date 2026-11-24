using CrestApps.OrchardCore.Learning.Certifications.Core;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Certifications.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Learning.Certifications.Controllers;

/// <summary>
/// The education credit types, such as CEU, CPD hours or PDU.
/// </summary>
public sealed class EducationCreditTypesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IEducationCreditService _credits;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public EducationCreditTypesController(IAuthorizationService authorizationService, IEducationCreditService credits, INotifier notifier, IHtmlLocalizer<EducationCreditTypesController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _credits = credits;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [HttpGet]
    [Admin("learning/credit-types", "LearningCreditTypes")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningCertificationsPermissions.ManageEducationCredits))
        {
            return Forbid();
        }

        return View(new EducationCreditTypeListViewModel
        {
            List = new AdminListContext<EducationCreditType> { Source = (await _credits.ListTypesAsync(HttpContext.RequestAborted)).ToList() },
        });
    }

    [HttpGet]
    [Admin("learning/credit-types/edit/{id?}", "LearningCreditTypesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningCertificationsPermissions.ManageEducationCredits))
        {
            return Forbid();
        }

        var model = new EducationCreditTypeEditViewModel();

        if (!string.IsNullOrEmpty(id))
        {
            var type = await _credits.FindTypeAsync(id, HttpContext.RequestAborted);

            if (type is null)
            {
                return NotFound();
            }

            model = new EducationCreditTypeEditViewModel { Id = type.ItemId, Name = type.Name, Unit = type.Unit, Description = type.Description, AllowExternalLearning = type.AllowExternalLearning };
        }

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("learning/credit-types/edit/{id?}", "LearningCreditTypesEdit")]
    public async Task<IActionResult> EditPost(EducationCreditTypeEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningCertificationsPermissions.ManageEducationCredits))
        {
            return Forbid();
        }

        var type = string.IsNullOrEmpty(model.Id) ? new EducationCreditType() : await _credits.FindTypeAsync(model.Id, HttpContext.RequestAborted);

        if (type is null)
        {
            return NotFound();
        }

        type.Name = model.Name;
        type.Unit = model.Unit;
        type.Description = model.Description?.Trim();
        type.AllowExternalLearning = model.AllowExternalLearning;

        var errors = await _credits.SaveTypeAsync(type, HttpContext.RequestAborted);

        foreach (var error in errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        if (errors.Count > 0)
        {
            return View(model);
        }

        await _notifier.SuccessAsync(H["The credit type has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Admin("learning/credit-types/delete/{id}", "LearningCreditTypesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningCertificationsPermissions.ManageEducationCredits))
        {
            return Forbid();
        }

        if (!await _credits.DeleteTypeAsync(id, HttpContext.RequestAborted))
        {
            return NotFound();
        }

        await _notifier.SuccessAsync(H["The credit type has been deleted. Credits already earned stay."]);

        return RedirectToAction(nameof(Index));
    }
}
