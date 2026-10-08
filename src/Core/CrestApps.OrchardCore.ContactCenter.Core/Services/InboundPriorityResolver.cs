using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InboundPriorityResolver"/> class.
    /// </summary>
    /// <param name="contributors">The contributors.</param>
    /// <param name="logger">The logger.</param>
    public InboundPriorityResolver(
        IEnumerable<IInboundPriorityContributor> contributors,
        ILogger<InboundPriorityResolver> logger)
    {
        _contributors = contributors.OrderBy(contributor => contributor.Order).ToArray();
        _logger = logger;
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
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // A contributor reads the CRM, and a caller must not be dropped because a lookup failed.
                // Landing at the configured priority is a worse outcome than the treatment they were owed, and a
                // far better one than not reaching anybody.
                _logger.LogWarning(
                    ex,
                    "The inbound priority contributor {Contributor} failed for queue {QueueId}; the caller keeps the priority reached so far.",
                    contributor.GetType().Name.SanitizeLogValue(),
                    context.QueueId.SanitizeLogValue());

                continue;
            }

            if (contribution is not null && contribution.Value > priority)
            {
                // Which reason raised a caller is what an operator asks when one call jumps the line, so it is said.
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "The inbound priority contributor {Contributor} raised the caller for queue {QueueId} from {FromPriority} to {ToPriority}.",
                        contributor.GetType().Name.SanitizeLogValue(),
                        context.QueueId.SanitizeLogValue(),
                        priority.ToString().SanitizeLogValue(),
                        contribution.Value.ToString().SanitizeLogValue());
                }

                priority = contribution.Value;
            }
        }

        return priority;
    }
}
