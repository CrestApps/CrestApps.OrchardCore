using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Commerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Commerce.Controllers;

/// <summary>
/// Commerce › Set Up Your Store: the steps from a new site to its first sale, each from the module it's about, with
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
        var model = await StoreSetupGuide.DescribeAsync(_steps, progress, HttpContext.RequestAborted);

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

        var result = await StoreSetupGuide.RunAsync(step, values, _siteService, HttpContext.RequestAborted);

        if (result is null)
        {
            return NotFound();
        }

        if (result.Succeeded)
        {
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

        await StoreSetupGuide.SkipAsync(step, undo, _siteService);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// The prefix of a quick action's field names in the posted form.
    /// </summary>
    internal const string FieldPrefix = "setup.";

    private IStoreSetupStep FindStep(string name)
        => StoreSetupGuide.Find(_steps, name);
}
