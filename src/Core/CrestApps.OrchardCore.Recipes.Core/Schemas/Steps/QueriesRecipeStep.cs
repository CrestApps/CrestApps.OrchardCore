using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "Queries" recipe step — creates or updates queries such as SQL, Lucene or Elasticsearch queries.
/// </summary>
public sealed class QueriesRecipeStep : IRecipeStep
{
    private readonly IQuerySchemaService _querySchemaService;

    private JsonSchema _cached;

    /// <inheritdoc />
    public string Name => "Queries";

    /// <summary>
    /// Initializes a new instance of the <see cref="QueriesRecipeStep"/> class.
    /// </summary>
    /// <param name="querySchemaService">The service that composes the query source schemas.</param>
    public QueriesRecipeStep(IQuerySchemaService querySchemaService)
    {
        _querySchemaService = querySchemaService;
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= await CreateSchemaAsync(cancellationToken);

        return _cached;
    }

    private async ValueTask<JsonSchema> CreateSchemaAsync(CancellationToken cancellationToken)
    {
        var querySchema = await _querySchemaService.GetQuerySchemaAsync(cancellationToken);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Const("Queries")
                    .Description("Recipe step discriminator. Must be 'Queries'.")),
                ("Queries", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(querySchema)
                    .MinItems(1)
                    .Description("The queries to create or update.")))
            .Required("name", "Queries")
            .AdditionalProperties(true)
            .Build();
    }
}
