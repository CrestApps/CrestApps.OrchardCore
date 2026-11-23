using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "MailingLists" recipe step of the Mailing Lists module: creates or updates mailing lists, matched by
/// <c>ItemId</c>.
/// </summary>
public sealed class MailingListsRecipeStepSchema : RecipeStepSchemaBase
{
    /// <summary>
    /// The member of the step that holds the lists.
    /// </summary>
    public const string EntriesName = "MailingLists";

    /// <inheritdoc/>
    public override string Name => "MailingLists";

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
        => RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                (EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", CatalogRecipeStepSchemas.ItemId("mailing list")),
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).MaxLength(128).Pattern("^[A-Za-z][A-Za-z0-9_-]*$").Description("The technical name, unique in the tenant: letters, digits, dashes and underscores, starting with a letter.")),
                            ("DisplayText", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The name people see on the sign-up form and the e-mail preferences page.")),
                            ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("What the list sends and how often.")),
                            ("ProviderName", new JsonSchemaBuilder().Type(SchemaValueType.String).Default("Local").Description("The provider that keeps the list, such as 'Local' (the site) or 'Mailchimp'.")),
                            ("Confirmation", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("None", "Email", "Provider").Default("Email").Description("How a sign-up is confirmed: single opt-in, double opt-in by the site's e-mail, or by the provider's own e-mail.")),
                            ("SkipConfirmationForVerifiedUsers", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether a signed-in user with a confirmed account e-mail is subscribed without a confirmation e-mail.")),
                            ("ConfirmationLinkLifetime", new JsonSchemaBuilder().Type(SchemaValueType.String).Default("7.00:00:00").Description("How long a confirmation link works, as a time span between one hour and 30 days.")),
                            ("ConsentText", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The consent text shown on the sign-up form and kept on each member as evidence.")),
                            ("SendWelcomeEmail", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether a welcome e-mail is sent when a person is subscribed.")),
                            ("IsEnabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether the list is offered on the e-mail preferences page and in sign-up widgets.")),
                            ("Properties", CatalogRecipeStepSchemas.Properties("mailing list")))
                        .Required("Name", "DisplayText")
                        .AdditionalProperties(true))
                    .Description("The mailing lists to create or update by id.")),
            ],
            [EntriesName]);
}
