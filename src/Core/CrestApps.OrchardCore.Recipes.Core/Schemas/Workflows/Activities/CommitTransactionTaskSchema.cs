using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>CommitTransactionTask</c> workflow task.
/// </summary>
public sealed class CommitTransactionTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "CommitTransactionTask";

    /// <inheritdoc />
    protected override string Category => "Session";

    /// <inheritdoc />
    protected override string DisplayText => "Commit Transaction Task";

    /// <inheritdoc />
    protected override string Description => "Commits the current database session, unless the model state is invalid";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Valid", "Invalid"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
        => [];
}
