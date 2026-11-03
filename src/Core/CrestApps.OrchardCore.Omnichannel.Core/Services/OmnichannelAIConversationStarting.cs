using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Runs the starting handlers for an automated activity's conversation and keeps their decisions on the activity,
/// which the conversation's session is created from.
/// </summary>
public sealed class OmnichannelAIConversationStarting : IOmnichannelAIConversationStarting
{
    private readonly IOmnichannelConversationContextFactory _contextFactory;
    private readonly IAIConversationStartingService _startingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelAIConversationStarting"/> class.
    /// </summary>
    /// <param name="contextFactory">Describes the activity's conversation.</param>
    /// <param name="startingService">Runs the starting handlers.</param>
    public OmnichannelAIConversationStarting(
        IOmnichannelConversationContextFactory contextFactory,
        IAIConversationStartingService startingService)
    {
        _contextFactory = contextFactory;
        _startingService = startingService;
    }

    /// <inheritdoc/>
    public async Task<AIConversationStartingResult> StartingAsync(OmnichannelActivity activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var conversation = await _contextFactory.CreateAsync(activity, session: null, profile: null, cancellationToken);
        var result = await _startingService.StartingAsync(conversation, cancellationToken);

        if (!string.IsNullOrWhiteSpace(result.ProfileId))
        {
            activity.AIProfileId = result.ProfileId;
        }

        if (result.Variables.Count > 0)
        {
            activity.SetConversationVariables(result.Variables);
        }

        return result;
    }
}
