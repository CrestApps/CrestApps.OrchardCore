using CrestApps.OrchardCore.Classifieds.Indexes;
using CrestApps.OrchardCore.Classifieds.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Classifieds.Migrations;

/// <summary>
/// Creates the index of posted listings, and the attachable Classified and Listing upgrade parts.
/// </summary>
internal sealed class ClassifiedMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ClassifiedMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ClassifiedIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<string>("Owner", column => column.Nullable().WithLength(255))
            .Column<string>("DisplayText", column => column.Nullable().WithLength(255))
            .Column<int>("Status")
            .Column<DateTime>("ExpiresUtc", column => column.Nullable())
            .Column<DateTime>("FeaturedUntilUtc", column => column.Nullable())
            .Column<DateTime>("SortUtc", column => column.Nullable())
            .Column<double>("Latitude", column => column.Nullable())
            .Column<double>("Longitude", column => column.Nullable())
            .Column<bool>("Published")
            .Column<bool>("Latest")
        );

        await IndexTableIndexCreation.CreateIfMissingAsync<ClassifiedIndex>(SchemaBuilder, "IDX_ClassifiedIndex_Status", collection: null, "Status", "Latest", "ExpiresUtc");
        await IndexTableIndexCreation.CreateIfMissingAsync<ClassifiedIndex>(SchemaBuilder, "IDX_ClassifiedIndex_Owner", collection: null, "Owner", "Latest");
        await IndexTableIndexCreation.CreateIfMissingAsync<ClassifiedIndex>(SchemaBuilder, "IDX_ClassifiedIndex_Place", collection: null, "Published", "Latitude", "Longitude");

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(ClassifiedPart), part => part
            .Attachable()
            .WithDisplayName("Classified")
            .WithDescription("Makes a content type something visitors post from the site, moderated, running for a term, with a place and paid upgrades.")
        );

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(ClassifiedUpgradePart), part => part
            .Attachable()
            .WithDisplayName("Listing upgrade")
            .WithDescription("Makes a product an upgrade for a posted listing: featuring it for some days, or bumping it to the top.")
        );

        return 1;
    }
}
