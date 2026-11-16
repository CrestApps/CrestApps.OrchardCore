using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Learning packages core.
/// </summary>
public static class LearningPackagesCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the learning package course item kind, the protected file area of packages (with no access policy, so
    /// no signed link opens a package file), the importer, the runtime state store and service, and the launch tokens.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLearningPackagesCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<LearningPackageOptions>();
        services.AddProtectedFileArea<LearningPackageFiles>(PackagesConstants.FilesFolder);
        services.TryAddScoped<LearningPackageImporter>();
        services.TryAddScoped<PackageRuntimeStore>();
        services.TryAddScoped<IPackageContentSource, DefaultPackageContentSource>();
        services.TryAddScoped<PackageLaunchTokens>();
        services.TryAddScoped<PackageRuntimeService>();
        services.AddCourseItemKind<LearningPackagePart>(PackagesConstants.PackageKind, kind =>
        {
            kind.DisplayName = LocalizationSource.Create("Learning package", typeof(LearningPackagesCoreServiceCollectionExtensions));
            kind.LearnerName = LocalizationSource.Create("Lesson", typeof(LearningPackagesCoreServiceCollectionExtensions));
            kind.ContentType = PackagesConstants.ContentTypes.Package;

            // Reported by the package first: the browser reports it, so the result is marked self-reported.
            kind.CompletionModes.Add(CompletionModes.PackageReported);
            kind.CompletionModes.Add(CompletionModes.Passed);
            kind.CompletionModes.Add(CompletionModes.MarkedDone);
            kind.ProducesGrade = true;
            kind.Icon = "fa-solid fa-box-open";
        });

        return services;
    }
}
