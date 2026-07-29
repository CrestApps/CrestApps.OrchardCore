using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;

/// <summary>
/// Describes the recipe schema for the <c>EmailTask</c> workflow task.
/// </summary>
public sealed class EmailTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "EmailTask";

    /// <inheritdoc />
    protected override string Category => "Messaging";

    /// <inheritdoc />
    protected override string DisplayText => "Email Task";

    /// <inheritdoc />
    protected override string Description => "Sends an email message using the configured email provider";

    /// <inheritdoc />
    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["Recipients"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions()
    {
        yield return ("Author", WorkflowActivitySchemaBuilders.LiquidExpression("The author's email address that defines who the email is from. Leave blank to use the configured email address."));
        yield return ("Sender", WorkflowActivitySchemaBuilders.LiquidExpression("The sender's email address. Required only when it differs from the 'Author' email address."));
        yield return ("ReplyTo", WorkflowActivitySchemaBuilders.LiquidExpression("The 'reply to' email address. Required only when replies should go to a different address than the 'Author' address."));
        yield return ("Recipients", WorkflowActivitySchemaBuilders.LiquidExpression("The comma-separated list of recipient email addresses."));
        yield return ("Cc", WorkflowActivitySchemaBuilders.LiquidExpression("The comma-separated list of carbon copy email addresses."));
        yield return ("Bcc", WorkflowActivitySchemaBuilders.LiquidExpression("The comma-separated list of blind carbon copy email addresses."));
        yield return ("Subject", WorkflowActivitySchemaBuilders.LiquidExpression("The subject of the email message."));
        yield return ("BodyFormat", WorkflowActivitySchemaBuilders.EnumValue("The format of the email message. 'All' sends both the text and the HTML body.", "All", "Text", "Html"));
        yield return ("TextBody", WorkflowActivitySchemaBuilders.LiquidExpression("The plain text body of the email message. Used when 'BodyFormat' is 'All' or 'Text'."));
        yield return ("HtmlBody", WorkflowActivitySchemaBuilders.LiquidExpression("The HTML body of the email message. Used when 'BodyFormat' is 'All' or 'Html'."));
    }
}
