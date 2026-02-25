using System.Text;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Orchestration context handler that populates document references
/// from a <see cref="ChatInteraction"/> or <see cref="AIProfile"/> resource
/// and enriches the system message with document metadata so the model knows
/// which documents are available and which tools to use to access them.
/// </summary>
/// <remarks>
/// Document processing tools are registered as system tools and are always included
/// by the orchestrator. This handler provides the model with document metadata
/// and tool descriptions. The resource ID is resolved server-side from
/// <see cref="AIToolExecutionContext.Resource"/> — it is never exposed to the model.
/// </remarks>
public sealed class DocumentOrchestrationHandler : IOrchestrationContextBuilderHandler
{
    private readonly AIToolDefinitionOptions _toolDefinitions;

    public DocumentOrchestrationHandler(IOptions<AIToolDefinitionOptions> toolDefinitions)
    {
        _toolDefinitions = toolDefinitions.Value;
    }

    public Task BuildingAsync(OrchestrationContextBuildingContext context)
    {
        if (context.Resource is ChatInteraction interaction &&
            interaction.Documents is { Count: > 0 })
        {
            context.Context.Documents = interaction.Documents;
        }
        else if (context.Resource is AIProfile profile)
        {
            var documentsMetadata = profile.As<AIProfileDocumentsMetadata>();

            if (documentsMetadata.Documents is { Count: > 0 })
            {
                context.Context.Documents = documentsMetadata.Documents;
            }
        }

        return Task.CompletedTask;
    }

    public Task BuiltAsync(OrchestrationContextBuiltContext context)
    {
        // Check for session documents after configure has run,
        // since the session is set via the configure callback
        // which executes between BuildingAsync and BuiltAsync.
        if (context.OrchestrationContext.Documents is not { Count: > 0 } &&
            context.Resource is AIProfile &&
            context.OrchestrationContext.CompletionContext?.AdditionalProperties is not null &&
            context.OrchestrationContext.CompletionContext.AdditionalProperties.TryGetValue("Session", out var sessionObj) &&
            sessionObj is AIChatSession session &&
            session.Documents is { Count: > 0 })
        {
            context.OrchestrationContext.Documents = session.Documents;
        }

        if (context.OrchestrationContext.Documents is not { Count: > 0 } ||
            context.OrchestrationContext.CompletionContext is null)
        {
            return Task.CompletedTask;
        }

        // Discover document processing tools dynamically by purpose
        // to list their descriptions in the system message.
        var docTools = _toolDefinitions.Tools
            .Where(t => t.Value.HasPurpose(AIToolPurposes.DocumentProcessing))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("\n\n[Available Documents or attachments]");

        if (docTools.Count > 0)
        {
            sb.AppendLine("The user has uploaded the following documents as supplementary context.");
            sb.AppendLine("Search the uploaded documents first using the document tools before answering.");
            sb.AppendLine("If the documents contain relevant information, base your answer on that content.");
            sb.AppendLine("If the documents do not contain relevant information, use your general knowledge to answer instead.");
            sb.AppendLine("Do not refuse to answer simply because the documents lack the requested information.");
            sb.AppendLine();
            sb.AppendLine("Available document tools:");

            foreach (var (name, entry) in docTools)
            {
                sb.Append("- ");
                sb.Append(name);
                sb.Append(": ");
                sb.AppendLine(entry.Description ?? entry.Title ?? name);
            }

            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("The user has uploaded the following documents as supplementary context.");
        }

        foreach (var doc in context.OrchestrationContext.Documents)
        {
            sb.Append("- ");
            sb.Append(doc.DocumentId);
            sb.Append(": \"");
            sb.Append(doc.FileName);
            sb.Append("\" (");
            sb.Append(doc.ContentType ?? "unknown");
            sb.Append(", ");
            sb.Append(FormatFileSize(doc.FileSize));
            sb.AppendLine(")");
        }

        context.OrchestrationContext.SystemMessageBuilder.Append(sb);

        return Task.CompletedTask;
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
