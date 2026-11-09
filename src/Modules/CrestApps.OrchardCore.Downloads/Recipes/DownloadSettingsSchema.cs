using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Downloads.Recipes;

/// <summary>
/// Describes the <c>DownloadSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class DownloadSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(DownloadSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("How long buyers' download links work, and how often, unless a product says otherwise.")
            .Properties(
                ("DownloadLimit", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(5).Description("How many times a buyer can download each file. Zero means no limit.")),
                ("LinkLifetimeDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(30).Description("How many days a buyer's links work. Zero means they never expire.")))
            .AdditionalProperties(true);
}
