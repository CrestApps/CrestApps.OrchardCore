using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Orders.Recipes;

/// <summary>
/// Describes the <c>OrderEmailTemplateSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class OrderEmailTemplateSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(OrderEmailTemplateSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The store's templates of its emails, of every kind (Settings › Commerce › Emails). The class keeps the name it had when it served orders only, because the site stores its settings under the class name: renaming it would lose the templates a store wrote and break its recipes and deployment plans.")
            .Properties(
                ("Layout", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The HTML around every email with a kind, with {{ Content }} where the email goes, such as the store's logo and address; empty for none.")),
                ("Templates", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.Object).Properties(
                    ("IsEnabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the template is used.")),
                    ("Subject", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The subject, with Liquid; the email's own when empty.")),
                    ("Body", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The body, as HTML with Liquid; {{ Content }} is the email as the store would send it."))).AdditionalProperties(true)).Description("Each email's own template, by its kind.")))
            .AdditionalProperties(true);
}
