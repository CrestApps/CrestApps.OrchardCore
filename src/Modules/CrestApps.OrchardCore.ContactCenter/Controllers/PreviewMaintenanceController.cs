using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Maintenance;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Provides the operator-visible export, quiesce, reset, and verify procedure for a Contact Center preview
/// tenant.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.Maintenance)]
public sealed class PreviewMaintenanceController : Controller
{
    private const string _exportReceiptKey = "ContactCenterPreviewExportReceipt";

    private readonly IContactCenterPreviewMaintenanceService _maintenanceService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly ContactCenterPreviewMaintenanceOptions _options;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewMaintenanceController"/> class.
    /// </summary>
    /// <param name="maintenanceService">The preview maintenance service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="options">The preview maintenance options.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public PreviewMaintenanceController(
        IContactCenterPreviewMaintenanceService maintenanceService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IOptions<ContactCenterPreviewMaintenanceOptions> options,
        IHtmlLocalizer<PreviewMaintenanceController> htmlLocalizer)
    {
        _maintenanceService = maintenanceService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _options = options.Value;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Displays the live Contact Center data set counts and the state of the maintenance procedure.
    /// </summary>
    /// <returns>The maintenance page.</returns>
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManagePreviewData))
        {
            return Forbid();
        }

        return View(await BuildViewModelAsync());
    }

    /// <summary>
    /// Exports every Contact Center data set of the current tenant as a downloadable JSON document.
    /// </summary>
    /// <returns>The export file.</returns>
    [HttpPost]
    [ActionName(nameof(Export))]
    public async Task<IActionResult> Export()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManagePreviewData))
        {
            return Forbid();
        }

        using var buffer = new MemoryStream();
        var report = await _maintenanceService.ExportAsync(buffer, HttpContext.RequestAborted);

        TempData[_exportReceiptKey] = report.Receipt;

        var fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"contact-center-{report.TenantName}-{report.TakenUtc:yyyyMMddHHmmss}.json");

        return File(buffer.ToArray(), "application/json", fileName);
    }

    /// <summary>
    /// Closes Contact Center work admission and waits for in-flight work to drain.
    /// </summary>
    /// <returns>A redirect back to the maintenance page.</returns>
    [HttpPost]
    [ActionName(nameof(Quiesce))]
    public async Task<IActionResult> Quiesce()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManagePreviewData))
        {
            return Forbid();
        }

        var report = await _maintenanceService.QuiesceAsync(
            TimeSpan.FromSeconds(_options.DrainTimeoutSeconds),
            HttpContext.RequestAborted);

        if (report.IsDrained)
        {
            await _notifier.SuccessAsync(H["Contact Center work admission is closed and {0} feature(s) drained.", report.QuiescedFeatureIds.Count]);
        }
        else
        {
            await _notifier.WarningAsync(H["Contact Center work admission is closed, but {0} feature(s) did not drain in time: {1}.", report.DrainTimedOutFeatureIds.Count, string.Join(", ", report.DrainTimedOutFeatureIds)]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Reopens Contact Center work admission.
    /// </summary>
    /// <returns>A redirect back to the maintenance page.</returns>
    [HttpPost]
    [ActionName(nameof(Resume))]
    public async Task<IActionResult> Resume()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManagePreviewData))
        {
            return Forbid();
        }

        var featureIds = await _maintenanceService.ResumeAsync();

        await _notifier.SuccessAsync(H["Contact Center work admission is open again for {0} feature(s).", featureIds.Count]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes the Contact Center data of the current tenant when every guard is satisfied.
    /// </summary>
    /// <param name="model">The operator's reset request.</param>
    /// <returns>A redirect back to the maintenance page.</returns>
    [HttpPost]
    [ActionName(nameof(Reset))]
    public async Task<IActionResult> Reset(ContactCenterPreviewMaintenanceViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManagePreviewData))
        {
            return Forbid();
        }

        var report = await _maintenanceService.ResetAsync(
            new ContactCenterPreviewResetRequest
            {
                ConfirmationToken = model.ConfirmationToken,
                ExportReceipt = model.ExportReceipt,
                Scope = model.Scope,
            },
            HttpContext.RequestAborted);

        if (!report.Succeeded)
        {
            await _notifier.ErrorAsync(DescribeRefusal(report.RefusalReason));

            return RedirectToAction(nameof(Index));
        }

        var verification = await _maintenanceService.VerifyAsync(report.Scope, HttpContext.RequestAborted);

        if (verification.IsClean)
        {
            await _notifier.SuccessAsync(H["Deleted {0} document(s) and verified that every data set in scope is empty.", report.DeletedCount]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Deleted {0} document(s), but the following data set(s) are not empty: {1}.", report.DeletedCount, string.Join(", ", verification.ResidualDataSetKeys)]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Verifies that every data set in the supplied scope is empty.
    /// </summary>
    /// <param name="scope">The scope to verify.</param>
    /// <returns>A redirect back to the maintenance page.</returns>
    [HttpPost]
    [ActionName(nameof(Verify))]
    public async Task<IActionResult> Verify(ContactCenterPreviewResetScope scope)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManagePreviewData))
        {
            return Forbid();
        }

        var verification = await _maintenanceService.VerifyAsync(scope, HttpContext.RequestAborted);

        if (verification.IsClean)
        {
            await _notifier.SuccessAsync(H["Every Contact Center data set in the {0} scope is empty.", scope.ToString()]);
        }
        else
        {
            await _notifier.WarningAsync(H["The following Contact Center data set(s) still hold documents: {0}.", string.Join(", ", verification.ResidualDataSetKeys)]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<ContactCenterPreviewMaintenanceViewModel> BuildViewModelAsync()
    {
        var status = await _maintenanceService.GetStatusAsync(HttpContext.RequestAborted);

        return new ContactCenterPreviewMaintenanceViewModel
        {
            TenantName = status.TenantName,
            DataSets = status.DataSets,
            ParticipatingFeatureIds = status.ParticipatingFeatureIds,
            QuiescedFeatureIds = status.QuiescedFeatureIds,
            IsResetAllowed = status.IsResetAllowed,
            IsProductionRefusal = status.IsProductionRefusal,
            ExportReceipt = TempData.Peek(_exportReceiptKey) as string,
        };
    }

    private LocalizedHtmlString DescribeRefusal(ContactCenterPreviewResetRefusalReason reason)
    {
        return reason switch
        {
            ContactCenterPreviewResetRefusalReason.ResetNotAllowed
                => H["The reset was refused because reset is not enabled. Set CrestApps_ContactCenter:PreviewMaintenance:AllowReset to true on this tenant."],
            ContactCenterPreviewResetRefusalReason.ProductionEnvironment
                => H["The reset was refused because the host is running in the Production environment."],
            ContactCenterPreviewResetRefusalReason.ConfirmationTokenMismatch
                => H["The reset was refused because the confirmation you typed does not match the tenant name."],
            ContactCenterPreviewResetRefusalReason.WorkNotQuiesced
                => H["The reset was refused because Contact Center work admission is still open. Quiesce the tenant first."],
            ContactCenterPreviewResetRefusalReason.ExportReceiptMissing
                => H["The reset was refused because no export receipt was supplied. Export the tenant first."],
            ContactCenterPreviewResetRefusalReason.ExportReceiptStale
                => H["The reset was refused because the export receipt no longer matches the live data. The data changed after the export was taken, so export again."],
            _ => H["The reset was refused."],
        };
    }
}
