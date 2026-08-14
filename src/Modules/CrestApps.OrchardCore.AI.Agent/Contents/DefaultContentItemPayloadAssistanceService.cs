using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

internal sealed class DefaultContentItemPayloadAssistanceService : IContentItemPayloadAssistanceService
{
    private readonly IContentManager _contentManager;
    private readonly DocumentJsonSerializerOptions _jsonSerializerOptions;

    public DefaultContentItemPayloadAssistanceService(
        IContentManager contentManager,
        IOptions<DocumentJsonSerializerOptions> jsonSerializerOptions)
    {
        _contentManager = contentManager;
        _jsonSerializerOptions = jsonSerializerOptions.Value;
    }

    public ValueTask<ContentItemPayloadValidationResult> ValidateAsync(
        ContentTypeDefinition contentDefinition,
        JsonNode inputNode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentDefinition);
        ArgumentNullException.ThrowIfNull(inputNode);

        return ValidateCoreAsync(contentDefinition, inputNode);
    }

    private async ValueTask<ContentItemPayloadValidationResult> ValidateCoreAsync(
        ContentTypeDefinition contentDefinition,
        JsonNode inputNode)
    {
        var sampleContentItem = await _contentManager.NewAsync(contentDefinition.Name);
        var referenceNode = JsonSerializer.SerializeToNode(sampleContentItem, _jsonSerializerOptions.SerializerOptions);

        if (referenceNode is null)
        {
            return ContentItemPayloadValidationResult.Success;
        }

        referenceNode = ContentItemPayloadShapeValidator.AddKnownContentItemRootProperties(referenceNode);

        var unmappedPaths = ContentItemPayloadShapeValidator.FindUnexpectedPaths(inputNode, referenceNode);

        if (unmappedPaths.Count == 0)
        {
            return ContentItemPayloadValidationResult.Success;
        }

        return new ContentItemPayloadValidationResult(
            false,
            ["The provided content item JSON contains values that could not be mapped to the Orchard Core content item structure."],
            unmappedPaths,
            null);
    }

    public async ValueTask<string> GetGuidanceAsync(
        ContentTypeDefinition contentDefinition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentDefinition);

        var sampleContentItem = await _contentManager.NewAsync(contentDefinition.Name);

        return
        $"""
        Sample content item JSON:
        {JsonSerializer.Serialize(sampleContentItem, _jsonSerializerOptions.SerializerOptions)}

        Content type definition:
        {JsonSerializer.Serialize(contentDefinition, JsonHelpers.ContentDefinitionSerializerOptions)}
        """;
    }

}
