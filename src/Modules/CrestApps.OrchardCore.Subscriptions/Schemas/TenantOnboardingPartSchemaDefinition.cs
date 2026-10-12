using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Subscriptions.Schemas;

/// <summary>
/// Provides recipe schema support for the <see cref="TenantOnboardingPart"/> payload.
/// </summary>
public sealed class TenantOnboardingPartSchemaDefinition : PartSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name => nameof(TenantOnboardingPart);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("RecipeName", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The setup recipe name used to initialize the provisioned tenant.")),
                ("FeatureProfile", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The feature profile applied to the provisioned tenant.")))
            .AdditionalProperties(true);
}
