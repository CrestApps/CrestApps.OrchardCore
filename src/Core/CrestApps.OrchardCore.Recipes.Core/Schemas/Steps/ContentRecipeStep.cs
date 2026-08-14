using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "content" recipe step — imports content items.
/// </summary>
public sealed class ContentRecipeStep : IRecipeStep
{
    private readonly IContentItemSchemaService _contentItemSchemaService;

    private JsonSchema _cached;

    public string Name => "content";

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentRecipeStep"/> class.
    /// </summary>
    /// <param name="contentItemSchemaService">The content item schema service.</param>
    public ContentRecipeStep(IContentItemSchemaService contentItemSchemaService)
    {
        _contentItemSchemaService = contentItemSchemaService;
    }

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public async ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var contentItemSchema = await _contentItemSchemaService.GetSchemaAsync(cancellationToken);

        _cached = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("content").Description("Recipe step discriminator. Must be 'content'.")),
                ("data", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(contentItemSchema)
                    .MinItems(1)
                    .Description("Content items to import or update. Each item must match the schema for its content type.")))
            .Required("name", "data")
            .AdditionalProperties(true)
            .Build();

        return _cached;
    }
}
