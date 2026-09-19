using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.Core.ContactCenter.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Raises a caller who has already reached us recently. Somebody calling back the same afternoon did not get
/// what they needed the first time, and making them queue from scratch is how a small problem becomes a
/// complaint.
/// </summary>
public sealed class RepeatCallerPriorityContributor : IInboundPriorityContributor
{
    /// <summary>
    /// How recently a previous call counts as a repeat. Long enough to catch somebody working through a problem
    /// in one afternoon, short enough that a customer who calls monthly is not permanently promoted.
    /// </summary>
    public static readonly TimeSpan RepeatWindow = TimeSpan.FromHours(4);

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RepeatCallerPriorityContributor"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    public RepeatCallerPriorityContributor(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public int Order => 20;

    /// <inheritdoc/>
    public Task<InteractionPriority?> ContributeAsync(InboundPriorityContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.LastInboundUtc is null)
        {
            return Task.FromResult<InteractionPriority?>(null);
        }

        return Task.FromResult<InteractionPriority?>(_timeProvider.GetUtcNow().UtcDateTime - context.LastInboundUtc.Value <= RepeatWindow
            ? InteractionPriority.High
            : null);
    }
}
