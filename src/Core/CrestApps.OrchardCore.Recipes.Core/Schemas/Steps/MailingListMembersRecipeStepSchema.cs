using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "MailingListMembers" recipe step of the Mailing Lists module: creates or updates the members of
/// mailing lists, matched by <c>ItemId</c>, then by list and address, with the evidence of their consent. Members are
/// personal data. Importing them sends no e-mail and gives no welcome coupon.
/// </summary>
public sealed class MailingListMembersRecipeStepSchema : RecipeStepSchemaBase
{
    /// <summary>
    /// The member of the step that holds the members.
    /// </summary>
    public const string EntriesName = "Members";

    /// <inheritdoc/>
    public override string Name => "MailingListMembers";

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
    {
        var contact = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The person's name.")),
                ("Email", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Email).Description("The e-mail address.")))
            .Required("Email")
            .AdditionalProperties(true)
            .Description("The person's contact details.");

        var consent = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object | SchemaValueType.Null)
            .Properties(
                ("Text", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The consent text the person agreed to.")),
                ("Form", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("Where the person agreed.")),
                ("AgreedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.DateTime).Description("When the person agreed, in UTC.")),
                ("ClientAddress", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The address the request came from.")))
            .AdditionalProperties(true)
            .Description("The evidence of the person's consent.");

        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                (EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", CatalogRecipeStepSchemas.ItemId("member")),
                            ("ListId", new JsonSchemaBuilder().Type(SchemaValueType.String).MinLength(1).Description("The id of the mailing list.")),
                            ("Contact", contact),
                            ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Pending", "Subscribed", "Unsubscribed", "Cleaned").Description("The member's state.")),
                            ("Source", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("How the member joined, such as Widget, Admin, Import or Provider.")),
                            ("EmailVerifiedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description("When the person proved they own the address, in UTC.")),
                            ("SubscribedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description("When the member was last subscribed, in UTC.")),
                            ("UnsubscribedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description("When the member was last unsubscribed or cleaned, in UTC.")),
                            ("StatusChangedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.DateTime).Description("When the member's state last changed, in UTC.")),
                            ("Consent", consent),
                            ("ProviderMemberId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The member's id at the list's provider.")),
                            ("Properties", CatalogRecipeStepSchemas.Properties("member")))
                        .Required("ListId", "Contact")
                        .AdditionalProperties(true))
                    .Description("The members to create or update.")),
            ],
            [EntriesName]);
    }
}
