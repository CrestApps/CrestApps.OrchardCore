using System.Text.Json.Nodes;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

internal interface IContentItemPayloadAssistanceService
{
    ValueTask<ContentItemPayloadValidationResult> ValidateAsync(
        ContentTypeDefinition contentDefinition,
        JsonNode inputNode,
        CancellationToken cancellationToken = default);

    ValueTask<string> GetGuidanceAsync(ContentTypeDefinition contentDefinition, CancellationToken cancellationToken = default);
}
