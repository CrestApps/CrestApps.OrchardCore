using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.Mvc.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Admin")]
[Route("Admin/[controller]")]
public sealed class AIDocumentController : Controller
{
    private readonly IAIDocumentStore _documentStore;
    private readonly IAIDocumentChunkStore _chunkStore;
    private readonly IAIProfileManager _profileManager;
    private readonly FileSystemFileStore _fileStore;

    public AIDocumentController(
        IAIDocumentStore documentStore,
        IAIDocumentChunkStore chunkStore,
        IAIProfileManager profileManager,
        FileSystemFileStore fileStore)
    {
        _documentStore = documentStore;
        _chunkStore = chunkStore;
        _profileManager = profileManager;
        _fileStore = fileStore;
    }

    [HttpPost("upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(string profileId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided." });
        }

        var profile = await _profileManager.FindByIdAsync(profileId);

        if (profile == null)
        {
            return NotFound(new { error = "Profile not found." });
        }

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".csv", ".json", ".xml", ".html", ".pdf", ".docx", ".xlsx", ".pptx",
        };

        var ext = Path.GetExtension(file.FileName);

        if (!allowedExtensions.Contains(ext))
        {
            return BadRequest(new { error = $"File type '{ext}' is not supported." });
        }

        // Read file content as text for simple text-based files.
        var text = string.Empty;
        var textExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".csv", ".json", ".xml", ".html",
        };

        if (textExtensions.Contains(ext))
        {
            using var reader = new StreamReader(file.OpenReadStream());
            text = await reader.ReadToEndAsync();
        }

        // Save the file to the file store.
        var storagePath = $"documents/{profileId}/{UniqueId.GenerateId()}{ext}";
        using (var stream = file.OpenReadStream())
        {
            await _fileStore.SaveFileAsync(storagePath, stream);
        }

        // Create the document record.
        var document = new AIDocument
        {
            ItemId = UniqueId.GenerateId(),
            ReferenceId = profileId,
            ReferenceType = "profile",
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            UploadedUtc = DateTime.UtcNow,
        };

        await _documentStore.CreateAsync(document);

        if (!string.IsNullOrEmpty(text))
        {
            await _chunkStore.CreateAsync(new AIDocumentChunk
            {
                ItemId = UniqueId.GenerateId(),
                AIDocumentId = document.ItemId,
                ReferenceId = profileId,
                ReferenceType = "profile",
                Content = text,
                Index = 0,
            });
        }

        // Update the profile's document metadata.
        profile.AlterSettings<DocumentsMetadata>(m =>
        {
            m.Documents ??= [];
            m.Documents.Add(new ChatDocumentInfo
            {
                DocumentId = document.ItemId,
                FileName = document.FileName,
                ContentType = document.ContentType,
                FileSize = document.FileSize,
            });
        });

        await _profileManager.UpdateAsync(profile);
        await _documentStore.SaveChangesAsync();

        return Ok(new
        {
            id = document.ItemId,
            fileName = document.FileName,
            contentType = document.ContentType,
            fileSize = document.FileSize,
        });
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string profileId, string documentId)
    {
        var profile = await _profileManager.FindByIdAsync(profileId);

        if (profile == null)
        {
            return NotFound(new { error = "Profile not found." });
        }

        var document = await _documentStore.FindByIdAsync(documentId);

        if (document != null)
        {
            await _documentStore.DeleteAsync(document);
        }

        // Remove from profile metadata.
        profile.AlterSettings<DocumentsMetadata>(m =>
        {
            m.Documents ??= [];
            m.Documents = m.Documents.Where(d => d.DocumentId != documentId).ToList();
        });

        await _profileManager.UpdateAsync(profile);
        await _documentStore.SaveChangesAsync();

        return Ok(new { success = true });
    }
}
