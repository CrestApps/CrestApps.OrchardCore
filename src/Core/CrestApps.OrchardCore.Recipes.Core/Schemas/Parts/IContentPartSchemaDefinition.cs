using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

public interface IContentPartSchemaDefinition
{
    string Name { get; }

    ValueTask<JsonSchemaBuilder> GetPartSchemaAsync(CancellationToken cancellationToken = default);
}
