using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Queries;

/// <summary>
/// Describes a query source discovered from the registered <see cref="IQuerySourceSchemaDefinition"/>
/// contributions.
/// </summary>
public sealed class QuerySourceDescriptor
{
    /// <summary>
    /// Gets the query source name, matching the <c>Source</c> discriminator, for example <c>Sql</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the human readable source title shown in the query editor.
    /// </summary>
    public string DisplayText { get; init; }

    /// <summary>
    /// Gets a description explaining what the source runs.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets the property definitions that are specific to the source, beyond the shared members.
    /// </summary>
    public IReadOnlyList<(string Name, JsonSchemaBuilder Schema)> Properties { get; init; } = [];

    /// <summary>
    /// Gets the names of the properties that must be provided in addition to the shared members.
    /// </summary>
    public IReadOnlyList<string> RequiredProperties { get; init; } = [];
}
