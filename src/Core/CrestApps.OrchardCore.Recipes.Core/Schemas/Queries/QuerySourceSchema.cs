using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Queries;

/// <summary>
/// Describes the recipe payload of a single query source inside the <c>Queries</c> array of the
/// <c>Queries</c> recipe step.
/// </summary>
public sealed class QuerySourceSchema
{
    /// <summary>
    /// Gets or sets the human readable source title shown in the query editor.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a description explaining what the source runs.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the property definitions that are specific to the source, beyond the shared members the
    /// schema service adds, such as <c>Name</c>, <c>Source</c>, <c>Schema</c> and <c>ReturnContentItems</c>.
    /// </summary>
    public IReadOnlyList<(string Name, JsonSchemaBuilder Schema)> Properties { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of the properties that must be provided in addition to the shared members.
    /// </summary>
    public IReadOnlyList<string> RequiredProperties { get; set; } = [];
}
