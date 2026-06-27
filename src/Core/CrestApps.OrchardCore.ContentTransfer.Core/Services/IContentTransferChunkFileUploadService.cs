using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.OrchardCore.ContentTransfer.Services;

/// <summary>
/// Handles chunked and whole-file uploads for content transfer bulk imports.
/// Unlike the shared media chunk upload service, this service uses the content transfer specific
/// upload limits so large imports can be supported without changing the global media settings.
/// Each request is bounded to a single chunk while the assembled file is validated against the
/// configured maximum file size.
/// </summary>
public interface IContentTransferChunkFileUploadService
{
    /// <summary>
    /// Processes an import upload request. When the request carries a content range header the file is
    /// streamed to a temporary file in chunks; otherwise the request is treated as a single whole-file
    /// upload.
    /// </summary>
    /// <param name="request">The current HTTP request containing the uploaded file or chunk.</param>
    /// <param name="chunkAsync">Invoked when an intermediate chunk has been stored and more chunks are expected.</param>
    /// <param name="completedAsync">Invoked once the whole file is available, receiving the assembled file or files.</param>
    /// <param name="invalidAsync">Invoked when the request is rejected, receiving the reason for the rejection.</param>
    /// <returns>The action result produced by the matching callback.</returns>
    Task<IActionResult> ProcessRequestAsync(
        HttpRequest request,
        Func<Guid, IFormFile, ContentRangeHeaderValue, Task<IActionResult>> chunkAsync,
        Func<IEnumerable<IFormFile>, Task<IActionResult>> completedAsync,
        Func<ContentTransferUploadError, Task<IActionResult>> invalidAsync);

    /// <summary>
    /// Removes temporary chunk files for this tenant that are older than the configured lifetime.
    /// </summary>
    void PurgeTempDirectory();
}
