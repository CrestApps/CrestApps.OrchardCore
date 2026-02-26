using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Composite link resolver that dispatches to a keyed <see cref="IAIReferenceLinkResolver"/>
/// based on the reference type. Falls back to <c>null</c> when no resolver is registered.
/// </summary>
public sealed class CompositeAIReferenceLinkResolver
{
    private readonly IServiceProvider _serviceProvider;

    public CompositeAIReferenceLinkResolver(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Resolves a link URL for the given reference by dispatching to the appropriate
    /// keyed <see cref="IAIReferenceLinkResolver"/> for the specified reference type.
    /// </summary>
    /// <param name="referenceId">The unique identifier of the referenced resource.</param>
    /// <param name="referenceType">The type of reference source (used as the service key).</param>
    /// <param name="metadata">Optional metadata associated with the reference.</param>
    /// <returns>The resolved link URL, or <c>null</c> if no resolver is registered or the resolver returns null.</returns>
    public string ResolveLink(string referenceId, string referenceType, IDictionary<string, object> metadata)
    {
        if (string.IsNullOrEmpty(referenceId) || string.IsNullOrEmpty(referenceType))
        {
            return null;
        }

        var resolver = _serviceProvider.GetKeyedService<IAIReferenceLinkResolver>(referenceType);

        return resolver?.ResolveLink(referenceId, metadata);
    }
}
