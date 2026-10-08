using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Asks each registered <see cref="IEntryPointResolver"/> in turn and takes the first that recognises the number.
/// <para>
/// The inbound processor previously took the first registered resolver and ignored the rest, so a second one —
/// the way a feature adds its own entry-point source — was silently never asked, and a number only it knew about
/// resolved to nothing while the caller was routed as though the tenant had never configured it. Either this is
/// one service or it is a chain; making it a chain is the answer that does not depend on registration order
/// being lucky.
/// </para>
/// </summary>
public sealed class EntryPointResolverChain : IEntryPointResolver
{
    private readonly IEntryPointResolver[] _resolvers;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntryPointResolverChain"/> class.
    /// </summary>
    /// <param name="resolvers">The resolvers to consult.</param>
    public EntryPointResolverChain(IEnumerable<IEntryPointResolver> resolvers)
    {
        _resolvers = resolvers
            .Where(resolver => resolver is not EntryPointResolverChain)
            .OrderBy(resolver => (resolver as IOrderedEntryPointResolver)?.Order ?? 0)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<ContactCenterEntryPoint> FindByDialedNumberAsync(string dialedNumber, CancellationToken cancellationToken = default)
    {
        foreach (var resolver in _resolvers)
        {
            var entryPoint = await resolver.FindByDialedNumberAsync(dialedNumber, cancellationToken);

            if (entryPoint is not null)
            {
                return entryPoint;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<EntryPointRoutingPlan> ResolveAsync(string dialedNumber, CancellationToken cancellationToken = default)
    {
        foreach (var resolver in _resolvers)
        {
            var plan = await resolver.ResolveAsync(dialedNumber, cancellationToken);

            if (plan is not null)
            {
                return plan;
            }
        }

        // A tenant with inbound voice and no entry point for this number is a normal state, and the caller is
        // handled by the processor's own fallback rather than by inventing a plan here.
        return null;
    }
}
