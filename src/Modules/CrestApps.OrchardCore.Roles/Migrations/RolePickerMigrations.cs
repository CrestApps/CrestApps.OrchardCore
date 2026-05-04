using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Roles.Migrations;

internal sealed class RolePickerMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="RolePickerMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public RolePickerMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("RolePickerPart", part => part
            .WithDisplayName("Role Picker")
            .WithDescription("Allows you to select one or more roles")
            .Attachable()
            .Reusable()
            .WithDefaultPosition("999")
        );

        return 1;
    }
}
