using System.Text;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.Services;
using CrestApps.OrchardCore.DncRegistry.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.BackgroundJobs;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.DncRegistry.Controllers;

/// <summary>
/// Admin controller for managing local DNC registry lists.
/// </summary>
[Admin("dnc-registry/local/{action}/{listId?}", "LocalDncRegistry{action}")]
public sealed class LocalDncRegistryAdminController : Controller
{
    private readonly ILocalDncListManager _listManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDisplayManager<LocalDncList> _entryDisplayManager;
    private readonly IDisplayManager<LocalDncListOptions> _optionsDisplayManager;
    private readonly IDisplayManager<ImportLocalDncList> _importDisplayManager;
    private readonly IShapeFactory _shapeFactory;
    private readonly PagerOptions _pagerOptions;
    private readonly INotifier _notifier;
    private readonly IUpdateModelAccessor _updateModelAccessor;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDncRegistryAdminController"/> class.
    /// </summary>
    /// <param name="listManager">The local DNC list manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="entryDisplayManager">The display manager for DNC list entries.</param>
    /// <param name="optionsDisplayManager">The display manager for list options.</param>
    /// <param name="importDisplayManager">The display manager for import form.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="notifier">The notifier service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public LocalDncRegistryAdminController(
        ILocalDncListManager listManager,
        IAuthorizationService authorizationService,
        IDisplayManager<LocalDncList> entryDisplayManager,
        IDisplayManager<LocalDncListOptions> optionsDisplayManager,
        IDisplayManager<ImportLocalDncList> importDisplayManager,
        IShapeFactory shapeFactory,
        IOptions<PagerOptions> pagerOptions,
        INotifier notifier,
        IUpdateModelAccessor updateModelAccessor,
        IHtmlLocalizer<LocalDncRegistryAdminController> htmlLocalizer)
    {
        _listManager = listManager;
        _authorizationService = authorizationService;
        _entryDisplayManager = entryDisplayManager;
        _optionsDisplayManager = optionsDisplayManager;
        _importDisplayManager = importDisplayManager;
        _shapeFactory = shapeFactory;
        _pagerOptions = pagerOptions.Value;
        _notifier = notifier;
        _updateModelAccessor = updateModelAccessor;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Displays the list of uploaded local DNC lists with pagination.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    public async Task<IActionResult> Index(PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DncRegistryPermissions.ManageDncRegistrySettings))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, _pagerOptions.GetPageSize());
        var totalCount = await _listManager.GetCountAsync();
        var lists = await _listManager.GetListsAsync(pager.Page, pager.PageSize);

        var pagerShape = await _shapeFactory.PagerAsync(pager, totalCount);

        var summaries = new List<dynamic>();

        foreach (var entry in lists)
        {
            var shape = await _entryDisplayManager.BuildDisplayAsync(entry, _updateModelAccessor.ModelUpdater, "SummaryAdmin");
            shape.Properties["LocalDncList"] = entry;
            summaries.Add(shape);
        }

        var options = new LocalDncListOptions
        {
            StartIndex = pager.GetStartIndex(),
            EndIndex = pager.GetStartIndex() + summaries.Count - 1,
            TotalItemCount = totalCount,
        };

        var header = await _optionsDisplayManager.BuildEditorAsync(options, _updateModelAccessor.ModelUpdater, false);

        var listShape = await _shapeFactory.CreateAsync<ListLocalDncListsViewModel>("LocalDncListAdminList", viewModel =>
        {
            viewModel.Options = options;
            viewModel.Header = header;
            viewModel.Entries = summaries;
            viewModel.Pager = pagerShape;
        });

        return View(listShape);
    }

    /// <summary>
    /// Displays the upload form for a new local DNC list.
    /// </summary>
    public async Task<IActionResult> Upload()
    {
        if (!await _authorizationService.AuthorizeAsync(User, DncRegistryPermissions.ManageDncRegistrySettings))
        {
            return Forbid();
        }

        var importModel = new ImportLocalDncList();
        var content = await _importDisplayManager.BuildEditorAsync(importModel, _updateModelAccessor.ModelUpdater, false);

        return View(content);
    }

    /// <summary>
    /// Handles the upload form submission.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Upload))]
    public async Task<IActionResult> UploadPOST()
    {
        if (!await _authorizationService.AuthorizeAsync(User, DncRegistryPermissions.ManageDncRegistrySettings))
        {
            return Forbid();
        }

        var importModel = new ImportLocalDncList();
        var content = await _importDisplayManager.UpdateEditorAsync(importModel, _updateModelAccessor.ModelUpdater, false);

        if (!ModelState.IsValid)
        {
            return View(nameof(Upload), content);
        }

        await using var fileStream = importModel.File.OpenReadStream();
        var list = await _listManager.QueueImportAsync(
            importModel.Name,
            importModel.CountryCode,
            importModel.File.FileName,
            fileStream);

        TriggerImportProcessing(list.ListId);

        await _notifier.InformationAsync(H["The local DNC list '{0}' was uploaded and queued for background processing.", list.Name]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Triggers immediate processing of a pending or stalled local DNC list import.
    /// </summary>
    /// <param name="listId">The list identifier to process.</param>
    /// <param name="returnUrl">The URL to return to.</param>
    public async Task<IActionResult> ProcessNow(string listId, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DncRegistryPermissions.ManageDncRegistrySettings))
        {
            return Forbid();
        }

        var list = await _listManager.FindByIdAsync(listId);

        if (list == null)
        {
            return NotFound();
        }

        if (list.Status == LocalDncListStatus.Completed || list.Status == LocalDncListStatus.Deleting)
        {
            await _notifier.WarningAsync(H["The list '{0}' is not in a state that can be processed.", list.Name]);

            return RedirectToReturnUrlOrIndex(returnUrl);
        }

        // Commit the status change synchronously so the user sees "Processing"
        // immediately on redirect and the background task sees the committed state.
        await _listManager.ResumeImportAsync(listId);

        TriggerImportProcessing(listId);

        await _notifier.InformationAsync(H["Processing has been triggered for list '{0}'.", list.Name]);

        return RedirectToReturnUrlOrIndex(returnUrl);
    }

    /// <summary>
    /// Pauses an in-progress local DNC list import.
    /// </summary>
    /// <param name="listId">The list identifier to pause.</param>
    /// <param name="returnUrl">The URL to return to.</param>
    public async Task<IActionResult> PauseImport(string listId, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DncRegistryPermissions.ManageDncRegistrySettings))
        {
            return Forbid();
        }

        var list = await _listManager.FindByIdAsync(listId);

        if (list == null)
        {
            return NotFound();
        }

        if (list.Status != LocalDncListStatus.Processing)
        {
            await _notifier.WarningAsync(H["The list '{0}' is not in a state that can be paused.", list.Name]);

            return RedirectToReturnUrlOrIndex(returnUrl);
        }

        await _listManager.PauseImportAsync(listId);

        await _notifier.SuccessAsync(H["Import for list '{0}' has been paused.", list.Name]);

        return RedirectToReturnUrlOrIndex(returnUrl);
    }

    /// <summary>
    /// Deletes a local DNC list and all its entries in the background.
    /// </summary>
    /// <param name="listId">The list identifier to delete.</param>
    /// <param name="returnUrl">The URL to return to.</param>
    public async Task<IActionResult> Delete(string listId, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DncRegistryPermissions.ManageDncRegistrySettings))
        {
            return Forbid();
        }

        var list = await _listManager.FindByIdAsync(listId);

        if (list == null)
        {
            return NotFound();
        }

        await _listManager.MarkAsDeletingAsync(listId);

        await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(
            $"local-dnc-delete-{listId}",
            listId,
            static async (scope, id) =>
            {
                var manager = scope.ServiceProvider.GetRequiredService<ILocalDncListManager>();
                await manager.DeleteAsync(id);
            });

        await _notifier.InformationAsync(H["Your request to delete the list '{0}' has been received. The list and its entries will be removed in the background.", list.Name]);

        return RedirectToReturnUrlOrIndex(returnUrl);
    }

    /// <summary>
    /// Downloads a CSV file containing all rejected records with their original values and error reasons.
    /// </summary>
    /// <param name="listId">The list identifier.</param>
    /// <param name="fileStore">The local DNC file store.</param>
    public async Task<IActionResult> DownloadErrors(
        string listId,
        [FromServices] ILocalDncFileStore fileStore)
    {
        if (string.IsNullOrWhiteSpace(listId))
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, DncRegistryPermissions.ManageDncRegistrySettings))
        {
            return Forbid();
        }

        var list = await _listManager.FindByIdAsync(listId);

        if (list == null)
        {
            return NotFound();
        }

        if (list.ErrorMessages == null || list.ErrorMessages.Count == 0)
        {
            await _notifier.WarningAsync(H["No rejected records found for this list."]);

            return RedirectToAction(nameof(Index));
        }

        var csv = new StringBuilder();
        csv.AppendLine("Row,Value,Reason");

        var hasFileData = false;

        if (!string.IsNullOrWhiteSpace(list.StoredFileName))
        {
            var fileInfo = await fileStore.GetFileInfoAsync(list.StoredFileName);

            if (fileInfo != null && fileInfo.Length > 0)
            {
                await using var stream = await fileStore.GetFileStreamAsync(fileInfo);
                using var reader = new StreamReader(stream);
                var rowIndex = 0;

                while (await reader.ReadLineAsync() is { } line)
                {
                    rowIndex++;

                    if (!list.ErrorMessages.TryGetValue(rowIndex, out var reason))
                    {
                        continue;
                    }

                    var escapedValue = EscapeCsvField(line.Trim());
                    var escapedReason = EscapeCsvField(reason);
                    csv.AppendLine($"{rowIndex},{escapedValue},{escapedReason}");
                }

                hasFileData = true;
            }
        }

        if (!hasFileData)
        {
            foreach (var error in list.ErrorMessages.OrderBy(x => x.Key))
            {
                var escapedReason = EscapeCsvField(error.Value);
                csv.AppendLine($"{error.Key},,{escapedReason}");
            }
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        var fileName = $"{list.Name}_Rejected_Records.csv";

        return File(bytes, "text/csv", fileName);
    }

    private static void TriggerImportProcessing(string listId)
    {
        // Defer the background job until after the request has completed to ensure the user sees the updated status and any status changes are committed before the background task runs.
        ShellScope.AddDeferredTask(async scope =>
        {
            await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(
                $"local-dnc-import-{listId}",
                listId,
                static (scope, id) => BackgroundTasks.LocalDncImportBackgroundTask.ProcessEntriesAsync(scope.ServiceProvider, CancellationToken.None, id));
        });
    }

    private IActionResult RedirectToReturnUrlOrIndex(string returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    private static string EscapeCsvField(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
