using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Tools;

/// <summary>
/// System tool that reads the full text content of a specific document.
/// The LLM calls this to load document content on demand.
/// </summary>
public sealed class ReadDocumentTool : AIFunction
{
    public const string TheName = SystemToolNames.ReadDocument;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "document_id": {
              "type": "string",
              "description": "The unique identifier of the document to read."
            }
          },
          "required": ["document_id"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Reads the full text content of a specific document attached to the chat session.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } =
        new Dictionary<string, object>() { ["Strict"] = false };

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetFirstString("document_id", out var documentId))
        {
            return "Unable to find a 'document_id' argument in the arguments parameter.";
        }

        var httpContextAccessor = arguments.Services.GetService<IHttpContextAccessor>();
        var executionContext = httpContextAccessor?.HttpContext?.Items[nameof(AIToolExecutionContext)] as AIToolExecutionContext;

        if (executionContext?.Resource is ChatInteraction interaction)
        {
            var chatInteractionId = interaction.ItemId;
            var documentStore = arguments.Services.GetService<IChatInteractionDocumentStore>();

            if (documentStore is null)
            {
                return "Document store is not available.";
            }

            var document = await documentStore.FindByIdAsync(documentId);

            if (document is null || document.ChatInteractionId != chatInteractionId)
            {
                return $"Document with ID '{documentId}' was not found in this session.";
            }

            return FormatDocumentText(document.FileName, document.Text);
        }

        if (executionContext?.Resource is AIProfile profile)
        {
            var profileDocumentStore = arguments.Services.GetService<IAIProfileDocumentStore>();

            if (profileDocumentStore is null)
            {
                return "Document store is not available.";
            }

            var document = await profileDocumentStore.FindByIdAsync(documentId);

            if (document is null || document.ProfileId != profile.ItemId)
            {
                return $"Document with ID '{documentId}' was not found in this profile.";
            }

            return FormatDocumentText(document.FileName, document.Text);
        }

        return "Document access requires an active chat interaction session or AI profile.";
    }

    private static string FormatDocumentText(string fileName, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return $"Document '{fileName}' has no extractable text content.";
        }

        const int maxLength = 50_000;

        if (text.Length > maxLength)
        {
            text = string.Concat(text.AsSpan(0, maxLength), "\n\n... [content truncated at 50KB]");
        }

        return $"[Document: {fileName}]\n\n{text}";
    }
}
