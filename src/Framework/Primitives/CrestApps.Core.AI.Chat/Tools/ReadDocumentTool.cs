using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;

using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Tooling;
using Microsoft.Extensions.AI;

using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.Chat.Tools;

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
        var logger = arguments.Services.GetRequiredService<ILogger<ReadDocumentTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {

            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        if (!arguments.TryGetFirstString("document_id", out var documentId))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required argument 'document_id'.", Name);

            return "Unable to find a 'document_id' argument in the arguments parameter.";

        }

        var executionContext = AIInvocationScope.Current?.ToolExecutionContext;

        if (executionContext?.Resource is ChatInteraction interaction)
        {

            var chatInteractionId = interaction.ItemId;
            var documentStore = arguments.Services.GetService<IAIDocumentStore>();

            if (documentStore is null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: document store is not available.", Name);

                return "Document store is not available.";

            }

            var document = await documentStore.FindByIdAsync(documentId);

            if (document is null || document.ReferenceId != chatInteractionId)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: document '{DocumentId}' was not found in this session.", Name, documentId);

                return $"Document with ID '{documentId}' was not found in this session.";
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {

                logger.LogDebug("AI tool '{ToolName}' completed.", Name);
            }

            return await FormatDocumentTextFromChunksAsync(arguments.Services, document);
        }

        if (executionContext?.Resource is AIProfile profile)

        {
            var documentStore = arguments.Services.GetService<IAIDocumentStore>();

            if (documentStore is null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: document store is not available.", Name);

                return "Document store is not available.";
            }

            // The document could belong to either the profile or a chat session.
            var validReferenceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {

                profile.ItemId,
            };

            if (AIInvocationScope.Current?.Items.TryGetValue(nameof(AIChatSession), out var sessionObj) == true &&
                sessionObj is AIChatSession session &&
                    session.Documents is { Count: > 0 })
            {

                validReferenceIds.Add(session.SessionId);

            }

            var document = await documentStore.FindByIdAsync(documentId);

            if (document is null || !validReferenceIds.Contains(document.ReferenceId))
            {
                logger.LogWarning("AI tool '{ToolName}' failed: document '{DocumentId}' was not found.", Name, documentId);

                return $"Document with ID '{documentId}' was not found.";
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {

                logger.LogDebug("AI tool '{ToolName}' completed.", Name);
            }

            return await FormatDocumentTextFromChunksAsync(arguments.Services, document);

        }

        logger.LogWarning("AI tool '{ToolName}' failed: no active chat interaction session or AI profile.", Name);

        return "Document access requires an active chat interaction session or AI profile.";
    }

    private static async Task<string> FormatDocumentTextFromChunksAsync(IServiceProvider services, AIDocument document)
    {
        var chunkStore = services.GetService<IAIDocumentChunkStore>();

        if (chunkStore is null)
        {

            return $"Document '{document.FileName}' has no extractable text content.";

        }

        var chunks = await chunkStore.GetChunksByAIDocumentIdAsync(document.ItemId);

        if (chunks.Count == 0)
        {

            return $"Document '{document.FileName}' has no extractable text content.";

        }

        var text = string.Join(Environment.NewLine, chunks.OrderBy(c => c.Index).Select(c => c.Content));

        return FormatDocumentText(document.FileName, text);
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
