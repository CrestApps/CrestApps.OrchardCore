using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Takes the strongest of the configured priority and everything the contributors notice.
/// <para>
/// The strongest wins rather than the last, because contributors describe independent reasons a caller matters —
/// a VIP account, a returning callback, a second call the same afternoon — and letting a weak reason cancel a
/// strong one would mean the order contributors happened to be registered in decided who got answered first.
/// </para>
/// </summary>
public sealed class InboundPriorityResolver : IInboundPriorityResolver
{
    private readonly IInboundPriorityContributor[] _contributors;

    /// <summary>
    /// Initializes a new instance of the <see cref="InboundPriorityResolver"/> class.
    /// </summary>
    /// <param name="contributors">The contributors.</param>
    public InboundPriorityResolver(IEnumerable<IInboundPriorityContributor> contributors)
    {
        _contributors = contributors.OrderBy(contributor => contributor.Order).ToArray();
    }

    /// <inheritdoc/>
    public async Task<InteractionPriority> ResolveAsync(InboundPriorityContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var priority = context.ConfiguredPriority;

        foreach (var contributor in _contributors)
        {
            InteractionPriority? contribution;

            try
            {
                contribution = await contributor.ContributeAsync(context, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // A contributor reads the CRM, and a caller must not be dropped because a lookup failed.
                // Landing at the configured priority is a worse outcome than the treatment they were owed, and a
                // far better one than not reaching anybody.
                continue;
            }

            if (contribution is not null && contribution.Value > priority)
            {
                priority = contribution.Value;
            }
        }

        return priority;
    }
}
