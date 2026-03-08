using System.Text.Json;
using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        var logger = arguments.Services.GetRequiredService<ILogger<ListDocumentsTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
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

            var documents = await documentStore.GetDocumentsAsync(chatInteractionId, AIConstants.DocumentReferenceTypes.ChatInteraction);

            if (documents is null || documents.Count == 0)
            {
                logger.LogWarning("AI tool '{ToolName}': no documents attached to session '{SessionId}'.", Name, chatInteractionId);
                return "No documents are attached to this session.";
            }

            var result = documents.Select(d => new
            {
                d.ItemId,
                d.FileName,
                d.ContentType,
                FileSize = FormatFileSize(d.FileSize),
            });

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("AI tool '{ToolName}' completed.", Name);
            }

            return JsonSerializer.Serialize(result);
        }

        if (executionContext?.Resource is AIProfile profile)
        {
            var documentStore = arguments.Services.GetService<IAIDocumentStore>();

            if (documentStore is null)
            {
                logger.LogWarning("AI tool '{ToolName}' failed: document store is not available.", Name);
                return "Document store is not available.";
            }

            // Collect documents from both profile-level and session-level sources.
            var allDocuments = new List<AIDocument>();

            var profileDocs = await documentStore.GetDocumentsAsync(profile.ItemId, AIConstants.DocumentReferenceTypes.Profile);

            if (profileDocs is { Count: > 0 })
            {
                allDocuments.AddRange(profileDocs);
            }

            if (AIInvocationScope.Current?.Items.TryGetValue(nameof(AIChatSession), out var sessionObj) == true &&
                sessionObj is AIChatSession session &&
                session.Documents is { Count: > 0 })
            {
                var sessionDocs = await documentStore.GetDocumentsAsync(session.SessionId, AIConstants.DocumentReferenceTypes.ChatSession);

                if (sessionDocs is { Count: > 0 })
                {
                    allDocuments.AddRange(sessionDocs);
                }
            }

            if (allDocuments.Count == 0)
            {
                logger.LogWarning("AI tool '{ToolName}': no documents attached.", Name);
                return "No documents are attached.";
            }

            var result = allDocuments.Select(d => new
            {
                d.ItemId,
                d.FileName,
                d.ContentType,
                FileSize = FormatFileSize(d.FileSize),
            });

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("AI tool '{ToolName}' completed.", Name);
            }

            return JsonSerializer.Serialize(result);
        }

        logger.LogWarning("AI tool '{ToolName}' failed: no active chat interaction session or AI profile.", Name);

        return "Document access requires an active chat interaction session or AI profile.";
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
