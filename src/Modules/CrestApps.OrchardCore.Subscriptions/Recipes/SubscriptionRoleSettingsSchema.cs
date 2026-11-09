using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Subscriptions.Recipes;

/// <summary>
/// Describes the <c>SubscriptionRoleSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class SubscriptionRoleSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(SubscriptionRoleSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Represents role assignment settings for users created during subscription registration.")
            .Properties(
                ("RoleNames", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The role names assigned to newly created subscription users.")))
            .AdditionalProperties(true);
}
