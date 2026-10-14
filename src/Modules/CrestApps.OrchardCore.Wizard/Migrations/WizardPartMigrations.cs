using CrestApps.OrchardCore.Wizard.Core.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Wizard.Migrations;

/// <summary>
/// Defines the <see cref="WizardPart"/> so it can be attached to any content type from the admin.
/// </summary>
public sealed class WizardPartMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="WizardPartMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The manager used to define the wizard part.</param>
    public WizardPartMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Creates the initial <see cref="WizardPart"/> definition.
    /// </summary>
    /// <returns>The version number this migration upgrades the feature to.</returns>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(WizardPart), builder => builder
            .Attachable()
            .WithDescription("Turns a content item into a multi-step wizard whose steps are content items."));

        return 1;
    }
}
