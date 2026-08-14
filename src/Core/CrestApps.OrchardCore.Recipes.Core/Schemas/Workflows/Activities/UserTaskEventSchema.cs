using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>UserTaskEvent</c> workflow event.
/// </summary>
public sealed class UserTaskEventSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "UserTaskEvent";

    /// <inheritdoc />
    protected override string Category => "Content";

    /// <inheritdoc />
    protected override string DisplayText => "User Task Event";

    /// <inheritdoc />
    protected override string Description => "Waits for a user to perform one of the specified actions, then routes the workflow based on the action taken";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => [];

    /// <inheritdoc />
    protected override bool HasDynamicOutcomes => true;

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Actions", WorkflowActivitySchemaBuilders.StringArray("A list of action names the user can perform. Each action name becomes a workflow outcome."));
        yield return ("Roles", WorkflowActivitySchemaBuilders.StringArray("The roles allowed to perform the actions. Leave empty to allow any role.", context.Examples.RoleNames));
    }
}
