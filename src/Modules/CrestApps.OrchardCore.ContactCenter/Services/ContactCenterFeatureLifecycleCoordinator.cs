using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Services;

internal sealed class ContactCenterFeatureLifecycleCoordinator
{
    private readonly IEnumerable<IContactCenterFeatureLifecycleParticipant> _participants;
    private readonly ILogger _logger;

    public ContactCenterFeatureLifecycleCoordinator(
        IEnumerable<IContactCenterFeatureLifecycleParticipant> participants,
        ILogger<ContactCenterFeatureLifecycleCoordinator> logger)
    {
        _participants = participants;
        _logger = logger;
    }

    public async Task QuiesceAsync(string featureId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(featureId);

        var participants = _participants
            .Where(participant => string.Equals(participant.FeatureId, featureId, StringComparison.Ordinal))
            .ToList();

        foreach (var participant in participants)
        {
            await ExecuteAsync(
                participant,
                "quiescing",
                () => participant.QuiesceAsync(cancellationToken),
                cancellationToken);
        }

        foreach (var participant in participants)
        {
            await ExecuteAsync(
                participant,
                "draining",
                () => participant.DrainAsync(cancellationToken),
                cancellationToken);
        }
    }

    public async Task ReconcileAsync(string featureId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(featureId);

        foreach (var participant in _participants.Where(participant =>
            string.Equals(participant.FeatureId, featureId, StringComparison.Ordinal)))
        {
            await ExecuteAsync(
                participant,
                "reconciling",
                () => participant.ReconcileAsync(cancellationToken),
                cancellationToken);
        }
    }

    private async Task ExecuteAsync(
        IContactCenterFeatureLifecycleParticipant participant,
        string operation,
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                OperationalLogRedactor.RedactException(ex),
                "An error occurred while {Operation} Contact Center feature '{FeatureId}' participant '{ParticipantType}'.",
                operation,
                participant.FeatureId,
                participant.GetType().Name);
        }
    }
}
