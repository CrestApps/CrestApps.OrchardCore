using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IPredictiveSystemDialAuthorizer"/>: a dial without an agent is
/// authorized only for an enabled Predictive profile on the over-dial pacing model, for a campaign call whose interaction
/// was recorded as over-dialed for the same activity and still names no agent.
/// </summary>
public sealed class PredictiveSystemDialAuthorizer : IPredictiveSystemDialAuthorizer
{
    private readonly IDialerProfileReader _profileReader;
    private readonly IInteractionManager _interactionManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PredictiveSystemDialAuthorizer"/> class.
    /// </summary>
    /// <param name="profileReader">The dialer profiles.</param>
    /// <param name="interactionManager">The interactions.</param>
    /// <param name="logger">The logger.</param>
    public PredictiveSystemDialAuthorizer(
        IDialerProfileReader profileReader,
        IInteractionManager interactionManager,
        ILogger<PredictiveSystemDialAuthorizer> logger)
    {
        _profileReader = profileReader;
        _interactionManager = interactionManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IsAuthorizedAsync(ProviderCommand command, ContactCenterDialRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(request.AgentId) ||
            !string.IsNullOrWhiteSpace(request.AgentUserId) ||
            !string.IsNullOrWhiteSpace(command.ReservationId) ||
            string.IsNullOrWhiteSpace(command.DialerProfileId) ||
            QueueCallbackDialerProfile.IsCallbackProfile(command.DialerProfileId) ||
            string.IsNullOrWhiteSpace(request.ActivityId) ||
            string.IsNullOrWhiteSpace(request.QueueId) ||
            !ContactCenterConstants.IsCampaignQueue(request.QueueId))
        {
            return false;
        }

        var profile = await _profileReader.FindByIdAsync(command.DialerProfileId, cancellationToken);

        if (profile is null ||
            !profile.Enabled ||
            profile.Mode != DialerMode.Predictive ||
            profile.PredictivePacingModel != PredictivePacingModel.OverDial)
        {
            _logger.LogWarning(
                "Refused dial '{CommandId}' without an agent: dialer profile '{ProfileId}' is not an enabled over-dialing Predictive profile.",
                command.CommandId.SanitizeLogValue(),
                command.DialerProfileId.SanitizeLogValue());

            return false;
        }

        var interaction = await _interactionManager.FindByIdAsync(request.InteractionId, cancellationToken);

        return interaction is not null &&
            !interaction.IsSettled &&
            string.IsNullOrEmpty(interaction.AgentId) &&
            string.Equals(interaction.ActivityItemId, request.ActivityId, StringComparison.Ordinal) &&
            string.Equals(DialerCallMetadata.GetDialerProfileId(interaction), profile.ItemId, StringComparison.Ordinal) &&
            DialerCallMetadata.IsOverDialed(interaction);
    }
}
