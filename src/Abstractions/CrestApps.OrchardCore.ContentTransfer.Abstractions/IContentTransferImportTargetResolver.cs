using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// Finds the target of an import by its name: a registered <see cref="IContentTransferRecordImporter"/>, or a content
/// type that allows bulk import.
/// </summary>
public interface IContentTransferImportTargetResolver
{
    /// <summary>
    /// Finds a target by name.
    /// </summary>
    /// <param name="name">The target name: a content type, or a registered importer's name.</param>
    /// <returns>The target, or <see langword="null"/> when nothing of that name can be imported.</returns>
    Task<IContentTransferImportTarget> FindAsync(string name);

    /// <summary>
    /// Lists the targets that are not content types and that a person may import.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <returns>The targets.</returns>
    Task<IReadOnlyList<IContentTransferRecordImporter>> ListRecordImportersAsync(ClaimsPrincipal user);

    /// <summary>
    /// Tells whether a person may import a target, using the target's own check.
    /// </summary>
    /// <param name="user">The person.</param>
    /// <param name="name">The target name.</param>
    /// <returns><see langword="true"/> when the target exists and they may.</returns>
    Task<bool> CanImportAsync(ClaimsPrincipal user, string name);
}

/// <summary>
/// Registers ContentTransfer import targets.
/// </summary>
public static class ContentTransferImportTargetServiceCollectionExtensions
{
    /// <summary>
    /// Registers a target that is not a content type, so its records can be imported from a file.
    /// </summary>
    /// <typeparam name="TImporter">The importer.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddContentTransferImportTarget<TImporter>(this IServiceCollection services)
        where TImporter : class, IContentTransferRecordImporter
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IContentTransferRecordImporter, TImporter>());

        return services;
    }
}
