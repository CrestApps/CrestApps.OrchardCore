using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;

internal interface IContentFieldSchemaDefinition
{
    string Name { get; }

    ValueTask<JsonSchemaBuilder> GetFieldSchemaAsync(CancellationToken cancellationToken = default);
}
