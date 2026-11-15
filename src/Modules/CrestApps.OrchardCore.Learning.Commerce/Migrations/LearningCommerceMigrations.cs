using CrestApps.OrchardCore.Learning.Commerce.Core.Indexes;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Programs;
using CrestApps.OrchardCore.Products.Core.Listing;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Learning.Commerce.Migrations;

/// <summary>
/// Creates the tables of Learning commerce and makes courses and learning paths products.
/// </summary>
internal sealed class LearningCommerceMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public LearningCommerceMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await LearningCommerceIndexSchema.CreateAsync(SchemaBuilder);

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(LearningProductPart), part => part
            .Attachable()
            .WithDisplayName("Learning Product")
            .WithDescription("What buying the product grants: the course or path itself, other courses, or seats for an account."));

        foreach (var type in new[] { LearningConstants.ContentTypes.Course, ProgramsConstants.LearningPathContentType })
        {
            if (await _contentDefinitionManager.GetTypeDefinitionAsync(type) is null)
            {
                continue;
            }

            await _contentDefinitionManager.AlterTypeDefinitionAsync(type, definition => definition
                .WithPart("ProductPart", part => part
                    .WithPosition("30")
                    .WithSettings(new ProductPageSettings { ShowDetails = true }))
                .WithPart(nameof(LearningProductPart), part => part.WithPosition("31")));
        }

        return 1;
    }
}
