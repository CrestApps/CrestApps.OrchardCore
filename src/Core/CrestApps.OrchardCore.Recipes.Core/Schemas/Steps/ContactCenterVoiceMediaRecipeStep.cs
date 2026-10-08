using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "ContactCenterVoiceMedia" recipe step — imports the voice media library: named audio clips (hold
/// music, greetings, IVR prompts) hosted in a telephony provider's media storage and referenced by queues, campaigns
/// and entry points.
/// </summary>
public sealed class ContactCenterVoiceMediaRecipeStep : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => "ContactCenterVoiceMedia";

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("VoiceMedia", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", CatalogRecipeStepSchemas.ItemId("voice media clip")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("Unique, admin-facing name of the clip.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Optional description, for example the business or line the clip is for.")),
                            ("ProviderName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Technical name of the telephony provider that hosts the clip (for example 'Telnyx'). A clip plays only through the provider that stores it.")),
                            ("MediaReference", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Provider-hosted media reference used to play the clip (for Telnyx, the Media Storage media name). The audio itself is not carried by the recipe: the reference must already exist in the destination provider account, so a plan is portable between environments that share that account.")),
                            ("Format", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Audio format the clip was uploaded in, for example 'mp3' or 'wav'.")),
                            ("Properties", CatalogRecipeStepSchemas.Properties("voice media clip")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The voice media clips to create or update.")),
            ],
            ["VoiceMedia"]);
}
