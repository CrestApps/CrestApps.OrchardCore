using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "ConsentAddressOptOuts" recipe step of the E-mail Preferences feature: adds the opt-outs for one
/// purpose (such as marketing) recorded against an address, so a person's "stop all marketing" moves with the site. An
/// opt-out is matched by its channel, address and purpose; one that is already recorded takes the imported source and
/// time.
/// </summary>
public sealed class ConsentAddressOptOutsRecipeStep : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => "ConsentAddressOptOuts";

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
    {
        var optOut = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("An opt-out for one purpose recorded against an address. It is matched by Channel, Address and Purpose.")
            .Properties(
                ("Channel", RecipeStepSchemaBuilders.SuggestedString(["Email", "SMS", "Phone"])
                    .Description("The channel the person stopped, such as 'Email'.")),
                ("Address", RecipeStepSchemaBuilders.String()
                    .MinLength(1)
                    .Description("The address: an e-mail address, or a phone number in E.164 form. It is stored trimmed, and an e-mail address in lower case.")),
                ("Purpose", RecipeStepSchemaBuilders.SuggestedString(["Marketing"])
                    .MinLength(1)
                    .Description("The purpose the person stopped, such as 'Marketing'. Opt-outs of every purpose are not kept by this step.")),
                ("Source", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description("What recorded the opt-out, such as 'unsubscribe-link'.")),
                ("OptedOutUtc", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Format(Formats.DateTime)
                    .Description("When the opt-out was recorded, in UTC. The time of the import when empty.")))
            .Required("Channel", "Address", "Purpose")
            .AdditionalProperties(true);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("OptOuts", RecipeStepSchemaBuilders.Array(optOut)
                    .Description("The opt-outs to add or update.")),
            ],
            ["OptOuts"]);
    }
}
