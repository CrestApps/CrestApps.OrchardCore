using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

internal sealed class RecipeContentItemPayloadAssistanceService : IContentItemPayloadAssistanceService
{
    private readonly IContentManager _contentManager;
    private readonly IContentItemSchemaService _contentItemSchemaService;
    private readonly DocumentJsonSerializerOptions _jsonSerializerOptions;

    public RecipeContentItemPayloadAssistanceService(
        IContentManager contentManager,
        IContentItemSchemaService contentItemSchemaService,
        IOptions<DocumentJsonSerializerOptions> jsonSerializerOptions)
    {
        _contentManager = contentManager;
        _contentItemSchemaService = contentItemSchemaService;
        _jsonSerializerOptions = jsonSerializerOptions.Value;
    }

    public async ValueTask<ContentItemPayloadValidationResult> ValidateAsync(
        ContentTypeDefinition contentDefinition,
        JsonNode inputNode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentDefinition);
        ArgumentNullException.ThrowIfNull(inputNode);

        var schema = await _contentItemSchemaService.GetSchemaAsync(contentDefinition.Name, cancellationToken);

        if (schema is null)
        {
            return ContentItemPayloadValidationResult.Success;
        }

        var builtSchema = schema.Build();
        var evaluationResult = builtSchema.Evaluate(
            JsonSerializer.SerializeToElement(inputNode, _jsonSerializerOptions.SerializerOptions),
            new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
            });

        if (evaluationResult.IsValid)
        {
            return ContentItemPayloadValidationResult.Success;
        }

        return new ContentItemPayloadValidationResult(
            false,
            ["The provided content item JSON does not match the expected content item JSON schema."],
            [],
            GetValidJSONPayload(builtSchema));
    }

    public async ValueTask<string> GetGuidanceAsync(
        ContentTypeDefinition contentDefinition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentDefinition);

        var sampleContentItem = await _contentManager.NewAsync(contentDefinition.Name);
        var schema = await _contentItemSchemaService.GetSchemaAsync(contentDefinition.Name, cancellationToken);

        if (schema is not null)
        {
            return GetValidJSONPayload(schema.Build());
        }

        return
            $"""
            Sample content item JSON:
            {JsonSerializer.Serialize(sampleContentItem, _jsonSerializerOptions.SerializerOptions)}

            Content type definition:
            {JsonSerializer.Serialize(contentDefinition, JsonHelpers.ContentDefinitionSerializerOptions)}
            """;
    }

    private static string GetValidJSONPayload(JsonSchema schema)
    {
        return
        $"""
        Valid content item JSON schema for retry payloads:
        {JsonSerializer.Serialize(schema)}
        """;
    }
}
