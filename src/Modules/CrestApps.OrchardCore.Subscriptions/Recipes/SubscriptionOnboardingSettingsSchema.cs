using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Subscriptions.Recipes;

/// <summary>
/// Describes the <c>SubscriptionOnboardingSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class SubscriptionOnboardingSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(SubscriptionOnboardingSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Represents tenant onboarding settings used by subscription flows that provision sites.")
            .Properties(
                ("DatabaseProvider", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The database provider every provisioned site is created with.")),
                ("ProtectedConnectionString", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The connection string provisioned sites are created with. It is stored encrypted with this tenant's data protection keys, so a plain value set by the Settings step can't be read: set it in the admin.")),
                ("Schema", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The schema provisioned sites are created in, when the provider supports one.")))
            .AdditionalProperties(true);
}
