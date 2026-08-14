using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "Sitemaps" recipe step — creates or updates sitemaps and sitemap indexes.
/// </summary>
public sealed class SitemapsRecipeStep : IRecipeStep
{
    private readonly ISitemapSchemaService _sitemapSchemaService;

    private JsonSchema _cached;

    public string Name => "Sitemaps";

    /// <summary>
    /// Initializes a new instance of the <see cref="SitemapsRecipeStep"/> class.
    /// </summary>
    /// <param name="sitemapSchemaService">The sitemap schema service used to describe each sitemap and its sources.</param>
    public SitemapsRecipeStep(ISitemapSchemaService sitemapSchemaService)
    {
        _sitemapSchemaService = sitemapSchemaService;
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

        var sitemapSchema = await _sitemapSchemaService.GetSitemapSchemaAsync(cancellationToken);

        _cached = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("Sitemaps").Description("Recipe step discriminator. Must be 'Sitemaps'.")),
                ("data", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(sitemapSchema)
                    .Description("Sitemaps and sitemap indexes to create or update.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return _cached;
    }
}
