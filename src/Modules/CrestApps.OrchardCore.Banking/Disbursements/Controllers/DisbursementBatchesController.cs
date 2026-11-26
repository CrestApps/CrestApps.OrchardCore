using System.Text;
using CrestApps.OrchardCore.Banking.DirectDeposit;
using CrestApps.OrchardCore.Banking.Disbursements.Core.Services;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using CrestApps.OrchardCore.Banking.Disbursements.ViewModels;
using CrestApps.OrchardCore.SourceAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Banking.Disbursements.Controllers;

/// <summary>
/// Banking › Disbursement Batches: release a batch to its provider, download its bank file, settle or cancel it, and
/// import the bank's return and change file.
/// </summary>
[Admin]
[Feature(DisbursementsConstants.Features.Disbursements)]
public sealed class DisbursementBatchesController : Controller
{
    private const int MaxReturnFileBytes = 5 * 1024 * 1024;

    private readonly IDisbursementBatchManager _batches;
    private readonly IDisbursementBatchStore _batchStore;
    private readonly IDisbursementService _disbursementService;
    private readonly IDisbursementFileStore _files;
    private readonly ISourceAccessService _access;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly INotifier _notifier;
    private readonly DisbursementFileRecorder _fileRecorder;
    private readonly IDisplayManager<DisbursementReturnsViewModel> _returnsDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;

    internal readonly IHtmlLocalizer H;

    public DisbursementBatchesController(
        IDisbursementBatchManager batches,
        IDisbursementBatchStore batchStore,
        IDisbursementService disbursementService,
        IDisbursementFileStore files,
        ISourceAccessService access,
        IAuthorizationService authorizationService,
        IShellFeaturesManager shellFeaturesManager,
        INotifier notifier,
        DisbursementFileRecorder fileRecorder,
        IDisplayManager<DisbursementReturnsViewModel> returnsDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        IHtmlLocalizer<DisbursementBatchesController> htmlLocalizer)
    {
        _fileRecorder = fileRecorder;
        _returnsDisplayManager = returnsDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _batches = batches;
        _batchStore = batchStore;
        _disbursementService = disbursementService;
        _files = files;
        _access = access;
        _authorizationService = authorizationService;
        _shellFeaturesManager = shellFeaturesManager;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [Admin("banking/disbursement-batches", "DisbursementBatchesIndex")]
    public async Task<IActionResult> Index(
        DisbursementBatchesIndexViewModel filter,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DisbursementsPermissions.ManageDisbursements))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var access = await _access.GetFilterAsync(User, SourceAccessLevel.Manage, HttpContext.RequestAborted);

        // The store reads only the batches the person may act on, so the count and the pages agree.
        var (count, batches) = await _batchStore.ListPageAsync(
            Enum.TryParse<DisbursementBatchStatus>(filter.Status, out var status) ? status : null,
            access,
            pager.GetStartIndex(),
            pager.PageSize,
            HttpContext.RequestAborted);
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(filter.Status))
        {
            routeData.Values["Status"] = filter.Status;
        }

        // A batch that holds a disbursement of a source the person may not act on is not listed.
        filter.List = new AdminListContext<DisbursementBatch>
        {
            Source = batches.Where(access.Allows).ToList(),
            Pager = (IShape)await shapeFactory.PagerAsync(pager, count, routeData),
        };
        filter.CanImportReturns = await IsNachaEnabledAsync();

        return View(filter);
    }

    [HttpPost]
    [Admin("banking/disbursement-batches/release/{id}", "DisbursementBatchesRelease")]
    public async Task<IActionResult> Release(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DisbursementsPermissions.ManageDisbursements))
        {
            return Forbid();
        }

        if (await FindManagedAsync(id) is not { } batch)
        {
            return NotFound();
        }

        var errors = await _disbursementService.ReleaseBatchAsync(batch, HttpContext.RequestAborted);

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }
        }
        else
        {
            await _notifier.SuccessAsync(batch.HasFile
                ? H["The batch was released. Download its bank file and send it to the bank, or let a data pipeline deliver it."]
                : H["The batch was released."]);
        }

        return RedirectToAction(nameof(Index));
    }

    [Admin("banking/disbursement-batches/download/{id}", "DisbursementBatchesDownload")]
    public async Task<IActionResult> Download(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DisbursementsPermissions.ManageDisbursements))
        {
            return Forbid();
        }

        if (await FindManagedAsync(id) is not { HasFile: true } batch || await _files.ReadAsync(batch.ItemId, HttpContext.RequestAborted) is not { } file)
        {
            return NotFound();
        }

        // The file holds every payee's account number: never cached, and each download is recorded.
        Response.Headers.CacheControl = "no-store";
        await _fileRecorder.RecordAsync(batch, "downloaded", User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, User.Identity?.Name);

        return File(file, "text/plain", batch.FileName ?? $"batch-{batch.ItemId}.txt");
    }

    [HttpPost]
    [Admin("banking/disbursement-batches/settle/{id}", "DisbursementBatchesSettle")]
    public async Task<IActionResult> Settle(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DisbursementsPermissions.ManageDisbursements))
        {
            return Forbid();
        }

        if (await FindManagedAsync(id) is not { } batch)
        {
            return NotFound();
        }

        var count = await _disbursementService.SettleBatchAsync(batch, HttpContext.RequestAborted);

        await _notifier.SuccessAsync(H["{0} disbursements of the batch are recorded as paid.", count]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Admin("banking/disbursement-batches/cancel/{id}", "DisbursementBatchesCancel")]
    public async Task<IActionResult> Cancel(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DisbursementsPermissions.ManageDisbursements))
        {
            return Forbid();
        }

        if (await FindManagedAsync(id) is not { } batch)
        {
            return NotFound();
        }

        if (await _disbursementService.CancelBatchAsync(batch, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["The batch was cancelled. Its disbursements are pending again."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Only an open batch can be cancelled."]);
        }

        return RedirectToAction(nameof(Index));
    }

    [Admin("banking/disbursement-batches/returns", "DisbursementBatchesReturns")]
    public async Task<IActionResult> Returns()
    {
        if (!await _authorizationService.AuthorizeAsync(User, DisbursementsPermissions.ManageDisbursements) || !await IsNachaEnabledAsync())
        {
            return Forbid();
        }

        return View(await ReturnsEditorAsync(new DisbursementReturnsViewModel()));
    }

    [HttpPost]
    [ActionName(nameof(Returns))]
    [Admin("banking/disbursement-batches/returns", "DisbursementBatchesReturns")]
    public async Task<IActionResult> ReturnsPost(IFormFile file)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DisbursementsPermissions.ManageDisbursements) || !await IsNachaEnabledAsync())
        {
            return Forbid();
        }

        if (file is null || file.Length == 0 || file.Length > MaxReturnFileBytes)
        {
            ModelState.AddModelError(nameof(file), H["Choose the bank's return file, 5 MB at most."].Value);

            return View(await ReturnsEditorAsync(new DisbursementReturnsViewModel()));
        }

        string text;

        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.ASCII))
        {
            text = await reader.ReadToEndAsync(HttpContext.RequestAborted);
        }

        var (returns, notices, problems) = NachaReturnFileReader.Read(text);

        // Only the disbursements the person may act on; a correction of bank numbers also needs Manage bank accounts.
        var access = await _access.GetFilterAsync(User, SourceAccessLevel.Manage, HttpContext.RequestAborted);
        var canChangeBankAccounts = await _authorizationService.AuthorizeAsync(User, Banking.BankingPermissions.ManageBankAccounts);
        var result = await _disbursementService.ApplyReturnsAsync(returns, access, HttpContext.RequestAborted);
        var corrections = await _disbursementService.ApplyChangeNoticesAsync(notices, access, canChangeBankAccounts, HttpContext.RequestAborted);

        result.Corrected = corrections.Corrected;
        result.Problems.AddRange(problems);
        result.Problems.AddRange(corrections.Problems);

        return View(await ReturnsEditorAsync(new DisbursementReturnsViewModel { Result = result }));
    }

    private async Task<DisbursementReturnsViewModel> ReturnsEditorAsync(DisbursementReturnsViewModel model)
    {
        model.Editor = await _returnsDisplayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false);

        return model;
    }

    [HttpPost]
    [Admin("banking/disbursement-batches/reopen/{id}", "DisbursementBatchesReopen")]
    public async Task<IActionResult> Reopen(string id, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DisbursementsPermissions.ManageDisbursements))
        {
            return Forbid();
        }

        if (await FindManagedAsync(id) is not { } batch)
        {
            return NotFound();
        }

        if (await _disbursementService.ReopenReleasingBatchAsync(batch, string.IsNullOrWhiteSpace(reason) ? H["Checked with the provider: nothing was sent."].Value : reason, HttpContext.RequestAborted))
        {
            await _notifier.SuccessAsync(H["The batch is open again. Release it to send it."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Only a batch left being sent can be reopened."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<DisbursementBatch> FindManagedAsync(string id)
    {
        if (string.IsNullOrEmpty(id) || await _batches.FindByIdAsync(id) is not { } batch)
        {
            return null;
        }

        return (await _access.GetFilterAsync(User, SourceAccessLevel.Manage, HttpContext.RequestAborted)).Allows(batch) ? batch : null;
    }

    private async Task<bool> IsNachaEnabledAsync()
        => (await _shellFeaturesManager.GetEnabledFeaturesAsync()).Any(feature => feature.Id == DisbursementsConstants.Features.DirectDeposit);
}
