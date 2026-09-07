using System.Security.Claims;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

/// <summary>
/// Shows a customer the sites they have bought and how far along each one is.
/// </summary>
/// <remarks>
/// Creating a site takes long enough that a customer who is shown nothing assumes the purchase failed. This
/// page is what turns a wait into a status, and it is honest about the one outcome that needs a person:
/// a job that has failed enough times to be abandoned says so, rather than spinning forever.
/// </remarks>
[Admin("my-sites/{action}/{itemId?}", "MySites{action}")]
public sealed class MySitesController : Controller
{
    private readonly ITenantProvisioningJobStore _jobStore;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MySitesController"/> class.
    /// </summary>
    /// <param name="jobStore">The durable provisioning job store.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MySitesController(
        ITenantProvisioningJobStore jobStore,
        IAuthorizationService authorizationService,
        IHtmlLocalizer<MySitesController> htmlLocalizer,
        IStringLocalizer<MySitesController> stringLocalizer)
    {
        _jobStore = jobStore;
        _authorizationService = authorizationService;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the signed-in customer's sites.
    /// </summary>
    [Admin("my-sites", "MySitesIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageOwnSubscriptions))
        {
            return Forbid();
        }

        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(ownerId))
        {
            return Forbid();
        }

        var jobs = await _jobStore.GetByOwnerAsync(ownerId);

        return View(jobs);
    }
}

/// <summary>
/// The administration view of every site being built, including the ones that need a person.
/// </summary>
/// <remarks>
/// A job that has been abandoned means somebody paid and did not get their site. That is the one state in
/// this module that cannot be resolved automatically, so it needs a screen where an operator can see it and
/// retry once they have fixed whatever caused it.
/// </remarks>
[Admin("provisioning/{action}/{itemId?}", "TenantProvisioning{action}")]
public sealed class TenantProvisioningAdminController : Controller
{
    private readonly ITenantProvisioningJobStore _jobStore;
    private readonly ITenantProvisioningService _provisioningService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantProvisioningAdminController"/> class.
    /// </summary>
    /// <param name="jobStore">The durable provisioning job store.</param>
    /// <param name="provisioningService">The service that builds a site.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier used to surface outcomes.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    public TenantProvisioningAdminController(
        ITenantProvisioningJobStore jobStore,
        ITenantProvisioningService provisioningService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<TenantProvisioningAdminController> htmlLocalizer)
    {
        _jobStore = jobStore;
        _provisioningService = provisioningService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists every provisioning job.
    /// </summary>
    [Admin("provisioning", "TenantProvisioningIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageSubscriptions))
        {
            return Forbid();
        }

        var jobs = await _jobStore.GetAllAsync();

        return View(jobs.OrderByDescending(job => job.CreatedUtc).ToArray());
    }

    /// <summary>
    /// Tries a job again straight away.
    /// </summary>
    /// <param name="itemId">The provisioning job identifier.</param>
    [HttpPost]
    public async Task<IActionResult> Retry(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageSubscriptions))
        {
            return Forbid();
        }

        var job = string.IsNullOrEmpty(itemId) ? null : await _jobStore.FindByIdAsync(itemId);

        if (job is null)
        {
            return NotFound();
        }

        if (job.Status == TenantProvisioningStatus.Succeeded)
        {
            await _notifier.WarningAsync(H["That site has already been created."]);

            return RedirectToAction(nameof(Index));
        }

        // Retrying an abandoned job means an operator believes the cause is fixed, so the attempt counter and
        // the back-off are reset. Without that reset the job would be abandoned again on the next attempt and
        // the retry button would do nothing.
        job.Status = TenantProvisioningStatus.Pending;
        job.AttemptCount = 0;
        job.NextAttemptUtc = null;

        await _jobStore.UpdateAsync(job);

        if (string.IsNullOrEmpty(job.ProtectedAdminPassword))
        {
            // The password is dropped once a job reaches a terminal state, so an abandoned job can be queued
            // again but cannot complete. Saying so beats letting it fail once more for a reason nobody can
            // see from the screen.
            await _notifier.WarningAsync(H["This job no longer holds an administrator password, so the site cannot be created automatically. Create it manually and cancel the job."]);

            return RedirectToAction(nameof(Index));
        }

        var status = await _provisioningService.ProvisionAsync(job.ItemId);

        if (status == TenantProvisioningStatus.Succeeded)
        {
            await _notifier.SuccessAsync(H["The site was created."]);
        }
        else
        {
            await _notifier.WarningAsync(H["The site was not created. The job is queued for another attempt."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
