using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Reviews.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Reviews.Recipes;

/// <summary>
/// Describes the <c>ReviewSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class ReviewSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(ReviewSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("How product reviews are collected and shown.")
            .Properties(
                ("RequireApproval", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether a review waits for the store to approve it before it shows.")),
                ("BuyersOnly", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether only buyers who paid for the product can review it.")),
                ("SendReviewRequests", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether buyers who accept marketing are emailed, some days after paying, to review what they bought.")),
                ("ReviewRequestDelayDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(14).Description("The days after an order is paid its review request goes, such as 14 for time to use what came.")))
            .AdditionalProperties(true);
}
