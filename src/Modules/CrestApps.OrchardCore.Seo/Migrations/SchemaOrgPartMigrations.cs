using CrestApps.OrchardCore.Seo.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Seo.Migrations;

/// <summary>
/// Defines the attachable Schema.org part.
/// </summary>
internal sealed class SchemaOrgPartMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaOrgPartMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public SchemaOrgPartMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Defines the part.
    /// </summary>
    /// <returns>The version of the migration.</returns>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(SchemaOrgPart), part => part
            .Attachable()
            .WithDisplayName("Schema.org")
            .WithDescription("Publishes the content item as schema.org structured data (JSON-LD): choose its schema.org type and where each property comes from in the part's settings.")
        );

        return 1;
    }
}
