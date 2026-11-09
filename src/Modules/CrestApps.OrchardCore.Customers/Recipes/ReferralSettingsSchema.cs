using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Customers.Recipes;

/// <summary>
/// Describes the <c>ReferralSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class ReferralSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(ReferralSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("What a referred friend gets and what the customer who referred them gets, under Settings > Commerce > Referrals.")
            .Properties(
                ("FriendCouponId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The coupon each referred friend gets a single-use code of, or null for none.")),
                ("RewardKind", new JsonSchemaBuilder().AnyOf(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("None", "StoreCredit", "Points"), new JsonSchemaBuilder().Type(SchemaValueType.Integer)).Description("What the customer who referred a friend gets for the friend's first order: None, StoreCredit (RewardAmount) or Points (RewardPoints).")),
                ("RewardAmount", new JsonSchemaBuilder().Type(SchemaValueType.Number).Default(10).Description("The store credit the customer gets for a friend's first order, in the checkout currency.")),
                ("RewardPoints", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(500).Description("The loyalty points the customer gets for a friend's first order.")),
                ("MinimumOrderAmount", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The least a friend's first order must spend on goods for the customer to be rewarded.")))
            .AdditionalProperties(true);
}
