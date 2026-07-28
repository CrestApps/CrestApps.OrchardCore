using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>RegisterUserTask</c> workflow task.
/// </summary>
public sealed class RegisterUserTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "RegisterUserTask";

    /// <inheritdoc />
    protected override string Category => "User";

    /// <inheritdoc />
    protected override string DisplayText => "Register User Task";

    /// <inheritdoc />
    protected override string Description => "Registers a new user and optionally sends a confirmation email";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Valid", "Invalid"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("SendConfirmationEmail", WorkflowActivitySchemaBuilders.Boolean("When true, sends a confirmation email to the newly registered user. Defaults to true."));
        yield return ("ConfirmationEmailSubject", WorkflowActivitySchemaBuilders.LiquidExpression("The subject of the confirmation email. Required when 'SendConfirmationEmail' is true."));
        yield return ("ConfirmationEmailTemplate", WorkflowActivitySchemaBuilders.LiquidExpression("The body of the confirmation email. Use the 'Workflow.Properties.EmailConfirmationUrl' Liquid property to include the confirmation link. Required when 'SendConfirmationEmail' is true."));
        yield return ("RequireModeration", WorkflowActivitySchemaBuilders.Boolean("When true, the newly created user account is disabled until an administrator approves it. Defaults to false."));
    }
}
