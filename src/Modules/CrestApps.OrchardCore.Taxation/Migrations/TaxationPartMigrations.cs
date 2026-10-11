using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Taxation.Migrations;

/// <summary>
/// Defines the <c>TaxationPart</c> so it can be attached to any content type.
/// </summary>
public sealed class TaxationPartMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxationPartMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public TaxationPartMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Creates the taxation part definition.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(TaxationConstants.Parts.TaxationPart, part => part
            .Attachable()
            .WithDisplayName("Taxation")
            .WithDescription("Classifies a content item for taxation without storing a tax rate."));

        return 1;
    }
}
