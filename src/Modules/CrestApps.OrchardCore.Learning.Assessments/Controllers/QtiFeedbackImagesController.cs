using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Assessments.Core.Qti;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.OrchardCore.Learning.Assessments.Controllers;

/// <summary>
/// Serves the pictures of imported QTI feedback to the people <see cref="QtiFeedbackImageAccess"/> allows; anyone else
/// gets 404, so the address tells nothing.
/// </summary>
public sealed class QtiFeedbackImagesController : Controller
{
    private static readonly Dictionary<string, string> _contentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
    };

    private readonly IProtectedFileStore<QtiFeedbackImages> _files;
    private readonly QtiFeedbackImageAccess _access;

    /// <summary>
    /// Initializes a new instance of the <see cref="QtiFeedbackImagesController"/> class.
    /// </summary>
    public QtiFeedbackImagesController(IProtectedFileStore<QtiFeedbackImages> files, QtiFeedbackImageAccess access)
    {
        _files = files;
        _access = access;
    }

    /// <summary>
    /// Serves a picture.
    /// </summary>
    /// <param name="fileId">The protected file id.</param>
    [HttpGet(QtiFeedbackImages.RoutePath + "/{fileId}", Name = "LearningQtiFeedbackImage")]
    public async Task<IActionResult> Show(string fileId)
    {
        if (!await _access.CanReadAsync(User, fileId, HttpContext.RequestAborted))
        {
            return NotFound();
        }

        var file = await _files.FindAsync(fileId, HttpContext.RequestAborted);

        if (file is null || !_contentTypes.TryGetValue(Path.GetExtension(file.FileName) ?? string.Empty, out var contentType))
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";

        return File(await _files.OpenAsync(fileId, HttpContext.RequestAborted), contentType);
    }
}
