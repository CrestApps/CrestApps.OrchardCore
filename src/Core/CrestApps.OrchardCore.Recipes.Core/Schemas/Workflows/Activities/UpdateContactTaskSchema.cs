using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>UpdateContactTask</c> workflow task.
/// </summary>
public sealed class UpdateContactTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "UpdateContactTask";

    /// <inheritdoc />
    protected override string Category => "Omnichannel CRM";

    /// <inheritdoc />
    protected override string DisplayText => "Update Contact";

    /// <inheritdoc />
    protected override string Description => "Sets fields on a contact or lead without checking the record's required fields";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["ContentProperties"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("ContactContentItemId", WorkflowActivitySchemaBuilders.String("The Liquid expression that resolves the content item identifier of the contact or lead to change. Defaults to '{{ Workflow.Input.ContentItem.ContentItemId }}'."));
        yield return ("ContentProperties", WorkflowActivitySchemaBuilders.String("The Liquid template of the JSON merged into the record, such as { \"Customer\": { \"NoAnswerTextSent\": { \"Value\": true } } }."));
        yield return ("Publish", WorkflowActivitySchemaBuilders.Boolean("Whether a record whose latest version is a draft is published once changed. Defaults to true."));
    }
}
