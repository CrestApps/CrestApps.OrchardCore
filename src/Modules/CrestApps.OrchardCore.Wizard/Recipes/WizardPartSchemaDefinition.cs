using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using CrestApps.OrchardCore.Wizard.Core.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Wizard.Recipes;

/// <summary>
/// Describes the <see cref="WizardPart"/> and its type settings to CrestApps Recipes.
/// </summary>
internal sealed class WizardPartSchemaDefinition : PartSchemaDefinitionBase
{
    public override string Name => nameof(WizardPart);

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                (nameof(WizardPartSettings), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Description("How the wizard's steps are authored and how a started wizard behaves.")
                    .Properties(
                        (nameof(WizardPartSettings.ContainedContentTypes), Strings("The content types allowed as steps. Empty allows every type that matches ContainedStereotypes.")),
                        (nameof(WizardPartSettings.ContainedStereotypes), Strings("The stereotypes allowed as steps. Empty does not filter by stereotype.")),
                        (nameof(WizardPartSettings.DisplayType), new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The display type each step is rendered with in the authoring editor.")),
                        (nameof(WizardPartSettings.CollapseContainedItems), new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether steps are collapsed by default in the authoring editor.")),
                        (nameof(WizardPartSettings.RequiresAuthenticatedUser), new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether a visitor must be signed in to start the wizard.")),
                        (nameof(WizardPartSettings.CompletionPolicy), new JsonSchemaBuilder()
                            .Enum(0, 1, 2, "None", "Draft", "Publish")
                            .Description("What happens to the collected responses when the wizard completes: 0 = None (kept only in the session for a workflow or handler), 1 = Draft (created as drafts), 2 = Publish (created and published, the default).")))
                    .AdditionalProperties(true)))
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                (nameof(WizardPart.Steps), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Required("ContentType")
                        .AdditionalProperties(true))
                    .Description("The ordered steps of the wizard, each a content item (ContentItemId, ContentType and its parts) of a type the settings allow. A started wizard copies them into the visitor's session, so they are never changed by a running wizard.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder Strings(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description(description);
}
