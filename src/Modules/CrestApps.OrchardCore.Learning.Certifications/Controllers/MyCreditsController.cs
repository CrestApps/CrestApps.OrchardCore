using System.Security.Claims;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Certifications.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Certifications.Controllers;

/// <summary>
/// The learner's education credits, and recording external learning with evidence.
/// </summary>
[Authorize]
public sealed class MyCreditsController : Controller
{
    private const long MaxEvidenceBytes = 10 * 1024 * 1024;

    private readonly IEducationCreditService _credits;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public MyCreditsController(IEducationCreditService credits, ILocalClock localClock, INotifier notifier, IHtmlLocalizer<MyCreditsController> htmlLocalizer, IStringLocalizer<MyCreditsController> stringLocalizer)
    {
        _credits = credits;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [HttpGet]
    [Route("learning/credits")]
    public async Task<IActionResult> Index()
        => View(await BuildAsync(new MyExternalLearningForm()));

    [HttpPost]
    [Route("learning/credits")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([Bind(Prefix = "Form")] MyExternalLearningForm form, IFormFile evidence, [FromServices] IProtectedFileStore<ExternalLearningEvidence> files)
    {
        if (evidence is { Length: > MaxEvidenceBytes })
        {
            ModelState.AddModelError(string.Empty, S["The evidence file is larger than 10 MB."]);
        }

        if (form.CompletedOn is null)
        {
            ModelState.AddModelError("Form.CompletedOn", S["Say when you completed it."]);
        }

        var submission = new ExternalLearningSubmission
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Title = form.Title,
            Provider = form.Provider,
            CompletedOn = form.CompletedOn ?? default,
            CreditTypeId = form.CreditTypeId,
            Amount = form.Amount ?? 0,
        };

        if (ModelState.IsValid && evidence is { Length: > 0 })
        {
            await using var stream = evidence.OpenReadStream();
            var saved = await files.SaveAsync(evidence.FileName, stream, HttpContext.RequestAborted);

            submission.EvidenceFileId = saved.FileId;
            submission.EvidenceFileName = saved.FileName;
        }

        if (ModelState.IsValid)
        {
            var errors = await _credits.SubmitExternalAsync(submission, HttpContext.RequestAborted);

            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            if (errors.Count == 0)
            {
                await _notifier.SuccessAsync(H["Recorded. Your credits are added once the learning is accepted."]);

                return RedirectToAction(nameof(Index));
            }

            if (submission.EvidenceFileId is not null)
            {
                await files.DeleteAsync(submission.EvidenceFileId, HttpContext.RequestAborted);
            }
        }

        return View(nameof(Index), await BuildAsync(form));
    }

    private async Task<MyCreditsViewModel> BuildAsync(MyExternalLearningForm form)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var types = (await _credits.ListTypesAsync(HttpContext.RequestAborted)).ToDictionary(type => type.ItemId, StringComparer.Ordinal);
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
        var balances = await _credits.GetBalancesAsync(userId, today, HttpContext.RequestAborted);
        var entries = new List<MyCreditEntry>();

        foreach (var entry in await _credits.ListEntriesAsync(userId, HttpContext.RequestAborted))
        {
            entries.Add(new MyCreditEntry
            {
                Entry = entry,
                TypeName = types.TryGetValue(entry.Bucket, out var type) ? type.Name : entry.Bucket,
                Earned = entry.EffectiveDate.ToString("d"),
                Expires = entry.ExpiresOn?.ToString("d"),
            });
        }

        return new MyCreditsViewModel
        {
            Balances = balances
                .Where(balance => types.ContainsKey(balance.Key))
                .Select(balance => new MyCreditBalance { Name = types[balance.Key].Name, Unit = types[balance.Key].Unit, Amount = balance.Value })
                .OrderBy(balance => balance.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            Entries = entries,
            Submissions = (await _credits.ListExternalForUserAsync(userId, HttpContext.RequestAborted))
                .Select(submission => new MyExternalLearning
                {
                    Submission = submission,
                    TypeName = types.TryGetValue(submission.CreditTypeId ?? string.Empty, out var type) ? type.Name : submission.CreditTypeId,
                    Completed = submission.CompletedOn.ToString("d"),
                })
                .ToArray(),
            ExternalTypes = types.Values.Where(type => type.AllowExternalLearning).Select(type => new SelectListItem(type.Name, type.ItemId, type.ItemId == form.CreditTypeId)).ToList(),
            Form = form,
        };
    }
}
