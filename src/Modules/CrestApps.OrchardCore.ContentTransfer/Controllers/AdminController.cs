using System.Data;
using System.Security.Claims;
using CrestApps.OrchardCore.ContentTransfer.Filters;
using CrestApps.OrchardCore.ContentTransfer.Indexes;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.ContentTransfer.Services;
using CrestApps.OrchardCore.ContentTransfer.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.BackgroundJobs;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using YesSql;
using YesSql.Filters.Query;
using YesSql.Services;
using StatusCodes = Microsoft.AspNetCore.Http.StatusCodes;

namespace CrestApps.OrchardCore.ContentTransfer.Controllers;

public sealed class AdminController : Controller, IUpdateModel
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ISession _session;

    private readonly IDisplayManager<ContentTransferEntry> _entryDisplayManager;
    private readonly IContentTransferEntryAdminListQueryService _entriesAdminListQueryService;
    private readonly IDisplayManager<ListContentTransferEntryOptions> _entryOptionsDisplayManager;
    private readonly INotifier _notifier;
    private readonly IShapeFactory _shapeFactory;
    private readonly PagerOptions _pagerOptions;
    private readonly IClock _clock;
    private readonly IContentTransferFileStore _contentTransferFileStore;
    private readonly IContentManager _contentManager;
    private readonly IDisplayManager<ImportContent> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IContentImportManager _contentImportManager;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentTransferChunkFileUploadService _chunkFileUploadService;
    private readonly IContentTransferFileFormatProvider[] _formatProviders;
    private readonly IContentTransferEntryManager _contentTransferEntryManager;
    private readonly ContentImportOptions _contentImportOptions;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public AdminController(
        IAuthorizationService authorizationService,
        ISession session,
        IShapeFactory shapeFactory,
        IOptions<PagerOptions> pagerOptions,
        IDisplayManager<ContentTransferEntry> entryDisplayManager,
        IContentTransferEntryAdminListQueryService entriesAdminListQueryService,
        IDisplayManager<ListContentTransferEntryOptions> entryOptionsDisplayManager,
        INotifier notifier,
        IStringLocalizer<AdminController> stringLocalizer,
        IContentDefinitionManager contentDefinitionManager,
        IHtmlLocalizer<AdminController> htmlLocalizer,
        IContentTransferFileStore contentTransferFileStore,
        IContentManager contentManager,
        IDisplayManager<ImportContent> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IContentImportManager contentImportManager,
        IContentItemDisplayManager contentItemDisplayManager,
        IContentTransferChunkFileUploadService chunkFileUploadService,
        IEnumerable<IContentTransferFileFormatProvider> formatProviders,
        IContentTransferEntryManager contentTransferEntryManager,
        IOptions<ContentImportOptions> contentImportOptions,
        IClock clock)
    {
        _authorizationService = authorizationService;
        _session = session;
        _entryDisplayManager = entryDisplayManager;
        _entriesAdminListQueryService = entriesAdminListQueryService;
        _entryOptionsDisplayManager = entryOptionsDisplayManager;
        _notifier = notifier;
        S = stringLocalizer;
        _contentDefinitionManager = contentDefinitionManager;
        H = htmlLocalizer;
        _contentTransferFileStore = contentTransferFileStore;
        _contentManager = contentManager;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _contentImportManager = contentImportManager;
        _contentItemDisplayManager = contentItemDisplayManager;
        _chunkFileUploadService = chunkFileUploadService;
        _formatProviders = formatProviders
            .OrderBy(provider => provider.FileExtension, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _contentTransferEntryManager = contentTransferEntryManager;
        _contentImportOptions = contentImportOptions.Value;
        _shapeFactory = shapeFactory;
        _pagerOptions = pagerOptions.Value;
        _clock = clock;
    }

    [Admin("content-transfer-entries", RouteName = "ListContentTransferEntries")]
    public async Task<IActionResult> List(
        [ModelBinder(BinderType = typeof(ContentTransferEntryFilterEngineModelBinder), Name = "q")] QueryFilterResult<ContentTransferEntry> queryFilterResult,
        PagerParameters pagerParameters,
        ListContentTransferEntryOptions options)
    {
        if (!await _authorizationService.AuthorizeAsync(HttpContext.User, ContentTransferPermissions.ListContentTransferEntries))
        {
            return Forbid();
        }

        options.FilterResult = queryFilterResult;
        await PopulateListOptionsAsync(options, ContentTransferDirection.Import);

        return View(await BuildListViewModelAsync(options, pagerParameters));
    }

    [HttpPost]
    [ActionName(nameof(List))]
    [FormValueRequired("submit.Filter")]
    public async Task<ActionResult> ListFilterPOST(ListContentTransferEntryOptions options)
    {
        return await FilterListAsync(nameof(List), options);
    }

    [HttpPost]
    [ActionName(nameof(List))]
    [FormValueRequired("submit.BulkAction")]
    public async Task<ActionResult> ListPOST(ListContentTransferEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(HttpContext.User, ContentTransferPermissions.ListContentTransferEntries))
        {
            return Forbid();
        }

        await ExecuteBulkActionAsync(itemIds, options.BulkAction, ContentTransferDirection.Import);

        return RedirectToAction(nameof(List));
    }

    public async Task<IActionResult> Delete(string entryId, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(HttpContext.User, ContentTransferPermissions.DeleteContentTransferEntries))
        {
            return Forbid();
        }

        var queuedDeletion = false;

        if (!string.IsNullOrWhiteSpace(entryId))
        {
            var entry = await _session.Query<ContentTransferEntry, ContentTransferEntryIndex>(x => x.EntryId == entryId).FirstOrDefaultAsync();

            if (entry != null)
            {
                await _contentTransferEntryManager.MarkAsDeletingAsync(entry.EntryId);
                TriggerEntryDeletion(entry.EntryId);
                queuedDeletion = true;
            }
        }

        if (queuedDeletion)
        {
            await _notifier.InformationAsync(H["The content transfer entry will be deleted in the background shortly."]);
        }

        return RedirectTo(returnUrl);
    }

    [Admin("import/contents/{contentTypeId}", "ImportContentFromFile")]
    public async Task<IActionResult> Import(string contentTypeId)
    {
        var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentTypeId);

        if (contentTypeDefinition == null)
        {
            return NotFound();
        }

        var settings = contentTypeDefinition.GetSettings<ContentTypeTransferSettings>();

        if (!settings.AllowBulkImport)
        {
            return BadRequest();
        }

        if (!await _authorizationService.AuthorizeAsync(User, ContentTransferPermissions.ImportContentFromFile, (object)contentTypeId))
        {
            return Unauthorized();
        }

        var context = new ImportContentContext()
        {
            ContentItem = await _contentManager.NewAsync(contentTypeId),
            ContentTypeDefinition = contentTypeDefinition,
        };

        var columns = await _contentImportManager.GetColumnsAsync(context);

        var importContent = new ImportContent()
        {
            ContentTypeId = contentTypeId,
            ContentTypeName = contentTypeDefinition.Name,
        };

        var viewModel = new ContentImporterViewModel()
        {
            ContentTypeDefinition = contentTypeDefinition,
            Content = await _displayManager.BuildEditorAsync(importContent, _updateModelAccessor.ModelUpdater, true, string.Empty, string.Empty),
            Columns = columns.Where(x => x.Type != ImportColumnType.ExportOnly),
            FileFormats = BuildFileFormatSelectList(),
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ContentTransferUploadSizeLimit]
    [ActionName(nameof(Import))]
    public async Task<IActionResult> ImportPOST(string contentTypeId)
    {
        if (string.IsNullOrEmpty(contentTypeId))
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, ContentTransferPermissions.ImportContentFromFile, (object)contentTypeId))
        {
            return Unauthorized();
        }

        var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentTypeId);

        if (contentTypeDefinition == null)
        {
            return NotFound();
        }

        var settings = contentTypeDefinition.GetSettings<ContentTypeTransferSettings>();

        if (!settings.AllowBulkImport)
        {
            return NotFound();
        }

        return await _chunkFileUploadService.ProcessRequestAsync(
            Request,
            (_, _, _) => Task.FromResult<IActionResult>(Ok(new { })),
            async (files) =>
            {
                var file = files.FirstOrDefault();

                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { error = S["File is required."].Value });
                }

                if (_contentImportOptions.MaxUploadFileSize > 0 && file.Length > _contentImportOptions.MaxUploadFileSize)
                {
                    return BuildUploadErrorResult(ContentTransferUploadError.MaxFileSizeExceeded);
                }

                var extension = Path.GetExtension(file.FileName);
                var formatProvider = ResolveFileFormatProviderByFileName(file.FileName);

                if (formatProvider == null)
                {
                    return BadRequest(new { error = GetUnsupportedFormatsMessage().Value });
                }

                var importContent = new ImportContent()
                {
                    ContentTypeId = contentTypeId,
                    ContentTypeName = contentTypeDefinition.Name,
                };

                await _displayManager.UpdateEditorAsync(importContent, _updateModelAccessor.ModelUpdater, false, string.Empty, string.Empty);

                if (!ModelState.IsValid)
                {
                    return BadRequest(new { error = GetModelErrorMessage() });
                }

                var fileName = Guid.NewGuid() + extension;
                var storedFileName = await _contentTransferFileStore.CreateFileFromStreamAsync(fileName, file.OpenReadStream(), false);

                var entry = new ContentTransferEntry()
                {
                    EntryId = IdGenerator.GenerateId(),
                    ContentType = contentTypeId,
                    Owner = CurrentUserId(),
                    Author = User.Identity.Name,
                    UploadedFileName = file.FileName,
                    StoredFileName = storedFileName,
                    Status = ContentTransferEntryStatus.Pending,
                    Direction = ContentTransferDirection.Import,
                    CreatedUtc = _clock.UtcNow,
                };

                importContent.CopyPropertiesTo(entry);

                _session.Save(entry);
                await _session.SaveChangesAsync();
                TriggerImportProcessing(entry.EntryId);

                return Ok(new { success = true });
            },
            (error) => Task.FromResult(BuildUploadErrorResult(error)));
    }

    [Admin("import/contents/{contentTypeId}/download-template", "ImportContentDownloadTemplateTemplate")]
    public async Task<IActionResult> DownloadTemplate(string contentTypeId, string format = null)
    {
        if (string.IsNullOrEmpty(contentTypeId))
        {
            return NotFound();
        }

        var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentTypeId);

        if (contentTypeDefinition == null)
        {
            return NotFound();
        }

        var settings = contentTypeDefinition.GetSettings<ContentTypeTransferSettings>();

        if (!settings.AllowBulkImport)
        {
            return BadRequest();
        }

        if (!await _authorizationService.AuthorizeAsync(User, ContentTransferPermissions.ImportContentFromFile, (object)contentTypeId))
        {
            return Unauthorized();
        }

        var context = new ImportContentContext()
        {
            ContentItem = await _contentManager.NewAsync(contentTypeId),
            ContentTypeDefinition = contentTypeDefinition,
        };

        var columns = await _contentImportManager.GetColumnsAsync(context);
        var importColumns = columns.Where(c => c.Type != ImportColumnType.ExportOnly).Select(c => c.Name).ToList();

        var formatProvider = ResolveFileFormatProvider(format);

        if (formatProvider == null)
        {
            return BadRequest(S["No file formats are currently enabled for bulk import."]);
        }

        var content = new MemoryStream();

        using (var writer = formatProvider.CreateWriter(content, contentTypeDefinition.DisplayName))
        {
            writer.WriteHeader(importColumns);
            writer.Flush();
        }

        content.Seek(0, SeekOrigin.Begin);

        return new FileStreamResult(content, formatProvider.ContentType)
        {
            FileDownloadName = $"{contentTypeDefinition.Name}_Template{formatProvider.FileExtension}",
        };
    }

    [Admin("export/contents", "ExportContentToFile")]
    public async Task<IActionResult> Export(
        [ModelBinder(BinderType = typeof(ContentTransferEntryFilterEngineModelBinder), Name = "q")] QueryFilterResult<ContentTransferEntry> queryFilterResult,
        PagerParameters pagerParameters,
        ListContentTransferEntryOptions options)
    {
        if (!await _authorizationService.AuthorizeAsync(HttpContext.User, ContentTransferPermissions.ExportContentFromFile))
        {
            return Forbid();
        }

        options.FilterResult = queryFilterResult;
        await PopulateListOptionsAsync(options, ContentTransferDirection.Export, CurrentUserId());

        return View(await BuildBulkExportViewModelAsync(options, pagerParameters));
    }

    [HttpPost]
    [ActionName(nameof(Export))]
    [FormValueRequired("submit.Filter")]
    public async Task<ActionResult> ExportFilterPOST(ListContentTransferEntryOptions options)
    {
        if (!await _authorizationService.AuthorizeAsync(HttpContext.User, ContentTransferPermissions.ExportContentFromFile))
        {
            return Forbid();
        }

        return await FilterListAsync(nameof(Export), options);
    }

    [HttpPost]
    [ActionName(nameof(Export))]
    [FormValueRequired("submit.BulkAction")]
    public async Task<ActionResult> ExportPOST(ListContentTransferEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(HttpContext.User, ContentTransferPermissions.ExportContentFromFile))
        {
            return Forbid();
        }

        await ExecuteBulkActionAsync(itemIds, options.BulkAction, ContentTransferDirection.Export, CurrentUserId());

        return RedirectToAction(nameof(Export));
    }

    [Admin("export/contents/download-file", "ExportContentDownloadFile")]
    public async Task<IActionResult> DownloadExport(
        string contentTypeId,
        string extension = null,
        bool partialExport = false,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        DateTime? modifiedFrom = null,
        DateTime? modifiedTo = null,
        string owners = null,
        bool publishedOnly = true,
        bool latestOnly = false,
        bool allVersions = false)
    {
        var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentTypeId);

        if (contentTypeDefinition == null)
        {
            return NotFound();
        }

        var settings = contentTypeDefinition.GetSettings<ContentTypeTransferSettings>();

        if (!settings.AllowBulkExport)
        {
            return BadRequest();
        }

        if (!await _authorizationService.AuthorizeAsync(User, ContentTransferPermissions.ExportContentFromFile, (object)contentTypeId))
        {
            return Unauthorized();
        }

        var context = new ImportContentContext()
        {
            ContentItem = await _contentManager.NewAsync(contentTypeId),
            ContentTypeDefinition = contentTypeDefinition,
        };

        var columns = await _contentImportManager.GetColumnsAsync(context);
        var exportColumns = columns.Where(x => x.Type != ImportColumnType.ImportOnly).ToList();

        // Build a filtered query for counting.
        var countQuery = BuildExportQuery(contentTypeId, partialExport, latestOnly, allVersions, createdFrom, createdTo, modifiedFrom, modifiedTo, owners);
        var totalCount = await countQuery.CountAsync();

        var threshold = _contentImportOptions.ExportQueueThreshold;
        var formatProvider = ResolveFileFormatProvider(extension);

        if (formatProvider == null)
        {
            return BadRequest(S["No file formats are currently enabled for bulk export."]);
        }

        if (totalCount > threshold)
        {
            // Queue the export for background processing.
            var fileName = $"{contentTypeDefinition.Name}_Export_{Guid.NewGuid():N}{formatProvider.FileExtension}";

            var entry = new ContentTransferEntry()
            {
                EntryId = IdGenerator.GenerateId(),
                ContentType = contentTypeId,
                Owner = CurrentUserId(),
                Author = User.Identity.Name,
                UploadedFileName = $"{contentTypeDefinition.Name}_Export{formatProvider.FileExtension}",
                StoredFileName = fileName,
                Status = ContentTransferEntryStatus.New,
                Direction = ContentTransferDirection.Export,
                CreatedUtc = _clock.UtcNow,
            };

            // Store the filters so the background task can apply them.
            if (partialExport)
            {
                entry.Put(new ExportFilterPart
                {
                    PublishedOnly = publishedOnly,
                    LatestOnly = latestOnly,
                    AllVersions = allVersions,
                    CreatedFrom = createdFrom,
                    CreatedTo = createdTo,
                    ModifiedFrom = modifiedFrom,
                    ModifiedTo = modifiedTo,
                    Owners = owners,
                });
            }

            _session.Save(entry);
            await _session.SaveChangesAsync();
            TriggerExportProcessing(entry.EntryId);

            await _notifier.InformationAsync(H["The export contains {0} records and has been queued for background processing. You can download it from Bulk Export when it is ready.", totalCount]);

            return RedirectToAction(nameof(Export));
        }

        // Immediate export: write directly to a temp file stream using pagination.
        var batchSize = _contentImportOptions.ExportBatchSize < 1 ? 200 : _contentImportOptions.ExportBatchSize;
        var columnNames = exportColumns.Select(c => c.Name).ToList();

        var tempFilePath = Path.GetTempFileName();
        try
        {
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var writer = formatProvider.CreateWriter(fileStream, contentTypeDefinition.DisplayName))
            {
                writer.WriteHeader(columnNames);

                // Paginate content items and write each page directly.
                var page = 0;

                while (true)
                {
                    var pageQuery = BuildExportQuery(contentTypeId, partialExport, latestOnly, allVersions, createdFrom, createdTo, modifiedFrom, modifiedTo, owners);

                    var contentItems = await pageQuery
                        .Skip(page * batchSize)
                        .Take(batchSize)
                        .ListAsync();

                    var items = contentItems.ToList();

                    if (items.Count == 0)
                    {
                        break;
                    }

                    // Create a temporary DataTable for this batch only.
                    using var dataTable = new DataTable();

                    foreach (var colName in columnNames)
                    {
                        dataTable.Columns.Add(colName);
                    }

                    foreach (var contentItem in items)
                    {
                        var mapContext = new ContentExportContext()
                        {
                            ContentItem = contentItem,
                            ContentTypeDefinition = contentTypeDefinition,
                            Row = dataTable.NewRow(),
                        };

                        await _contentImportManager.ExportAsync(mapContext);

                        var rowValues = new List<string>(columnNames.Count);
                        foreach (var colName in columnNames)
                        {
                            rowValues.Add(mapContext.Row[colName]?.ToString() ?? string.Empty);
                        }

                        writer.WriteRow(rowValues);
                    }

                    page++;
                }

                writer.Flush();
            }

            // Read back from temp file for download (file-based, not memory-based).
            var downloadStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, options: FileOptions.DeleteOnClose);

            return new FileStreamResult(downloadStream, formatProvider.ContentType)
            {
                FileDownloadName = $"{contentTypeDefinition.Name}_Export{formatProvider.FileExtension}",
            };
        }
        catch
        {
            if (System.IO.File.Exists(tempFilePath))
            {
                System.IO.File.Delete(tempFilePath);
            }

            throw;
        }
    }

    [Admin("export/dashboard", "ExportDashboard")]
    public Task<IActionResult> ExportDashboard(
        [ModelBinder(BinderType = typeof(ContentTransferEntryFilterEngineModelBinder), Name = "q")] QueryFilterResult<ContentTransferEntry> queryFilterResult,
        PagerParameters pagerParameters,
        ListContentTransferEntryOptions options)
    {
        return Export(queryFilterResult, pagerParameters, options);
    }

    [Admin("import/entries/{entryId}/resume", "ResumeImport")]
    public async Task<IActionResult> ResumeImport(string entryId, string returnUrl)
    {
        if (string.IsNullOrEmpty(entryId))
        {
            return NotFound();
        }

        var entry = await _session.Query<ContentTransferEntry, ContentTransferEntryIndex>(x =>
                x.EntryId == entryId
                && x.Direction == ContentTransferDirection.Import
                && x.Owner == CurrentUserId())
            .FirstOrDefaultAsync();

        if (entry == null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, ContentTransferPermissions.ImportContentFromFile, (object)entry.ContentType))
        {
            return Forbid();
        }

        var isStalled = IsStalled(entry);

        if (!entry.Status.CanResumeImport() && !isStalled)
        {
            await _notifier.WarningAsync(H["Only pending, paused, failed, or stalled import files can be resumed."]);
            return RedirectTo(returnUrl);
        }

        await _contentTransferEntryManager.ResumeImportAsync(entry.EntryId);
        TriggerImportProcessing(entry.EntryId);
        await _notifier.SuccessAsync(H["The import will resume in the background shortly."]);

        return RedirectTo(returnUrl);
    }

    [Admin("import/entries/{entryId}/pause", "PauseImport")]
    public async Task<IActionResult> PauseImport(string entryId, string returnUrl)
    {
        if (string.IsNullOrEmpty(entryId))
        {
            return NotFound();
        }

        var entry = await _session.Query<ContentTransferEntry, ContentTransferEntryIndex>(x =>
                x.EntryId == entryId
                && x.Direction == ContentTransferDirection.Import
                && x.Owner == CurrentUserId())
            .FirstOrDefaultAsync();

        if (entry == null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, ContentTransferPermissions.ImportContentFromFile, (object)entry.ContentType))
        {
            return Forbid();
        }

        if (entry.Status != ContentTransferEntryStatus.Processing)
        {
            await _notifier.WarningAsync(H["Only processing import files can be paused."]);
            return RedirectTo(returnUrl);
        }

        await _contentTransferEntryManager.PauseImportAsync(entry.EntryId);
        await _notifier.SuccessAsync(H["The import has been paused."]);

        return RedirectTo(returnUrl);
    }

    [Admin("export/dashboard/{entryId}/download", "DownloadExportFile")]
    public async Task<IActionResult> DownloadExportFile(string entryId)
    {
        if (string.IsNullOrEmpty(entryId))
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(HttpContext.User, ContentTransferPermissions.ExportContentFromFile))
        {
            return Forbid();
        }

        var entry = await _session.Query<ContentTransferEntry, ContentTransferEntryIndex>(x =>
            x.EntryId == entryId
            && x.Direction == ContentTransferDirection.Export
            && x.Owner == CurrentUserId())
            .FirstOrDefaultAsync();

        if (entry == null || entry.Status != ContentTransferEntryStatus.Completed)
        {
            return NotFound();
        }

        var fileInfo = await _contentTransferFileStore.GetFileInfoAsync(entry.StoredFileName);

        if (fileInfo == null || fileInfo.Length == 0)
        {
            await _notifier.ErrorAsync(H["The export file is no longer available."]);
            return RedirectToAction(nameof(Export));
        }

        var formatProvider = ResolveFileFormatProviderByFileName(entry.StoredFileName);

        if (formatProvider == null)
        {
            await _notifier.ErrorAsync(H["The file format for this export is no longer enabled."]);
            return RedirectToAction(nameof(Export));
        }

        var stream = await _contentTransferFileStore.GetFileStreamAsync(fileInfo);

        return new FileStreamResult(stream, formatProvider.ContentType)
        {
            FileDownloadName = entry.UploadedFileName ?? $"{entry.ContentType}_Export{formatProvider.FileExtension}",
        };
    }

    [Admin("import/entries/{entryId}/download-errors", "DownloadErrors")]
    public async Task<IActionResult> DownloadErrors(string entryId)
    {
        if (string.IsNullOrEmpty(entryId))
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(HttpContext.User, ContentTransferPermissions.ImportContentFromFile))
        {
            return Forbid();
        }

        var entry = await _session.Query<ContentTransferEntry, ContentTransferEntryIndex>(x =>
            x.EntryId == entryId
            && x.Direction == ContentTransferDirection.Import
            && x.Owner == CurrentUserId())
            .FirstOrDefaultAsync();

        if (entry == null)
        {
            return NotFound();
        }

        if (!entry.TryGet<ImportFileProcessStatsPart>(out var statsPart)
            || statsPart.Errors == null
            || statsPart.Errors.Count == 0)
        {
            await _notifier.WarningAsync(H["No error records found for this entry."]);
            return RedirectToAction(nameof(List));
        }

        var fileInfo = await _contentTransferFileStore.GetFileInfoAsync(entry.StoredFileName);

        if (fileInfo == null || fileInfo.Length == 0)
        {
            await _notifier.ErrorAsync(H["The original import file is no longer available."]);
            return RedirectToAction(nameof(List));
        }

        var formatProvider = ResolveFileFormatProviderByFileName(entry.StoredFileName);

        if (formatProvider == null)
        {
            await _notifier.ErrorAsync(H["The file format for this import is no longer enabled."]);
            return RedirectToAction(nameof(List));
        }

        await using var sourceStream = await _contentTransferFileStore.GetFileStreamAsync(fileInfo);
        var outputStream = new MemoryStream();
        statsPart.ErrorMessages ??= [];

        using (var reader = formatProvider.CreateReader(sourceStream))
        using (var writer = formatProvider.CreateWriter(outputStream, entry.ContentType))
        {
            var columnNames = reader.GetColumnNames().ToList();
            columnNames.Add(S["Errors"]);
            writer.WriteHeader(columnNames);

            var rowIndex = 1;

            foreach (var rowValues in reader.ReadRows())
            {
                if (!statsPart.Errors.Contains(rowIndex))
                {
                    rowIndex++;
                    continue;
                }

                statsPart.ErrorMessages.TryGetValue(rowIndex, out var errorMessage);

                var values = rowValues.ToList();
                values.Add(errorMessage ?? string.Empty);
                writer.WriteRow(values);
                rowIndex++;
            }

            writer.Flush();
        }

        outputStream.Position = 0;

        return new FileStreamResult(outputStream, formatProvider.ContentType)
        {
            FileDownloadName = $"{entry.ContentType}_Errors{formatProvider.FileExtension}",
        };
    }

    private async Task PopulateListOptionsAsync(ListContentTransferEntryOptions options, ContentTransferDirection direction, string owner = null)
    {
        options.SearchText = options.FilterResult.ToString();
        options.OriginalSearchText = options.SearchText;
        options.Direction = direction;
        options.Owner = owner;
        options.RouteValues.TryAdd("q", options.FilterResult.ToString());

        options.Statuses =
        [
            new(S["Pending"], nameof(ContentTransferEntryStatus.Pending)),
            new(S["Processing"], nameof(ContentTransferEntryStatus.Processing)),
            new(S["Completed"], nameof(ContentTransferEntryStatus.Completed)),
            new(S["Completed with errors"], nameof(ContentTransferEntryStatus.CompletedWithErrors)),
            new(S["Paused"], nameof(ContentTransferEntryStatus.Paused)),
            new(S["Deleting"], nameof(ContentTransferEntryStatus.Deleting)),
            new(S["Failed"], nameof(ContentTransferEntryStatus.Failed)),
        ];

        options.Sorts =
        [
            new(S["Recently created"], nameof(ContentTransferEntryOrder.Latest)),
            new(S["Previously created"], nameof(ContentTransferEntryOrder.Oldest)),
        ];

        options.BulkActions =
        [
            new(S["Remove"], nameof(ContentTransferEntryBulkAction.Remove)),
        ];

        options.ImportableTypes = direction == ContentTransferDirection.Import
            ? await GetTransferableContentTypesAsync(ContentTransferDirection.Import)
            : [];
        options.ExportableTypes = direction == ContentTransferDirection.Export
            ? await GetTransferableContentTypesAsync(ContentTransferDirection.Export)
            : [];
    }

    private async Task<IList<SelectListItem>> GetTransferableContentTypesAsync(ContentTransferDirection direction)
    {
        var contentTypes = new List<SelectListItem>();

        foreach (var contentTypeDefinition in await _contentDefinitionManager.ListTypeDefinitionsAsync())
        {
            var settings = contentTypeDefinition.GetSettings<ContentTypeTransferSettings>();
            var isAllowed = direction == ContentTransferDirection.Import
                ? settings.AllowBulkImport
                : settings.AllowBulkExport;
            var permission = direction == ContentTransferDirection.Import
                ? ContentTransferPermissions.ImportContentFromFile
                : ContentTransferPermissions.ExportContentFromFile;

            if (!isAllowed || !await _authorizationService.AuthorizeAsync(HttpContext.User, permission, (object)contentTypeDefinition.Name))
            {
                continue;
            }

            contentTypes.Add(new SelectListItem(contentTypeDefinition.DisplayName, contentTypeDefinition.Name));
        }

        return contentTypes
            .OrderBy(item => item.Text ?? item.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IShape> BuildListViewModelAsync(ListContentTransferEntryOptions options, PagerParameters pagerParameters)
    {
        var routeData = new RouteData(options.RouteValues);
        var pager = new Pager(pagerParameters, _pagerOptions.GetPageSize());

        var queryResult = await _entriesAdminListQueryService.QueryAsync(pager.Page, pager.PageSize, options, this);
        var pagerShape = await _shapeFactory.PagerAsync(pager, queryResult.TotalCount, routeData);

        var summaries = new List<dynamic>();

        foreach (var entry in queryResult.Entries)
        {
            dynamic shape = await _entryDisplayManager.BuildDisplayAsync(entry, this, "SummaryAdmin");
            shape.ContentTransferEntry = entry;
            summaries.Add(shape);
        }

        var startIndex = (pager.Page - 1) * pager.PageSize + 1;
        options.StartIndex = queryResult.TotalCount == 0 ? 0 : startIndex;
        options.EndIndex = queryResult.TotalCount == 0 ? 0 : startIndex + summaries.Count - 1;
        options.EntriesCount = summaries.Count;
        options.TotalItemCount = queryResult.TotalCount;

        var header = await _entryOptionsDisplayManager.BuildEditorAsync(options, this, false, string.Empty, string.Empty);

        return await _shapeFactory.CreateAsync<ListContentTransferEntriesViewModel>("ContentTransferEntriesAdminList", viewModel =>
        {
            viewModel.Options = options;
            viewModel.Header = header;
            viewModel.Entries = summaries;
            viewModel.Pager = pagerShape;
        });
    }

    private ContentExporterViewModel BuildContentExporterViewModel(IList<SelectListItem> exportableTypes)
    {
        var formats = BuildFileFormatSelectList();

        return new()
        {
            ContentTypes = exportableTypes,
            Extensions = formats,
            Extension = formats.Count > 0 ? formats[0].Value : null,
        };
    }

    private async Task<BulkExportViewModel> BuildBulkExportViewModelAsync(ListContentTransferEntryOptions options, PagerParameters pagerParameters)
    {
        return new BulkExportViewModel()
        {
            Exporter = BuildContentExporterViewModel(options.ExportableTypes),
            List = await BuildListViewModelAsync(options, pagerParameters),
        };
    }

    private async Task<ActionResult> FilterListAsync(string actionName, ListContentTransferEntryOptions options)
    {
        if (!string.Equals(options.SearchText, options.OriginalSearchText, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(actionName, new RouteValueDictionary
            {
                { "q", options.SearchText },
            });
        }

        await _entryOptionsDisplayManager.UpdateEditorAsync(options, this, false, string.Empty, string.Empty);
        options.RouteValues.TryAdd("q", options.FilterResult.ToString());

        return RedirectToAction(actionName, options.RouteValues);
    }

    private async Task ExecuteBulkActionAsync(IEnumerable<string> itemIds, ContentTransferEntryBulkAction? bulkAction, ContentTransferDirection direction, string owner = null)
    {
        if (itemIds?.Any() != true)
        {
            return;
        }

        var query = _session.Query<ContentTransferEntry, ContentTransferEntryIndex>(x =>
            x.EntryId.IsIn(itemIds) && x.Direction == direction);

        if (!string.IsNullOrWhiteSpace(owner))
        {
            query = query.Where(x => x.Owner == owner);
        }

        var entries = await query.ListAsync();

        var deletedCount = 0;

        switch (bulkAction)
        {
            case ContentTransferEntryBulkAction.Remove:
                foreach (var entry in entries)
                {
                    await _contentTransferEntryManager.MarkAsDeletingAsync(entry.EntryId);
                    TriggerEntryDeletion(entry.EntryId);
                    deletedCount++;
                }

                if (deletedCount > 0)
                {
                    await _notifier.SuccessAsync(H["{0} {1} queued for background deletion.", deletedCount, H.Plural(deletedCount, "entry", "entries")]);
                }
                break;
        }
    }

    private bool IsStalled(ContentTransferEntry entry)
        => entry.Status == ContentTransferEntryStatus.Processing
            && entry.ProcessSaveUtc.HasValue
            && (_clock.UtcNow - entry.ProcessSaveUtc.Value).TotalMinutes > 10;

    private static void TriggerImportProcessing(string entryId)
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(
                $"content-transfer-import-{entryId}",
                entryId,
                static (backgroundScope, id) => BackgroundTasks.ImportFilesBackgroundTask.ProcessEntriesAsync(backgroundScope.ServiceProvider, CancellationToken.None, id));
        });
    }

    private static void TriggerExportProcessing(string entryId)
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(
                $"content-transfer-export-{entryId}",
                entryId,
                static (backgroundScope, id) => BackgroundTasks.ExportFilesBackgroundTask.ProcessEntriesAsync(backgroundScope.ServiceProvider, CancellationToken.None, id));
        });
    }

    private static void TriggerEntryDeletion(string entryId)
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync(
                $"content-transfer-delete-{entryId}",
                entryId,
                static async (backgroundScope, id) =>
                {
                    var manager = backgroundScope.ServiceProvider.GetRequiredService<IContentTransferEntryManager>();
                    await manager.DeleteAsync(id);
                });
        });
    }

    private IContentTransferFileFormatProvider ResolveFileFormatProvider(string format)
    {
        if (!string.IsNullOrEmpty(format))
        {
            var extension = format.StartsWith('.') ? format : "." + format;
            var provider = _formatProviders.FirstOrDefault(p => p.FileExtension.Equals(extension, StringComparison.OrdinalIgnoreCase));

            if (provider != null)
            {
                return provider;
            }
        }

        return _formatProviders.Length > 0 ? _formatProviders[0] : null;
    }

    private IContentTransferFileFormatProvider ResolveFileFormatProviderByFileName(string fileName)
        => _formatProviders.FirstOrDefault(provider => provider.CanHandle(fileName));

    private List<SelectListItem> BuildFileFormatSelectList()
        => _formatProviders
            .Select(provider => new SelectListItem(GetFileFormatLabel(provider), provider.FileExtension))
            .ToList();

    private string GetFileFormatExtensions()
        => string.Join(", ", _formatProviders.Select(provider => provider.FileExtension));

    private LocalizedString GetUnsupportedFormatsMessage()
        => _formatProviders.Length == 0
            ? S["No file formats are currently enabled."]
            : S["Only the enabled file formats are supported: {0}.", GetFileFormatExtensions()];

    private static string GetFileFormatLabel(IContentTransferFileFormatProvider provider)
        => provider.FileExtension.TrimStart('.').ToUpperInvariant();

    private string GetModelErrorMessage()
        => ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? error.Exception?.Message
                : error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
            ?? S["The import request is invalid."].Value;

    private IActionResult BuildUploadErrorResult(ContentTransferUploadError error)
    {
        switch (error)
        {
            case ContentTransferUploadError.MaxFileSizeExceeded:
                return StatusCode(
                    StatusCodes.Status413PayloadTooLarge,
                    new { error = S["The file exceeds the maximum allowed size of {0}.", FormatFileSize(_contentImportOptions.MaxUploadFileSize)].Value });

            case ContentTransferUploadError.MaxChunkSizeExceeded:
                return StatusCode(
                    StatusCodes.Status413PayloadTooLarge,
                    new { error = S["The upload could not be completed because a chunk exceeded the allowed size."].Value });

            default:
                return BadRequest(new { error = S["The upload request was invalid. Please try again."].Value });
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["bytes", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    private IQuery<ContentItem> BuildExportQuery(
        string contentTypeId,
        bool partialExport,
        bool latestOnly,
        bool allVersions,
        DateTime? createdFrom,
        DateTime? createdTo,
        DateTime? modifiedFrom,
        DateTime? modifiedTo,
        string owners)
    {
        IQuery<ContentItem, ContentItemIndex> query;

        if (allVersions)
        {
            query = _session.Query<ContentItem, ContentItemIndex>(x => x.ContentType == contentTypeId);
        }
        else if (latestOnly)
        {
            query = _session.Query<ContentItem, ContentItemIndex>(x => x.ContentType == contentTypeId && x.Latest);
        }
        else
        {
            // Default: published only.
            query = _session.Query<ContentItem, ContentItemIndex>(x => x.ContentType == contentTypeId && x.Published);
        }

        if (partialExport)
        {
            if (createdFrom.HasValue)
            {
                query = query.Where(x => x.CreatedUtc >= createdFrom.Value);
            }

            if (createdTo.HasValue)
            {
                query = query.Where(x => x.CreatedUtc <= createdTo.Value);
            }

            if (modifiedFrom.HasValue)
            {
                query = query.Where(x => x.ModifiedUtc >= modifiedFrom.Value);
            }

            if (modifiedTo.HasValue)
            {
                query = query.Where(x => x.ModifiedUtc <= modifiedTo.Value);
            }

            if (!string.IsNullOrWhiteSpace(owners))
            {
                var ownerList = owners.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (ownerList.Length == 1)
                {
                    var owner = ownerList[0];
                    query = query.Where(x => x.Owner == owner);
                }
                else if (ownerList.Length > 1)
                {
                    // Capture into locals (safe for expression trees, supports up to 5 owners).
                    var o0 = ownerList[0];
                    var o1 = ownerList.ElementAtOrDefault(1) ?? o0;
                    var o2 = ownerList.ElementAtOrDefault(2) ?? o0;
                    var o3 = ownerList.ElementAtOrDefault(3) ?? o0;
                    var o4 = ownerList.ElementAtOrDefault(4) ?? o0;

                    query = query.Where(x =>
                        x.Owner == o0 || x.Owner == o1 || x.Owner == o2 ||
                        x.Owner == o3 || x.Owner == o4);
                }
            }
        }

        return query.OrderBy(x => x.CreatedUtc);
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private IActionResult RedirectTo(string returnUrl)
    {
        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? (IActionResult)this.LocalRedirect(returnUrl, true)
            : RedirectToAction(nameof(List));
    }
}
