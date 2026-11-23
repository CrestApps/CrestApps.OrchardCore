using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation.Controllers;

/// <summary>
/// Files documents electronically: submits one in a format through a channel, downloads the file built, asks the channel
/// for the agency's answer, and records the answer to a document filed by hand. Each action checks the document's own
/// permission through its target provider.
/// </summary>
[Admin]
[Feature(TaxationConstants.Feature.EFiling)]
public sealed class EFilingController : Controller
{
    private readonly IEFilingService _eFiling;
    private readonly IEFileSubmissionManager _submissions;
    private readonly IEnumerable<IEFileTargetProvider> _targets;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public EFilingController(
        IEFilingService eFiling,
        IEFileSubmissionManager submissions,
        IEnumerable<IEFileTargetProvider> targets,
        INotifier notifier,
        IHtmlLocalizer<EFilingController> htmlLocalizer)
    {
        _eFiling = eFiling;
        _submissions = submissions;
        _targets = targets;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [HttpPost]
    [Admin("taxation/e-filing/submit", "TaxationEFilingSubmit")]
    public async Task<IActionResult> Submit(string targetType, string targetId, string option)
    {
        var target = await _eFiling.FindTargetAsync(targetType, targetId, HttpContext.RequestAborted);

        if (target is null)
        {
            return NotFound();
        }

        if (!await CanFileAsync(target))
        {
            return Forbid();
        }

        var parts = (option ?? string.Empty).Split('|', 2);
        var result = parts.Length == 2
            ? await _eFiling.SubmitAsync(target, parts[0], parts[1], User, HttpContext.RequestAborted)
            : null;

        if (result is null)
        {
            await _notifier.ErrorAsync(H["Choose how to file the document."]);
        }
        else if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", result.Errors)]);
        }
        else if (result.Submission.Status == EFileSubmissionStatus.Submitted && result.Submission.IsManual)
        {
            await _notifier.SuccessAsync(H["The file is ready: download it, file it on the agency's site, then record the agency's answer."]);
        }
        else
        {
            await _notifier.SuccessAsync(H["The document has been submitted."]);
        }

        return BackTo(target);
    }

    [Admin("taxation/e-filing/{id}/download", "TaxationEFilingDownload")]
    public async Task<IActionResult> Download(string id)
    {
        var (submission, target) = await FindAsync(id);

        if (submission is null || target is null || string.IsNullOrEmpty(submission.Content))
        {
            return NotFound();
        }

        if (!await CanFileAsync(target))
        {
            return Forbid();
        }

        return File(Convert.FromBase64String(submission.Content), submission.ContentType ?? "application/octet-stream", submission.FileName ?? "submission");
    }

    [HttpPost]
    [Admin("taxation/e-filing/{id}/check", "TaxationEFilingCheck")]
    public async Task<IActionResult> Check(string id)
    {
        var (submission, target) = await FindAsync(id);

        if (submission is null || target is null)
        {
            return NotFound();
        }

        if (!await CanFileAsync(target))
        {
            return Forbid();
        }

        var result = await _eFiling.CheckAsync(submission, HttpContext.RequestAborted);

        await (result.Succeeded
            ? _notifier.SuccessAsync(H["The filing channel has been asked for the agency's answer."])
            : _notifier.ErrorAsync(H["{0}", string.Join(" ", result.Errors)]));

        return BackTo(target);
    }

    [HttpPost]
    [Admin("taxation/e-filing/{id}/answer", "TaxationEFilingAnswer")]
    public async Task<IActionResult> RecordAnswer(string id, bool accepted, string confirmationNumber, DateOnly? filedOn, string message)
    {
        var (submission, target) = await FindAsync(id);

        if (submission is null || target is null)
        {
            return NotFound();
        }

        if (!await CanFileAsync(target))
        {
            return Forbid();
        }

        var result = await _eFiling.RecordAnswerAsync(submission, accepted, confirmationNumber, filedOn ?? default, message, User, HttpContext.RequestAborted);

        await (result.Succeeded
            ? _notifier.SuccessAsync(accepted ? H["The acceptance has been recorded and the document is marked as filed."] : H["The rejection has been recorded: correct the document and file it again."])
            : _notifier.ErrorAsync(H["{0}", string.Join(" ", result.Errors)]));

        return BackTo(target);
    }

    private async Task<(EFileSubmission Submission, EFileTarget Target)> FindAsync(string id)
    {
        if (string.IsNullOrEmpty(id) || await _submissions.FindByIdAsync(id, HttpContext.RequestAborted) is not { } submission)
        {
            return (null, null);
        }

        return (submission, await _eFiling.FindTargetAsync(submission.TargetType, submission.TargetId, HttpContext.RequestAborted));
    }

    private async Task<bool> CanFileAsync(EFileTarget target)
        => _targets.FirstOrDefault(provider => provider.TargetType == target.Type) is { } provider
            && await provider.CanFileAsync(User, target);

    // A tax return goes back to its page; another kind of document to the tax returns.
    private RedirectToActionResult BackTo(EFileTarget target)
        => target.Type == TaxationConstants.TaxReturnEFileTargetType
            ? RedirectToAction(nameof(TaxReturnsController.Details), "TaxReturns", new { id = target.Id })
            : RedirectToAction(nameof(TaxReturnsController.Index), "TaxReturns");
}
