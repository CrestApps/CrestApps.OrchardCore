using CrestApps.OrchardCore.YesSql.Core.Upgrades;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Hands the requester e-mails an earlier version kept in the service desk's own outbox to the templated e-mail outbox,
/// a batch at a time (<see cref="TicketEmailOutbox.HandOverAsync"/>): the ones still waiting are sent through it, and all
/// of them are removed. The migration that moved the e-mails to the templated e-mail outbox asks for it once.
/// </summary>
public sealed class TicketEmailHandOverStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Tickets.EmailOutboxHandOver";

    private readonly TicketEmailOutbox _outbox;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketEmailHandOverStep"/> class.
    /// </summary>
    /// <param name="outbox">The requester messages.</param>
    public TicketEmailHandOverStep(TicketEmailOutbox outbox)
    {
        _outbox = outbox;
    }

    /// <inheritdoc/>
    public string Name => StepName;

    /// <inheritdoc/>
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
        => await _outbox.HandOverAsync(cancellationToken) < TicketEmailOutbox.SendsPerRun;
}
