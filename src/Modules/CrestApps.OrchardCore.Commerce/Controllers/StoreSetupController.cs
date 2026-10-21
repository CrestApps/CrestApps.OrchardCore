using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Commerce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Commerce.Controllers;

/// <summary>
/// Commerce › Set up your store: the steps from a new site to its first sale, each from the module it's about, with
/// what's done, a quick way to do the simple ones, and a link to where each is done in full.
/// </summary>
[Admin("commerce/setup/{action}/{name?}", "CommerceSetup{action}")]
public sealed class StoreSetupController : Controller
{
    private readonly IEnumerable<IStoreSetupStep> _steps;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public StoreSetupController(
        IEnumerable<IStoreSetupStep> steps,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<StoreSetupController> htmlLocalizer)
    {
        _steps = steps;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [Admin("commerce/setup", "CommerceSetup")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, CommercePermissions.SetUpStore))
        {
            return Forbid();
        }

        var progress = await _siteService.GetSettingsAsync<StoreSetupSettings>();
        var model = new StoreSetupViewModel();

        foreach (var step in _steps.OrderBy(step => step.Order).ThenBy(step => step.Name, StringComparer.Ordinal))
        {
            var description = await step.DescribeAsync(HttpContext.RequestAborted);
            var done = description.IsDone ?? progress.Completed.Contains(step.Name, StringComparer.Ordinal);

            model.Steps.Add(new StoreSetupStepViewModel
            {
                Name = step.Name,
                Description = description,
                IsDone = done,
                IsSkipped = !done && progress.Skipped.Contains(step.Name, StringComparer.Ordinal),
            });
        }

        // The step to do now: the first that's neither done nor skipped, the required ones first.
        model.Current = model.Steps.FirstOrDefault(step => !step.IsDone && !step.IsSkipped && !step.Description.IsOptional)?.Name
            ?? model.Steps.FirstOrDefault(step => !step.IsDone && !step.IsSkipped)?.Name;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(string name)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CommercePermissions.SetUpStore))
        {
            return Forbid();
        }

        if (FindStep(name) is not { } step)
        {
            return NotFound();
        }

        var values = Request.Form
            .Where(entry => entry.Key.StartsWith(FieldPrefix, StringComparison.Ordinal))
            .ToDictionary(entry => entry.Key[FieldPrefix.Length..], entry => entry.Value.ToString(), StringComparer.Ordinal);

        var result = await step.RunAsync(new StoreSetupActionContext { Values = values }, HttpContext.RequestAborted);

        if (result is null)
        {
            return NotFound();
        }

        if (result.Succeeded)
        {
            await UpdateProgressAsync(progress =>
            {
                if (!progress.Completed.Contains(step.Name, StringComparer.Ordinal))
                {
                    progress.Completed.Add(step.Name);
                }

                progress.Skipped.Remove(step.Name);
            });

            await _notifier.SuccessAsync(new LocalizedHtmlString(result.Message.Name, HtmlEncoder.Default.Encode(result.Message.Value)));

            // On to the next step.
            return RedirectToAction(nameof(Index));
        }

        await _notifier.ErrorAsync(new LocalizedHtmlString(result.Message.Name, HtmlEncoder.Default.Encode(result.Message.Value)));

        return RedirectToAction(nameof(Index), new { step = step.Name });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Skip(string name, bool undo = false)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CommercePermissions.SetUpStore))
        {
            return Forbid();
        }

        if (FindStep(name) is not { } step)
        {
            return NotFound();
        }

        await UpdateProgressAsync(progress =>
        {
            progress.Skipped.Remove(step.Name);

            if (!undo)
            {
                progress.Skipped.Add(step.Name);
            }
        });

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// The prefix of a quick action's field names in the posted form.
    /// </summary>
    internal const string FieldPrefix = "setup.";

    private IStoreSetupStep FindStep(string name)
        => _steps.FirstOrDefault(step => string.Equals(step.Name, name, StringComparison.Ordinal));

    private async Task UpdateProgressAsync(Action<StoreSetupSettings> update)
    {
        var site = await _siteService.LoadSiteSettingsAsync();
        var progress = site.GetOrCreate<StoreSetupSettings>();

        update(progress);
        site.Put(progress);
        await _siteService.UpdateSiteSettingsAsync(site);
    }
}
