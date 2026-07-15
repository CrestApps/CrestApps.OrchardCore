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

        await ExecuteRequiredAsync(
            participants,
            "quiescing",
            participant => participant.QuiesceAsync(cancellationToken),
            cancellationToken);

        await ExecuteRequiredAsync(
            participants,
            "draining",
            participant => participant.DrainAsync(cancellationToken),
            cancellationToken);
    }

    public async Task ReconcileAsync(string featureId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(featureId);

        foreach (var participant in _participants.Where(participant =>
            string.Equals(participant.FeatureId, featureId, StringComparison.Ordinal)))
        {
            await ExecuteBestEffortAsync(
                participant,
                "reconciling",
                () => participant.ReconcileAsync(cancellationToken),
                cancellationToken);
        }
    }

    private async Task ExecuteRequiredAsync(
        IReadOnlyCollection<IContactCenterFeatureLifecycleParticipant> participants,
        string operation,
        Func<IContactCenterFeatureLifecycleParticipant, Task> action,
        CancellationToken cancellationToken)
    {
        List<Exception> failures = [];

        foreach (var participant in participants)
        {
            try
            {
                await action(participant);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add(ex);
                LogFailure(participant, operation, ex);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                $"Unable to finish {operation} Contact Center feature work.",
                failures);
        }
    }

    private async Task ExecuteBestEffortAsync(
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
            LogFailure(participant, operation, ex);
        }
    }

    private void LogFailure(
        IContactCenterFeatureLifecycleParticipant participant,
        string operation,
        Exception exception)
    {
        _logger.LogError(
            OperationalLogRedactor.RedactException(exception),
            "An error occurred while {Operation} Contact Center feature '{FeatureId}' participant '{ParticipantType}'.",
            operation,
            participant.FeatureId,
            participant.GetType().Name);
    }
}
