using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Checkout.Recipes;

/// <summary>
/// Describes the <c>Coupons</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class CouponsRecipeStepSchema : RecipeStepSchemaBase
{
    public override string Name => CheckoutConstants.CouponsRecipeStep;

    protected override JsonSchema CreateSchema()
    {
        var coupon = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("A coupon or automatic discount. It is matched by Code (without case sensitivity) and checked with the same rules as the coupon editor. How often it was used is never imported.")
            .Properties(
                ("Code", RecipeStepSchemaBuilders.String().MinLength(1).Description("The code the buyer enters, which the step matches coupons by. An automatic discount needs one too: it identifies the discount's uses.")),
                ("Method", EnumValue<CouponMethod>("Whether the buyer enters the code (Code) or the discount applies by itself to every checkout that meets its conditions (Automatic). Defaults to Code.")),
                ("OnlyGeneratedCodes", RecipeStepSchemaBuilders.Boolean().Description("Whether only the unique codes generated for the coupon work, and not its own code.")),
                ("Description", NullableString("What the buyer sees on their invoice. Required for an automatic discount.")),
                ("Kind", EnumValue<CouponKind>("How the reduction is calculated. Defaults to Percentage.")),
                ("Percentage", RecipeStepSchemaBuilders.Number().Description("The percentage taken off (1 to 100) for Percentage and BuyXGetY coupons; 100 makes the items a buyer gets free.")),
                ("Amount", RecipeStepSchemaBuilders.Number().Description("The amount taken off for a FixedAmount coupon. Must be greater than zero.")),
                ("Currency", NullableString("The ISO-4217 currency a fixed amount is in. Required for a FixedAmount coupon, which only applies to invoices in that currency.")),
                ("Target", EnumValue<DiscountTarget>("What the coupon reduces: OneTime (one-time amount), FirstCycle (first recurring cycle only) or Shipping. Defaults to OneTime.")),
                ("IsEnabled", RecipeStepSchemaBuilders.Boolean().Description("Whether the coupon can be used. Defaults to true.")),
                ("StartsUtc", NullableDateTime("The UTC time the coupon starts being valid.")),
                ("EndsUtc", NullableDateTime("The UTC time the coupon stops being valid. Must be after StartsUtc.")),
                ("MaxRedemptions", NullableInteger("How many times the coupon may be used in total, or null for no limit.")),
                ("MaxRedemptionsPerCustomer", NullableInteger("How many times one customer (account, or guest email) may use the coupon, or null for no limit.")),
                ("MinimumAmount", NullableNumber("The amount the items the coupon applies to must reach.")),
                ("MaximumAmount", NullableNumber("The most the items the coupon applies to may come to, or null for no maximum. Can't be less than MinimumAmount.")),
                ("MinimumQuantity", NullableInteger("How many of the items it counts the order must have, or null for no minimum.")),
                ("BuyQuantity", RecipeStepSchemaBuilders.Integer().Description("For a BuyXGetY offer: how many qualifying items the buyer buys each time the offer applies (1 or more). Ignored for other kinds.")),
                ("GetQuantity", RecipeStepSchemaBuilders.Integer().Description("For a BuyXGetY offer: how many items the buyer gets at Percentage off each time the offer applies (1 or more). Ignored for other kinds.")),
                ("BuyProductIds", RecipeStepSchemaBuilders.StringArray().Description("For a BuyXGetY offer: the product content item ids that count toward what the buyer buys. Empty, with BuyCategoryIds empty, counts every item.")),
                ("BuyCategoryIds", RecipeStepSchemaBuilders.StringArray().Description("For a BuyXGetY offer: the category (taxonomy term) content item ids whose products count toward what the buyer buys.")),
                ("MaxApplicationsPerOrder", NullableInteger("For a BuyXGetY offer: the most times it applies to one order, or null for no limit.")),
                ("CombinesWithProductDiscounts", RecipeStepSchemaBuilders.Boolean().Description("Whether the coupon can be used with discounts off some products.")),
                ("CombinesWithOrderDiscounts", RecipeStepSchemaBuilders.Boolean().Description("Whether the coupon can be used with discounts off the order.")),
                ("CombinesWithShippingDiscounts", RecipeStepSchemaBuilders.Boolean().Description("Whether the coupon can be used with discounts off shipping.")),
                ("ProductIds", RecipeStepSchemaBuilders.StringArray().Description("The product content item ids the coupon is limited to. With CategoryIds empty too, it applies to every item.")),
                ("CategoryIds", RecipeStepSchemaBuilders.StringArray().Description("The category (taxonomy term) content item ids the coupon is limited to.")),
                ("Sources", RecipeStepSchemaBuilders.StringArray().Description("The checkout sources the coupon is limited to, matched without case sensitivity. Empty means any source.")),
                ("ReferenceTypes", RecipeStepSchemaBuilders.StringArray().Description("The reference types (what a checkout pays for, such as Transaction) the coupon is limited to. Empty means any.")),
                ("CustomerId", NullableString("The id of the one customer (user id or guest customer id) who may use the coupon, or null for anyone.")),
                ("CustomerEmail", NullableString("The email address of the one customer who may use the coupon, or null for anyone.")))
            .Required("Code")
            .AdditionalProperties(true);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [("Coupons", RecipeStepSchemaBuilders.Array(coupon).Description("The coupons to create, or to update when a stored coupon has the same code."))],
            ["Coupons"]);
    }

    private static JsonSchemaBuilder EnumValue<TEnum>(string description)
        where TEnum : struct, Enum
        => new JsonSchemaBuilder()
            .AnyOf(
                RecipeStepSchemaBuilders.String().Enum(System.Enum.GetNames<TEnum>()),
                RecipeStepSchemaBuilders.Integer())
            .Description(description);

    private static JsonSchemaBuilder NullableString(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

    private static JsonSchemaBuilder NullableDateTime(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description(description);

    private static JsonSchemaBuilder NullableInteger(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Description(description);

    private static JsonSchemaBuilder NullableNumber(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description(description);
}
