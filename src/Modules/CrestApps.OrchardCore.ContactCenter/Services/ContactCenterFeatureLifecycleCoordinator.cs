using CrestApps.Core.ContactCenter;
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

    public async Task QuiesceAsync(IReadOnlyCollection<string> capabilities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        if (capabilities.Count == 0)
        {
            return;
        }

        var participants = _participants
            .Where(participant => capabilities.Contains(participant.Capability, StringComparer.Ordinal))
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

    private void LogFailure(
        IContactCenterFeatureLifecycleParticipant participant,
        string operation,
        Exception exception)
    {
        _logger.LogError(
            exception,
            "An error occurred while {Operation} Contact Center feature '{Capability}' participant '{ParticipantType}'.",
            operation,
            participant.Capability,
            participant.GetType().Name);
    }
}
