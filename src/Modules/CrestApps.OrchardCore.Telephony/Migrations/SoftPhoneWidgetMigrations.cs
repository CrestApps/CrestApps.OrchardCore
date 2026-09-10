using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Telephony.Migrations;

/// <summary>
/// Creates the Soft Phone widget content type so an operator can place the floating soft phone on the front
/// end through Design &gt; Widgets, instead of it being auto-injected by a filter.
/// </summary>
internal sealed class SoftPhoneWidgetMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftPhoneWidgetMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public SoftPhoneWidgetMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("SoftPhonePart", part => part
            .WithDisplayName("Soft Phone")
            .WithDescription("Renders the floating soft phone for users allowed to use it.")
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync("SoftPhone", type => type
            .Draftable(false)
            .Listable(false)
            .Securable(false)
            .Creatable(false)
            .Versionable(false)
            .WithDisplayName("Soft Phone")
            .WithDescription("A widget that shows the floating soft phone on the site.")
            .WithPart("SoftPhonePart")
            .Stereotype("Widget")
        );

        return 1;
    }
}
