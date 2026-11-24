using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Packages.Recipes;

/// <summary>
/// Describes the <c>LearningPackageContentHostSettings</c> member of Orchard Core's <c>Settings</c> recipe step to
/// CrestApps Recipes.
/// </summary>
internal sealed class LearningPackageContentHostSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(LearningPackageContentHostSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The learning package settings.")
            .Properties(
                (nameof(LearningPackageContentHostSettings.BaseUrl), new JsonSchemaBuilder()
                    .Type(SchemaValueType.String, SchemaValueType.Null)
                    .Description("The base URL of the package content host, such as https://packages.example.com: http or https, a host and an optional port, nothing after it. Empty serves package files from the site's own host in strict mode.")))
            .AdditionalProperties(true);
}
