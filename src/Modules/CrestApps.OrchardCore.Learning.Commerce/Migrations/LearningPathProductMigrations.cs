using CrestApps.OrchardCore.Learning.Programs;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Learning.Commerce.Migrations;

/// <summary>
/// Sells learning paths on their pages when Learning Paths is turned on with or after Learning Commerce, so the path type
/// gets the product parts the commerce migration gave the types that existed when it ran. When both features are turned
/// on together, Orchard Core may reach this migration before the Programs migration that creates the path type, so it
/// runs the Programs migrations first in the same activation. Only if the type still does not exist is nothing recorded,
/// so Orchard runs this again at the next start of the tenant; it is registered last in its feature
/// (<see cref="LearningPathsStartup.Order"/>), because a 0 also stops the migrations registered after it.
/// </summary>
internal sealed class LearningPathProductMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IServiceProvider _services;

    public LearningPathProductMigrations(IContentDefinitionManager contentDefinitionManager, IServiceProvider services)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _services = services;
    }

    public async Task<int> CreateAsync()
    {
        if (await _contentDefinitionManager.GetTypeDefinitionAsync(ProgramsConstants.LearningPathContentType) is null)
        {
            // The migration manager is resolved here, not injected: it is built from every migration, this one included.
            // It runs each feature once per activation, so the Programs migrations are not run twice.
            await _services.GetRequiredService<IDataMigrationManager>().UpdateAsync(ProgramsConstants.Features.Paths);
        }

        if (await _contentDefinitionManager.GetTypeDefinitionAsync(ProgramsConstants.LearningPathContentType) is null)
        {
            return 0;
        }

        await LearningCommerceProductTypes.SellOnItsPageAsync(_contentDefinitionManager, ProgramsConstants.LearningPathContentType);

        return 1;
    }
}
