using CrestApps.OrchardCore.Downloads.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.Downloads.Controllers;

/// <summary>
/// Puts files into the private store from a product's editor, lets the store check them, and manages a buyer's
/// downloads from the order's page.
/// </summary>
public sealed class DigitalFilesAdminController : Controller
{
    private static readonly FileExtensionContentTypeProvider _contentTypes = new();

    private readonly IDigitalFileStore _fileStore;
    private readonly DownloadService _downloads;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly ILogger _logger;

    internal readonly IHtmlLocalizer H;

    public DigitalFilesAdminController(
        IDigitalFileStore fileStore,
        DownloadService downloads,
        IAuthorizationService authorizationService,
        INotifier notifier,
        ILogger<DigitalFilesAdminController> logger,
        IHtmlLocalizer<DigitalFilesAdminController> htmlLocalizer)
    {
        _fileStore = fileStore;
        _downloads = downloads;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _logger = logger;
        H = htmlLocalizer;
    }

    [HttpPost]
    [Admin("downloads/files/upload", "DigitalFilesUpload")]
    [RequestSizeLimit(DownloadsConstants.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = DownloadsConstants.MaxUploadBytes)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DownloadsPermissions.ManageDownloads))
        {
            return Forbid();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "Choose a file to upload." });
        }

        StoredDigitalFile stored;

        try
        {
            await using var stream = file.OpenReadStream();
            stored = await _fileStore.SaveAsync(file.FileName, stream, HttpContext.RequestAborted);
        }
        catch (FileStoreException ex)
        {
            _logger.LogWarning("The digital file '{FileName}' was refused: {Reason}", file.FileName, ex.Message);

            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A scanner that can't be reached throws; the file is refused rather than kept unchecked.
            _logger.LogError(ex, "The digital file '{FileName}' couldn't be checked, so it wasn't kept.", file.FileName);

            return BadRequest(new { error = "The file couldn't be checked, so it wasn't kept. Try again later." });
        }

        return Ok(new { fileId = stored.FileId, fileName = stored.FileName, size = stored.Size });
    }

    [Admin("downloads/files/{fileId}", "DigitalFilesFile")]
    public async Task<IActionResult> File(string fileId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DownloadsPermissions.ManageDownloads))
        {
            return Forbid();
        }

        var file = await _fileStore.FindAsync(fileId, HttpContext.RequestAborted);
        var stream = file is null ? null : await _fileStore.OpenAsync(fileId, HttpContext.RequestAborted);

        if (stream is null)
        {
            return NotFound();
        }

        if (!_contentTypes.TryGetContentType(file.FileName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return File(stream, contentType, file.FileName);
    }

    [HttpPost]
    [Admin("downloads/{itemId}/revoke", "DownloadsRevoke")]
    public async Task<IActionResult> Revoke(string itemId, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DownloadsPermissions.ManageDownloads))
        {
            return Forbid();
        }

        if (await _downloads.FindAsync(itemId, HttpContext.RequestAborted) is { } entitlement)
        {
            await _downloads.RevokeAsync(entitlement, $"Taken back by {User.Identity?.Name}.", HttpContext.RequestAborted);
            await _notifier.SuccessAsync(H["The buyer can no longer download {0}.", entitlement.FileName]);
        }

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "~/");
    }

    [HttpPost]
    [Admin("downloads/{itemId}/reset", "DownloadsReset")]
    public async Task<IActionResult> Reset(string itemId, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DownloadsPermissions.ManageDownloads))
        {
            return Forbid();
        }

        if (await _downloads.FindAsync(itemId, HttpContext.RequestAborted) is { } entitlement)
        {
            await _downloads.ResetAsync(entitlement, HttpContext.RequestAborted);
            await _notifier.SuccessAsync(H["The buyer can download {0} again.", entitlement.FileName]);
        }

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "~/");
    }
}
