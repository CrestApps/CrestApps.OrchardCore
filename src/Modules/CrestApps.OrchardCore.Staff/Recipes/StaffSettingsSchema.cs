using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Staff.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Staff.Recipes;

/// <summary>
/// Describes the <c>StaffSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class StaffSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(StaffSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The Staff settings.")
            .Properties(
                (nameof(StaffSettings.LimitPickersToStaff), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether people pickers that ask for staff offer only working staff members.")))
            .AdditionalProperties(true);
}
