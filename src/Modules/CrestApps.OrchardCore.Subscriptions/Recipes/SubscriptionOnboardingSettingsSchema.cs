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
                ("Schema", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The schema provisioned sites are created in, when the provider supports one.")),
                ("AllowCustomPrefix", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether a buyer may choose the site's URL prefix. When false, the prefix is the site's name.")),
                ("AllowCustomDomains", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether a buyer may make the site answer on host names of their own.")),
                ("ReservedPrefixes", new JsonSchemaBuilder().Type(SchemaValueType.Array | SchemaValueType.Null).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("URL prefixes no sold site may take. The admin prefix is always reserved; null uses the built-in list.")),
                ("ReservedHosts", new JsonSchemaBuilder().Type(SchemaValueType.Array | SchemaValueType.Null).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("Host names no sold site may take, such as the operator's own. The default tenant's hosts are always reserved.")),
                ("BaseDomain", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The domain under which each sold site gets its own host, {name}.{BaseDomain}, when buyers may not name their own.")))
            .AdditionalProperties(true);
}
