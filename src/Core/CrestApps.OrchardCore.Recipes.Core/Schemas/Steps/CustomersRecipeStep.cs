using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "Customers" recipe step — adds or updates guest and business customers. A customer is matched by
/// <c>ItemId</c>, then by the email of a guest; customers with an account, and erased or merged customers, are left alone.
/// </summary>
public sealed class CustomersRecipeStep : RecipeStepSchemaBase
{
    /// <inheritdoc/>
    public override string Name => "Customers";

    /// <inheritdoc/>
    protected override JsonSchema CreateSchema()
    {
        var address = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The postal address.")
            .Properties(
                ("Name", NullableString("The name of the recipient.")),
                ("Company", NullableString("The company of the recipient.")),
                ("AddressLine1", NullableString("The first street address line.")),
                ("AddressLine2", NullableString("The second street address line, such as an apartment or suite.")),
                ("DependentLocality", NullableString("The dependent locality, such as a neighborhood, where the country uses one.")),
                ("SortingCode", NullableString("The sorting code, where the country uses one.")),
                ("Country", NullableString("The ISO 3166-1 alpha-2 country code, for example 'US' or 'CA'.")),
                ("Region", NullableString("The state, province or region code, for example 'WA' or 'ON'.")),
                ("County", NullableString("The county, where the country uses one.")),
                ("City", NullableString("The city or locality.")),
                ("District", NullableString("The district, where the country uses one.")),
                ("PostalCode", NullableString("The postal or ZIP code.")),
                ("Phone", NullableString("The phone number for deliveries to this address.")))
            .AdditionalProperties(true);

        var customerAddress = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("AddressId", NullableString("The identifier of the address within the customer's address book. Generated when empty. 'DefaultBillingAddressId' and 'DefaultShippingAddressId' refer to it.")),
                ("Label", NullableString("A label for the address, for example 'Home' or 'Office'.")),
                ("Address", address))
            .Required("Address")
            .AdditionalProperties(true);

        var customer = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("A customer. It needs a name, a company or an email.")
            .Properties(
                ("ItemId", NullableString("The stable identifier of the customer. Preserved across environments; generated when empty or invalid.")),
                ("Kind", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Person", "Business").Default("Person").Description("Whether the customer is a person or a business.")),
                ("DisplayName", NullableString("The name shown for the customer. When empty, the person's name or the company name is shown.")),
                ("FirstName", NullableString("The person's first name.")),
                ("LastName", NullableString("The person's last name.")),
                ("CompanyName", NullableString("The company name of a business customer.")),
                ("TaxId", NullableString("The tax identifier of a business customer.")),
                ("Email", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.Email).Description("The customer's email. A guest with the same email is updated rather than duplicated.")),
                ("Phone", NullableString("The customer's phone number.")),
                ("AcceptsMarketing", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the customer accepts marketing.")),
                ("Addresses", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(customerAddress)
                    .Description("The customer's address book. It replaces the stored one.")),
                ("DefaultBillingAddressId", NullableString("The 'AddressId' of the default billing address.")),
                ("DefaultShippingAddressId", NullableString("The 'AddressId' of the default shipping address.")))
            .AdditionalProperties(true);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("Customers", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(customer)
                    .MinItems(1)
                    .Description("The customers to add or update.")),
            ],
            ["Customers"]);
    }

    private static JsonSchemaBuilder NullableString(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);
}
