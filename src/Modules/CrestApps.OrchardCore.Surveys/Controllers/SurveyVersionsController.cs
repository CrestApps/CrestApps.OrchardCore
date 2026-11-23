using System.Security.Claims;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Localization;
using OrchardCore.Navigation;

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
    public async Task<IActionResult> Index(
        string id,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory,
        [FromServices] SurveyVersionStore versionStore)
    {
        var survey = await AuthorizeAsync(id, SurveyOperation.Manage);

        if (survey is null)
        {
            return Forbid();
        }

        // A page of the version index: the rows show the number, date, publisher and notes the index holds, so no
        // version (and its whole definition) is loaded. A row indexed before those columns existed is read from its
        // version until the reindex step reaches it.
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, rows) = await versionStore.PageRowsAsync(id, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var versions = new List<SurveyVersion>();

        foreach (var row in rows)
        {
            versions.Add(row.PublishedBy is null
                ? await _versionCache.GetAsync(row.ItemId, HttpContext.RequestAborted) ?? SummaryOf(row)
                : SummaryOf(row));
        }

        return View(new SurveyVersionsViewModel
        {
            Survey = survey,
            List = new AdminListContext<SurveyVersion>
            {
                Source = versions,
                Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { { "id", id } }),
            },
        });
    }

    // The fields of a version the list shows, from its index row; the definition is left out.
    private static SurveyVersion SummaryOf(SurveyVersionIndex row)
        => new()
        {
            ItemId = row.ItemId,
            SurveyId = row.SurveyId,
            Number = row.Number,
            PublishedUtc = row.PublishedUtc,
            PublishedBy = string.IsNullOrEmpty(row.PublishedBy) ? null : row.PublishedBy,
            Notes = row.Notes,
        };

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
    /// <param name="draft">The stamp (ticks) of the draft the page was shown with; a draft changed since is not replaced.</param>
    /// <param name="auditTrail">The audit trail.</param>
    /// <returns>A redirect to the designer.</returns>
    [HttpPost]
    [Admin("surveys/{id}/versions/{versionId}/restore", "SurveysVersionRestore")]
    public async Task<IActionResult> Restore(string id, string versionId, long? draft, [FromServices] ISurveyAuditTrail auditTrail)
    {
        // Restoring replaces the draft, which is editing: it needs Manage (as the Versions list does) as well as Publish.
        var survey = await AuthorizeAsync(id, SurveyOperation.Manage);

        if (survey is null || !await _surveyAuthorization.AuthorizeAsync(User, survey, SurveyOperation.Publish, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var result = await _publisher.RestoreAsync(id, versionId, SurveyDraftStamp.ToUtc(draft), HttpContext.RequestAborted);

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
        else if (result == SurveyErrorCode.Busy)
        {
            await _notifier.ErrorAsync(H["Someone else changed the draft since you opened this page. Reload the page and try again."]);

            return RedirectToAction(nameof(Index), new { id });
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
