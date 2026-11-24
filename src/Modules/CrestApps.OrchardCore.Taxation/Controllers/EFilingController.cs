using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using ISession = YesSql.ISession;

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
    private readonly IDisplayManager<EFileSubmissionRequest> _requestDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly INotifier _notifier;
    private readonly ISession _session;
    private readonly ILogger _logger;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public EFilingController(
        IEFilingService eFiling,
        IEFileSubmissionManager submissions,
        IEnumerable<IEFileTargetProvider> targets,
        IDisplayManager<EFileSubmissionRequest> requestDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        INotifier notifier,
        ISession session,
        ILogger<EFilingController> logger,
        IHtmlLocalizer<EFilingController> htmlLocalizer,
        IStringLocalizer<EFilingController> stringLocalizer)
    {
        _eFiling = eFiling;
        _submissions = submissions;
        _targets = targets;
        _requestDisplayManager = requestDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _notifier = notifier;
        _session = session;
        _logger = logger;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [HttpPost]
    [Admin("taxation/e-filing/submit", "TaxationEFilingSubmit")]
    public async Task<IActionResult> Submit(string targetType, string targetId)
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

        // The submit form is built from drivers: each one reads its own fields, such as a pack's payment choices.
        var request = new EFileSubmissionRequest { Target = target };

        await _requestDisplayManager.UpdateEditorAsync(request, _updateModelAccessor.ModelUpdater, isNew: true);

        if (!ModelState.IsValid)
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage))]);

            return BackTo(target);
        }

        var result = await RunAsync(target, () => _eFiling.SubmitAsync(request, User, HttpContext.RequestAborted));

        if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", result.Errors)]);
        }
        else if (result.Submission.IsTest)
        {
            await _notifier.SuccessAsync(H["The test submission has been sent to the agency's test system: nothing is filed."]);
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

        var result = await RunAsync(target, () => _eFiling.CheckAsync(submission, HttpContext.RequestAborted));

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

        var result = await RunAsync(target, () => _eFiling.RecordAnswerAsync(submission, accepted, confirmationNumber, filedOn ?? default, message, User, HttpContext.RequestAborted));

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

    // Marking an accepted document filed runs the modules that follow it (such as one that posts to the books); when one
    // fails, nothing is saved and the person is told instead of getting an error page.
    private async Task<EFilingResult> RunAsync(EFileTarget target, Func<Task<EFilingResult>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await _session.CancelAsync();
            _logger.LogError(exception, "E-filing the '{TargetType}' '{TargetId}' failed, so nothing of it was saved.", target.Type, target.Id);

            var result = new EFilingResult();

            result.Errors.Add(S["Nothing was saved: a module that acts on the document failed. The error has been logged."]);

            return result;
        }
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
