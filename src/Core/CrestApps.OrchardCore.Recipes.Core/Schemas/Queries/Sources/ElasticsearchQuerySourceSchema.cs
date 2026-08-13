using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Queries.Sources;

/// <summary>
/// Describes the recipe schema for the <c>Elasticsearch</c> query source contributed by the Elasticsearch feature.
/// </summary>
public sealed class ElasticsearchQuerySourceSchema : QuerySourceSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "Elasticsearch";

    /// <inheritdoc />
    protected override string DisplayText => "Elasticsearch";

    /// <inheritdoc />
    protected override string Description => "Runs an Elasticsearch query against an Elasticsearch index.";

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Index", "Template"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(QuerySourceSchemaContext context)
    {
        yield return ("Index", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .WithSuggestions(context.Examples.IndexProfileNames)
            .Description("The name of the Elasticsearch index the query runs against."));

        yield return ("Template", new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The Elasticsearch query JSON executed against the index. It supports Liquid and file expressions such as [file:text('Snippets/query.json')]."));
    }
}
