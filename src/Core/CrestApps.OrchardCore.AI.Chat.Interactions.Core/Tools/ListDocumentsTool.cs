using System.Text.Json;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Tools;

/// <summary>
/// System tool that lists documents available in the current chat session.
/// Returns metadata (ID, name, type, size) so the LLM can decide which to read.
/// </summary>
public sealed class ListDocumentsTool : AIFunction
{
    public const string TheName = SystemToolNames.ListDocuments;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Lists all documents attached to the current chat session, returning their ID, file name, content type, and file size.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var httpContextAccessor = arguments.Services.GetService<IHttpContextAccessor>();
        var executionContext = httpContextAccessor?.HttpContext?.Items[nameof(AIToolExecutionContext)] as AIToolExecutionContext;

        if (executionContext?.Resource is not ChatInteraction interaction)
        {
            return "Document access requires an active chat interaction session.";
        }

        var chatInteractionId = interaction.ItemId;
        var documentStore = arguments.Services.GetService<IChatInteractionDocumentStore>();

        if (documentStore is null)
        {
            return "Document store is not available.";
        }

        var documents = await documentStore.GetDocuments(chatInteractionId);

        if (documents is null || documents.Count == 0)
        {
            return "No documents are attached to this session.";
        }

        var result = documents.Select(d => new
        {
            d.ItemId,
            d.FileName,
            d.ContentType,
            FileSize = FormatFileSize(d.FileSize),
        });

        return JsonSerializer.Serialize(result);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
