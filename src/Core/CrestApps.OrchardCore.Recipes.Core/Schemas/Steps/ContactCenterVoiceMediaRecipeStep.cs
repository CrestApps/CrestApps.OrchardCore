using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "ContactCenterVoiceMedia" recipe step — imports the voice media library: named audio clips (hold
/// music, greetings, IVR prompts) the platform keeps a master copy of and gives each telephony provider a copy of,
/// referenced by queues, campaigns and entry points.
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
                            ("ProviderName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Technical name of the telephony provider whose copy 'MediaReference' plays (for example 'Telnyx'). Kept for plans written when an entry recorded a single provider copy; 'ProviderReferences' records every provider's copy, and this pair is folded into it when the entry is read.")),
                            ("MediaReference", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Reference the provider named by 'ProviderName' plays the clip by (for Telnyx, the Media Storage media name). The audio itself is not carried by the recipe.")),
                            ("Format", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Audio format the clip was uploaded in, for example 'mp3' or 'wav'.")),
                            ("MasterStorageReference", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Reference of the master copy in the platform's own media store (the Telephony recording media store: the tenant's application-data folder, or Azure Blob Storage when that feature is enabled). The audio itself is not carried by the recipe, so the reference resolves only where the destination shares that media store; otherwise upload the clip again in the destination. Null for a clip that exists only at a provider.")),
                            ("ContentType", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Content type the audio was uploaded with, for example 'audio/mpeg', given to a provider again when it is sent a copy from the master copy.")),
                            ("ProviderReferences", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The reference that provider plays the clip by."))
                                .Description("The copy each telephony provider holds, keyed by the provider's technical name (for example 'Telnyx'). A provider without an entry is given a copy from the master copy when it first plays the clip. A reference resolves only in the destination provider account it was made in.")),
                            ("Properties", CatalogRecipeStepSchemas.Properties("voice media clip")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The voice media clips to create or update.")),
            ],
            ["VoiceMedia"]);
}
