using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Downloads.Services;
using CrestApps.OrchardCore.Downloads.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Downloads.Controllers;

/// <summary>
/// Where a buyer's download link leads. Opening it shows the file with a Download button; the download itself is a
/// post, so the link checkers some mail services run on every link never use up a download. The page is the
/// <c>DownloadPage</c> shape, so a theme can override it.
/// </summary>
[AllowAnonymous]
public sealed class DownloadsController : Controller
{
    private static readonly FileExtensionContentTypeProvider _contentTypes = new();

    private readonly DownloadService _downloads;
    private readonly IDigitalFileStore _fileStore;
    private readonly IShapeFactory _shapeFactory;

    public DownloadsController(DownloadService downloads, IDigitalFileStore fileStore, IShapeFactory shapeFactory)
    {
        _downloads = downloads;
        _fileStore = fileStore;
        _shapeFactory = shapeFactory;
    }

    [HttpGet("downloads/{token}", Name = DownloadsConstants.RouteNames.Download)]
    public async Task<IActionResult> Index(string token)
    {
        var (entitlement, problem) = await _downloads.ResolveAsync(token, HttpContext.RequestAborted);

        return await PageAsync(token, entitlement, problem);
    }

    [HttpPost("downloads/{token}")]
    [ActionName(nameof(Index))]
    public async Task<IActionResult> Download(string token)
    {
        var (entitlement, problem) = await _downloads.ResolveAsync(token, HttpContext.RequestAborted);

        if (problem != DownloadProblem.None)
        {
            return await PageAsync(token, entitlement, problem);
        }

        var stream = await _fileStore.OpenAsync(entitlement.FileId, HttpContext.RequestAborted);

        if (stream is null)
        {
            return await PageAsync(token, entitlement, DownloadProblem.NotFound);
        }

        if (!await _downloads.RecordDownloadAsync(entitlement, HttpContext.RequestAborted))
        {
            await stream.DisposeAsync();

            var (current, now) = await _downloads.ResolveAsync(token, HttpContext.RequestAborted);

            return await PageAsync(token, current, now == DownloadProblem.None ? DownloadProblem.LimitReached : now);
        }

        if (!_contentTypes.TryGetContentType(entitlement.FileName ?? string.Empty, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return File(stream, contentType, entitlement.FileName);
    }

    private async Task<IActionResult> PageAsync(string token, DownloadEntitlement entitlement, DownloadProblem problem)
    {
        if (problem != DownloadProblem.None)
        {
            Response.StatusCode = problem == DownloadProblem.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status410Gone;
        }

        return View("Index", await _shapeFactory.CreateAsync<DownloadPageViewModel>("DownloadPage", model =>
        {
            model.Token = token;
            model.Entitlement = problem == DownloadProblem.NotFound ? null : entitlement;
            model.Problem = problem;
        }));
    }
}
