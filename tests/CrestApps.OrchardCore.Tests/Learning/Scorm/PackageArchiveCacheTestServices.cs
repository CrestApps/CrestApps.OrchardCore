using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// Test services have no tenant, so the package archive cache is kept in a temporary folder.
/// </summary>
internal static class PackageArchiveCacheTestServices
{
    public static void Use(IServiceCollection services)
        => services.Replace(ServiceDescriptor.Singleton(serviceProvider => new PackageArchiveCache(
            Path.Combine(Path.GetTempPath(), "package-cache-tests"),
            serviceProvider.GetRequiredService<IClock>(),
            NullLogger.Instance)));
}
