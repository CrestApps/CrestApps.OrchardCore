using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Indexing;

/// <summary>
/// Says whether a document of a search index may be copied into a store whose readers don't pass the source's own access
/// checks, such as an AI knowledge base that answers anyone allowed to chat with a profile. The module that restricts
/// who may read a record implements it: Content Access Control says a content item restricted to some roles isn't for
/// everyone. A document no implementation restricts is for everyone. Register with
/// <see cref="IndexedDocumentVisibilityServiceCollectionExtensions.AddIndexedDocumentVisibility{TVisibility}"/>.
/// </summary>
public interface IIndexedDocumentVisibility
{
    /// <summary>
    /// Returns whether everyone, signed in or not, may read the record a search index document holds.
    /// </summary>
    /// <param name="documentId">The document's id in the source index, such as a content item id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when the record is restricted.</returns>
    Task<bool> IsForEveryoneAsync(string documentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Registers who may read the records of search index documents.
/// </summary>
public static class IndexedDocumentVisibilityServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="IIndexedDocumentVisibility"/>.
    /// </summary>
    /// <typeparam name="TVisibility">The visibility.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddIndexedDocumentVisibility<TVisibility>(this IServiceCollection services)
        where TVisibility : class, IIndexedDocumentVisibility
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IIndexedDocumentVisibility, TVisibility>());

        return services;
    }

    /// <summary>
    /// Returns whether every registered <see cref="IIndexedDocumentVisibility"/> says the record is for everyone.
    /// </summary>
    /// <param name="visibilities">The registered visibilities.</param>
    /// <param name="documentId">The document's id in the source index.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when any of them restricts it.</returns>
    public static async Task<bool> IsForEveryoneAsync(this IEnumerable<IIndexedDocumentVisibility> visibilities, string documentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visibilities);

        if (string.IsNullOrEmpty(documentId))
        {
            return true;
        }

        foreach (var visibility in visibilities)
        {
            if (!await visibility.IsForEveryoneAsync(documentId, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }
}
