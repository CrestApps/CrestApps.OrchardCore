using System.Text.Json;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Controllers;

/// <summary>
/// The JSON endpoints behind the builder's autosave and versions: saving and discarding the draft of a report, listing
/// and reading its versions, and restoring one into the draft. Each change states the revision it was based on and is
/// answered with 409 when someone else changed the report since.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
[Admin]
public sealed class ReportDesignHistoryController : Controller
{
    private readonly ReportDesignService _designService;
    private readonly ReportDesignHistoryService _history;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignHistoryController"/> class.
    /// </summary>
    /// <param name="designService">The design service.</param>
    /// <param name="history">The service that keeps drafts and versions.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public ReportDesignHistoryController(
        ReportDesignService designService,
        ReportDesignHistoryService history,
        IAuthorizationService authorizationService)
    {
        _designService = designService;
        _history = history;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Starts a new report as a draft with the builder's first changes, so nothing is lost before it is published.
    /// </summary>
    /// <returns>The new report's identifier, revision, and builder URL as JSON.</returns>
    [HttpPost]
    [Admin("reports/builder/drafts", "ReportDesignerDraftCreate")]
    public async Task<IActionResult> CreateDraft()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageOwnReportDesigns))
        {
            return Forbid();
        }

        var payload = await ReadPayloadAsync();

        if (payload is null)
        {
            return BadRequest();
        }

        var design = payload.ToDesign();

        design.ItemId = null;

        var result = await _history.CreateDraftAsync(design, User);

        return Json(new
        {
            Id = result.DesignId,
            result.Revision,
            result.ModifiedUtc,
            ModifiedBy = result.ModifiedByName,
            EditUrl = Url.RouteUrl("ReportDesignerEdit", new { id = result.DesignId }),
        }, ReportDesignerJson.Options);
    }

    /// <summary>
    /// Saves the builder's changes into the draft of a report.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <returns>The new revision as JSON, or 409 on a conflict.</returns>
    [HttpPost]
    [Admin("reports/builder/{id}/draft", "ReportDesignerDraftSave")]
    public async Task<IActionResult> SaveDraft(string id)
    {
        var design = await FindEditableAsync(id);

        if (design.Result is not null)
        {
            return design.Result;
        }

        var payload = await ReadPayloadAsync();

        if (payload is null)
        {
            return BadRequest();
        }

        return Outcome(await _history.SaveDraftAsync(design.Design, payload.ToDesign(), payload.Revision, payload.Force, User));
    }

    /// <summary>
    /// Throws away the unpublished changes of a report.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <param name="request">The revision the builder had.</param>
    /// <returns>The new revision as JSON, or 409 on a conflict.</returns>
    [HttpPost]
    [Admin("reports/builder/{id}/draft/discard", "ReportDesignerDraftDiscard")]
    public async Task<IActionResult> DiscardDraft(string id, [FromBody] ReportRevisionRequest request)
    {
        var design = await FindEditableAsync(id);

        if (design.Result is not null)
        {
            return design.Result;
        }

        request ??= new ReportRevisionRequest();

        // A report that was never published is only its draft, so discarding it deletes it.
        if (design.IsUnpublished)
        {
            var deleted = await _history.DeleteUnpublishedAsync(design.Design.ItemId, request.Revision, request.Force, User);

            return deleted.Status == ReportHistoryStatus.Saved
                ? Json(new { Deleted = true, ListUrl = Url.RouteUrl("ReportDesignsIndex") }, ReportDesignerJson.Options)
                : deleted.Status == ReportHistoryStatus.NotFound ? NotFound() : ConflictResult(deleted);
        }

        return Outcome(await _history.DiscardDraftAsync(design.Design, request.Revision, request.Force, User));
    }

    /// <summary>
    /// Lists the versions of a report from the newest.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <returns>The versions as JSON.</returns>
    [HttpGet]
    [Admin("reports/builder/{id}/versions", "ReportDesignerVersions")]
    public async Task<IActionResult> Versions(string id)
    {
        var design = await FindEditableAsync(id);

        if (design.Result is not null)
        {
            return design.Result;
        }

        var versions = design.IsUnpublished ? [] : await _history.ListVersionsAsync(design.Design.ItemId);

        return Json(versions.Select((version, index) => new
        {
            version.Number,
            version.DisplayText,
            version.CreatedUtc,
            version.CreatedByName,
            version.RestoredFrom,
            IsCurrent = index == 0,
        }), ReportDesignerJson.Options);
    }

    /// <summary>
    /// Reads a version of a report as the builder edits it, so it can be looked at or previewed.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <param name="number">The version number.</param>
    /// <returns>The version as JSON.</returns>
    [HttpGet]
    [Admin("reports/builder/{id}/versions/{number:int}", "ReportDesignerVersion")]
    public async Task<IActionResult> Version(string id, int number)
    {
        var design = await FindEditableAsync(id);

        if (design.Result is not null)
        {
            return design.Result;
        }

        var version = design.IsUnpublished ? null : await _history.FindVersionAsync(design.Design.ItemId, number);

        if (version?.Design is null)
        {
            return NotFound();
        }

        var payload = ReportDesignerPayload.From(version.Design);

        payload.Id = design.Design.ItemId;

        return Json(new
        {
            version.Number,
            version.CreatedUtc,
            version.CreatedByName,
            Design = payload,
        }, ReportDesignerJson.Options);
    }

    /// <summary>
    /// Copies a version into the draft of a report.
    /// </summary>
    /// <param name="id">The report identifier.</param>
    /// <param name="number">The version number.</param>
    /// <param name="request">The revision the builder had.</param>
    /// <returns>The new revision as JSON, or 409 on a conflict.</returns>
    [HttpPost]
    [Admin("reports/builder/{id}/versions/{number:int}/restore", "ReportDesignerVersionRestore")]
    public async Task<IActionResult> Restore(string id, int number, [FromBody] ReportRevisionRequest request)
    {
        var design = await FindEditableAsync(id);

        if (design.Result is not null)
        {
            return design.Result;
        }

        if (design.IsUnpublished)
        {
            return NotFound();
        }

        request ??= new ReportRevisionRequest();

        var result = await _history.RestoreAsync(design.Design, number, request.Revision, request.Force, User);

        return result.Status == ReportHistoryStatus.NotFound ? NotFound() : Outcome(result);
    }

    /// <summary>
    /// The answer to a change refused because someone else changed the report, or because another change of it took
    /// too long: 409 with who changed it last and the current revision.
    /// </summary>
    /// <param name="result">The refused change.</param>
    /// <returns>The result.</returns>
    internal static IActionResult ConflictResult(ReportHistoryResult result)
    {
        return new JsonResult(new
        {
            Conflict = result.Status == ReportHistoryStatus.Conflict,
            Busy = result.Status == ReportHistoryStatus.Busy,
            result.Revision,
            ModifiedBy = result.ModifiedByName,
            result.ModifiedUtc,
        }, ReportDesignerJson.Options)
        {
            StatusCode = StatusCodes.Status409Conflict,
        };
    }

    private IActionResult Outcome(ReportHistoryResult result)
    {
        if (result.Status != ReportHistoryStatus.Saved)
        {
            return ConflictResult(result);
        }

        return Json(new
        {
            result.Revision,
            result.ModifiedUtc,
            ModifiedBy = result.ModifiedByName,
            result.VersionNumber,
        }, ReportDesignerJson.Options);
    }

    private async Task<ReportDesignerPayload> ReadPayloadAsync()
    {
        var body = await RequestBodyReader.ReadAsync(Request, ReportDesignerController.MaxPayloadBytes, HttpContext.RequestAborted);

        if (body.IsTooLarge || string.IsNullOrWhiteSpace(body.Body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ReportDesignerPayload>(body.Body, ReportDesignerJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Finds a report the user may edit: a published one, or one that exists only as its draft, which is authorized the
    // same way through the owner recorded in the draft.
    private async Task<(ReportDesign Design, bool IsUnpublished, IActionResult Result)> FindEditableAsync(string id)
    {
        var design = await _designService.FindAsync(id);
        var isUnpublished = false;

        if (design is null)
        {
            design = (await _history.FindUnpublishedAsync(id))?.Design;
            isUnpublished = design is not null;
        }

        if (design is null)
        {
            return (null, false, NotFound());
        }

        if (!await _authorizationService.AuthorizeAsync(User, ReportDesignerPermissions.ManageAllReportDesigns, design))
        {
            return (null, false, Forbid());
        }

        return (design, isUnpublished, null);
    }
}

/// <summary>
/// The revision a change of a report's draft is based on.
/// </summary>
public sealed class ReportRevisionRequest
{
    /// <summary>
    /// Gets or sets the revision the builder had.
    /// </summary>
    public long Revision { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to make the change even when someone else changed the report since.
    /// </summary>
    public bool Force { get; set; }
}
