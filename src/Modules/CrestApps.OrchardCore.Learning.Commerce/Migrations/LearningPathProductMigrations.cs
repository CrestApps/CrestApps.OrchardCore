using CrestApps.OrchardCore.Learning.Programs;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Learning.Commerce.Migrations;

/// <summary>
/// Sells learning paths on their pages when Learning Paths is turned on with or after Learning Commerce, so the path type
/// gets the product parts the commerce migration gave the types that existed when it ran. When the path type does not
/// exist yet (the Programs migration has not run in this pass), nothing is recorded, so Orchard runs this again at the
/// next start of the tenant.
/// </summary>
internal sealed class LearningPathProductMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public LearningPathProductMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        if (await _contentDefinitionManager.GetTypeDefinitionAsync(ProgramsConstants.LearningPathContentType) is null)
        {
            return 0;
        }

        await LearningCommerceProductTypes.SellOnItsPageAsync(_contentDefinitionManager, ProgramsConstants.LearningPathContentType);

        return 1;
    }
}
