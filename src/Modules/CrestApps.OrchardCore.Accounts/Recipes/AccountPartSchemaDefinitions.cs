using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using Json.Schema;

namespace CrestApps.OrchardCore.Accounts.Recipes;

/// <summary>
/// Describes <c>AccountPart</c>, the part that makes a content type an account type, to CrestApps Recipes.
/// </summary>
internal sealed class AccountPartSchemaDefinition : PartSchemaDefinitionBase
{
    public override string Name => AccountsConstants.ContentParts.Account;

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                (nameof(AccountPartSettings), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Description("The settings of the account part.")
                    .Properties(
                        (nameof(AccountPartSettings.OfferedContentTypes), new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                            .Description("The record types (such as contacts and opportunities) already added to the account type's list. The Accounts feature adds every new record type to the list once and records it here, so a type an administrator removes from the list is not added back.")))
                    .AdditionalProperties(true)))
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("An account part holds no values of its own; the account's members, locations and other data live in their own parts.")
            .AdditionalProperties(true);
}

/// <summary>
/// Describes <c>AccountMembersPart</c>, the users who sign in for an account, to CrestApps Recipes.
/// </summary>
internal sealed class AccountMembersPartSchemaDefinition : PartSchemaDefinitionBase
{
    public override string Name => AccountsConstants.ContentParts.Members;

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                (nameof(AccountMembersPart.Members), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Description("The users who sign in for the account.")
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            (nameof(AccountMember.UserId), new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The user id of the member.")),
                            (nameof(AccountMember.UserName), new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("The user name, as it was when the member was saved.")),
                            (nameof(AccountMember.Email), new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("The email, as it was when the member was saved.")),
                            (nameof(AccountMember.Role), new JsonSchemaBuilder()
                                .Type(SchemaValueType.String, SchemaValueType.Integer)
                                .Enum("Member", "Admin", 0, 1)
                                .Description("What the member may do for the account: Member (0) acts for the account in what they do themselves; Admin (1) also manages the account for the other members.")),
                            (nameof(AccountMember.ContactContentItemId), new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("The content item id of the contact record of the member, when there is one.")))
                        .Required(nameof(AccountMember.UserId))
                        .AdditionalProperties(true))))
            .AdditionalProperties(true);
}

/// <summary>
/// Describes <c>AccountLocationsPart</c>, the addresses of an account, to CrestApps Recipes.
/// </summary>
internal sealed class AccountLocationsPartSchemaDefinition : PartSchemaDefinitionBase
{
    public override string Name => AccountsConstants.ContentParts.Locations;

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                (nameof(AccountLocationsPart.Locations), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Description("The account's addresses, such as its offices and delivery sites.")
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            (nameof(AccountLocation.LocationId), new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The identifier of the location within the account, which orders and other records refer to.")),
                            (nameof(AccountLocation.Name), new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description("The name of the location, such as Head office.")),
                            (nameof(AccountLocation.Address), Address()))
                        .AdditionalProperties(true))))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder Address()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object, SchemaValueType.Null)
            .Description("The postal address of the location.")
            .Properties(
                ("Name", Text("The name of the recipient.")),
                ("Company", Text("The company name.")),
                ("AddressLine1", Text("The first street line.")),
                ("AddressLine2", Text("The second street line.")),
                ("DependentLocality", Text("The neighborhood or suburb, where a country uses one.")),
                ("SortingCode", Text("The sorting code, where a country uses one.")),
                ("Country", Text("The ISO 3166-1 alpha-2 country code, such as US.")),
                ("Region", Text("The state, province or region code.")),
                ("County", Text("The county.")),
                ("City", Text("The city or locality.")),
                ("District", Text("The district.")),
                ("PostalCode", Text("The postal or ZIP code.")),
                ("Phone", Text("A phone number for the address.")),
                ("Latitude", new JsonSchemaBuilder().Type(SchemaValueType.Number, SchemaValueType.Null).Description("The latitude, when the address was geocoded.")),
                ("Longitude", new JsonSchemaBuilder().Type(SchemaValueType.Number, SchemaValueType.Null).Description("The longitude, when the address was geocoded.")),
                ("GeocodedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Format(Formats.DateTime).Description("When the address was geocoded.")),
                ("GeocodeSource", Text("The service that geocoded the address.")))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String, SchemaValueType.Null).Description(description);
}
