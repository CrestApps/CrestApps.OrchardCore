using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Applies a call-session change through the managers a test already set up, in the same scope, so a test sees the
/// change on the very copies it arranged.
/// </summary>
internal sealed class InPlaceCallSessionUpdater : ICallSessionUpdater
{
    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;

    public InPlaceCallSessionUpdater(IInteractionManager interactionManager, ICallSessionManager callSessionManager)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
    }

    public async Task<bool> UpdateAsync(string interactionId, Func<CallSession, bool> mutate, CancellationToken cancellationToken = default)
    {
        var session = await _callSessionManager.FindByInteractionIdAsync(interactionId, cancellationToken);

        if (session is null || !mutate(session))
        {
            return false;
        }

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);

        return true;
    }

    public async Task<bool> UpdateWithInteractionAsync(string interactionId, Func<CallSession, Interaction, bool> mutate, CancellationToken cancellationToken = default)
    {
        var session = await _callSessionManager.FindByInteractionIdAsync(interactionId, cancellationToken);
        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (session is null || interaction is null || !mutate(session, interaction))
        {
            return false;
        }

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        return true;
    }
}
