using CrestApps.OrchardCore.Learning.Packages.Core.Indexes;
using CrestApps.OrchardCore.Learning.Packages.Drivers;
using CrestApps.OrchardCore.Learning.Packages.Migrations;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Packages.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Packages;

/// <summary>
/// Registers SCORM 1.2 learning packages: the learning package course item with its part, the import page, the package
/// panel of the learner player, the sandboxed file endpoint and the runtime endpoint, the permission and personal data.
/// </summary>
[Feature(PackagesConstants.Features.Scorm)]
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(PackagesConstants.CollectionName));
        services.AddDataMigration<PackagesMigrations>()
            .AddIndexProvider<PackageRuntimeStateIndexProvider>();

        services.AddLearningPackagesCore();
        services.AddContentPart<LearningPackagePart>()
            .UseDisplayDriver<LearningPackagePartDisplayDriver>()
            .UseDisplayDriver<LearningPackagePartLearnerDisplayDriver>();

        services.AddPermissionProvider<PackagesPermissionProvider>();
    }
}
