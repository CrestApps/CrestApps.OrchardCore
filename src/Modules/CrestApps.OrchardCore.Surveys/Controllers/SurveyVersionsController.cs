using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.Controllers;

/// <summary>
/// The versions of a survey: the list, a read-only view, comparisons and restoring a version into the draft.
/// </summary>
[Admin]
public sealed class SurveyVersionsController : Controller
{
    private readonly ISurveyManager _manager;
    private readonly ISurveyVersionCache _versionCache;
    private readonly ISurveyPublisher _publisher;
    private readonly ISurveyDefinitionDiffer _differ;
    private readonly ISurveyAuthorizationService _surveyAuthorization;
    private readonly ISurveyControlRegistry _registry;
    private readonly INotifier _notifier;

    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyVersionsController"/> class.
    /// </summary>
    public SurveyVersionsController(
        ISurveyManager manager,
        ISurveyVersionCache versionCache,
        ISurveyPublisher publisher,
        ISurveyDefinitionDiffer differ,
        ISurveyAuthorizationService surveyAuthorization,
        ISurveyControlRegistry registry,
        INotifier notifier,
        IStringLocalizerFactory localizerFactory,
        IHtmlLocalizer<SurveyVersionsController> htmlLocalizer,
        IStringLocalizer<SurveyVersionsController> stringLocalizer)
    {
        _manager = manager;
        _versionCache = versionCache;
        _publisher = publisher;
        _differ = differ;
        _surveyAuthorization = surveyAuthorization;
        _registry = registry;
        _notifier = notifier;
        _localizerFactory = localizerFactory;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the versions of a survey.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/{id}/versions", "SurveysVersions")]
    public async Task<IActionResult> Index(string id)
    {
        var survey = await AuthorizeAsync(id, SurveyOperation.Manage);

        if (survey is null)
        {
            return Forbid();
        }

        return View(new SurveyVersionsViewModel
        {
            Survey = survey,
            List = new AdminListContext<SurveyVersion> { Source = await _versionCache.ListAsync(id, HttpContext.RequestAborted) },
        });
    }

    /// <summary>
    /// Shows a version, or the draft, read-only.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="versionId">The version, or draft.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/{id}/versions/{versionId}", "SurveysVersion")]
    public async Task<IActionResult> Display(string id, string versionId)
    {
        var survey = await AuthorizeAsync(id, SurveyOperation.Manage);

        if (survey is null)
        {
            return Forbid();
        }

        var version = versionId == "draft" ? null : await _versionCache.GetAsync(versionId, HttpContext.RequestAborted);

        if (versionId != "draft" && (version is null || version.SurveyId != id))
        {
            return NotFound();
        }

        return View(new SurveyVersionViewModel
        {
            Survey = survey,
            Version = version,
            Definition = version?.Definition ?? survey.Draft,
            ControlNames = _registry.GetControls().ToDictionary(descriptor => descriptor.Name, descriptor => Localized(descriptor.DisplayName) ?? descriptor.Name, StringComparer.OrdinalIgnoreCase),
        });
    }

    /// <summary>
    /// Compares two versions, or a version and the draft.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="from">The older version.</param>
    /// <param name="to">The newer version, or draft.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/{id}/compare", "SurveysCompare")]
    public async Task<IActionResult> Compare(string id, string from, string to = "draft")
    {
        var survey = await AuthorizeAsync(id, SurveyOperation.Manage);

        if (survey is null)
        {
            return Forbid();
        }

        var older = await _versionCache.GetAsync(from, HttpContext.RequestAborted);
        var newer = to == "draft" ? null : await _versionCache.GetAsync(to, HttpContext.RequestAborted);

        if (older is null || older.SurveyId != id || (to != "draft" && (newer is null || newer.SurveyId != id)))
        {
            return NotFound();
        }

        return View(new SurveyCompareViewModel
        {
            Survey = survey,
            FromLabel = S["Version {0}", older.Number],
            ToLabel = newer is null ? S["Draft"] : S["Version {0}", newer.Number],
            Diff = _differ.Compare(older.Definition, newer?.Definition ?? survey.Draft),
        });
    }

    /// <summary>
    /// Copies a version into the draft.
    /// </summary>
    /// <param name="id">The survey.</param>
    /// <param name="versionId">The version.</param>
    /// <param name="auditTrail">The audit trail.</param>
    /// <returns>A redirect to the designer.</returns>
    [HttpPost]
    [Admin("surveys/{id}/versions/{versionId}/restore", "SurveysVersionRestore")]
    public async Task<IActionResult> Restore(string id, string versionId, [FromServices] ISurveyAuditTrail auditTrail)
    {
        var survey = await AuthorizeAsync(id, SurveyOperation.Publish);

        if (survey is null)
        {
            return Forbid();
        }

        var result = await _publisher.RestoreAsync(id, versionId, HttpContext.RequestAborted);

        if (result == SurveyErrorCode.None)
        {
            var version = await _versionCache.GetAsync(versionId, HttpContext.RequestAborted);

            await auditTrail.RecordAsync(SurveyAuditEvents.SurveyRestored, new SurveyAuditEntry
            {
                ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                SurveyId = survey.ItemId,
                SurveyName = survey.Name,
                VersionNumber = version?.Number,
            }, HttpContext.RequestAborted);

            await _notifier.SuccessAsync(H["The version has been copied into the draft. Publish the draft to use it."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The version could not be restored."]);
        }

        return RedirectToAction("Design", "SurveyDesigner", new { id });
    }

    private async Task<Survey> AuthorizeAsync(string id, SurveyOperation operation)
    {
        var survey = string.IsNullOrEmpty(id) ? null : await _manager.FindByIdAsync(id);

        return survey is not null && await _surveyAuthorization.AuthorizeAsync(User, survey, operation, HttpContext.RequestAborted) ? survey : null;
    }

    private string Localized(LocalizationSource source)
        => source is null ? null : _localizerFactory.Localize(source).Value;
}
