using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Recipes;

/// <summary>
/// Describes the <c>EmailSuppressions</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class EmailSuppressionsRecipeStepSchema : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => EmailSuppressionsStep.StepKey;

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
    {
        var entry = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("An email address the business no longer sends to.")
            .Properties(
                ("Address", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The email address.")),
                ("Reason", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("HardBounce", "RepeatedSoftBounces", "Complaint", "Manual").Default("Manual").Description("Why the address is suppressed.")),
                ("Detail", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("What the mail server or the provider said, or a note.")))
            .Required("Address")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Adds email addresses to the suppression list, matched by address. An address already on the list keeps its first reason, so running the step twice adds nothing.")
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(Name).Description($"Recipe step discriminator. Must be '{Name}'.")),
                (EmailSuppressionsStep.EntriesName, new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(entry).Description("The addresses to suppress.")))
            .Required("name", EmailSuppressionsStep.EntriesName)
            .AdditionalProperties(true)
            .Build();
    }
}
