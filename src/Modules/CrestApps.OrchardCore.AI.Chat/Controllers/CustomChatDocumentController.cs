using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.AI.Chat.Controllers;

public sealed class CustomChatDocumentController : Controller
{
    private readonly IAICustomChatSessionManager _sessionManager;
    private readonly CustomChatTempDocumentStore _documentStore;

    public CustomChatDocumentController(
        IAICustomChatSessionManager sessionManager,
        CustomChatTempDocumentStore documentStore)
    {
        _sessionManager = sessionManager;
        _documentStore = documentStore;
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Admin("ai/custom-chat/upload")]
    public async Task<IActionResult> Upload(string customChatInstanceId, IFormFile file, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customChatInstanceId))
        {
            return BadRequest("customChatInstanceId is required.");
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        var session = await _sessionManager.FindByCustomChatInstanceIdAsync(customChatInstanceId);

        if (session == null)
        {
            return NotFound();
        }

        var tempFilePath = await _documentStore.SaveAsync(session.SessionId, file, cancellationToken);

        var documents = session.Documents ?? new CustomChatSessionDocuments();

        documents.Items.Add(new CustomChatSessionDocumentEntry
        {
            DocumentId = Path.GetFileName(tempFilePath),
            FileName = file.FileName,
            ContentType = file.ContentType,
            Length = file.Length,
            TempFilePath = tempFilePath,
            CreatedUtc = DateTime.UtcNow
        });

        session.Documents = documents;

        await _sessionManager.SaveCustomChatAsync(session, cancellationToken);

        return Ok(new
        {
            FileName = file.FileName,
            Size = file.Length
        });
    }
}
