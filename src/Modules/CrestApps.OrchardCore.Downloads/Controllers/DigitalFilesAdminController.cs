using CrestApps.OrchardCore.Downloads.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.StaticFiles;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

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

    internal readonly IHtmlLocalizer H;

    public DigitalFilesAdminController(
        IDigitalFileStore fileStore,
        DownloadService downloads,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<DigitalFilesAdminController> htmlLocalizer)
    {
        _fileStore = fileStore;
        _downloads = downloads;
        _authorizationService = authorizationService;
        _notifier = notifier;
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

        await using var stream = file.OpenReadStream();
        var stored = await _fileStore.SaveAsync(file.FileName, stream, HttpContext.RequestAborted);

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
