using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Builds the <see cref="AIConversationContext"/> of an automated omnichannel conversation from its activity: the
/// contact, the customer's number, our endpoint, the campaign and the subject.
/// </summary>
public sealed class OmnichannelConversationContextFactory : IOmnichannelConversationContextFactory
{
    private readonly IContentManager _contentManager;
    private readonly ICatalog<OmnichannelCampaign> _campaignCatalog;
    private readonly IOmnichannelChannelEndpointStore _endpointStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelConversationContextFactory"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager, which loads the contact.</param>
    /// <param name="campaignCatalog">The campaign catalog.</param>
    /// <param name="endpointStore">The channel endpoint store, which says which of our numbers is in use.</param>
    public OmnichannelConversationContextFactory(
        IContentManager contentManager,
        ICatalog<OmnichannelCampaign> campaignCatalog,
        IOmnichannelChannelEndpointStore endpointStore)
    {
        _contentManager = contentManager;
        _campaignCatalog = campaignCatalog;
        _endpointStore = endpointStore;
    }

    /// <inheritdoc/>
    public async Task<AIConversationContext> CreateAsync(
        OmnichannelActivity activity,
        AIChatSession session,
        AIProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var context = new AIConversationContext
        {
            Channel = ToConversationChannel(activity.Channel),
            Direction = string.Equals(activity.Source, ActivitySources.Inbound, StringComparison.Ordinal)
                ? AIConversationDirections.Inbound
                : AIConversationDirections.Outbound,
            Session = session,
            Profile = profile,
            ActivityId = activity.ItemId,
            CustomerAddress = activity.PreferredDestination,
        };

        context.Values["Activity"] = activity;

        // The variables the load or a starting workflow put on the activity before the session existed.
        foreach (var (name, value) in activity.GetConversationVariables())
        {
            context.InitialVariables[name] = value;
        }

        if (!string.IsNullOrWhiteSpace(activity.ContactContentItemId))
        {
            context.Contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

            // Kept on the session, which the caller saves with the turn, so work done after the conversation, such
            // as rewriting the contact's memory, knows who it was with.
            if (session is not null && !session.Has<AIConversationContactMetadata>())
            {
                session.Put(new AIConversationContactMetadata
                {
                    ContactId = activity.ContactContentItemId,
                    ActivityId = activity.ItemId,
                });
            }
        }

        if (activity.Subject is not null)
        {
            context.Values["Subject"] = activity.Subject;
        }

        if (!string.IsNullOrWhiteSpace(activity.CampaignId))
        {
            var campaign = await _campaignCatalog.FindByIdAsync(activity.CampaignId, cancellationToken);

            if (campaign is not null)
            {
                context.Values["Campaign"] = campaign;
            }
        }

        if (!string.IsNullOrWhiteSpace(activity.ChannelEndpointId))
        {
            var endpoint = await _endpointStore.FindByIdAsync(activity.ChannelEndpointId, cancellationToken);

            if (endpoint is not null)
            {
                context.BusinessAddress = endpoint.Value;
                context.Values["Endpoint"] = endpoint;
            }
        }

        return context;
    }

    /// <summary>
    /// Maps an omnichannel channel name to a conversation channel.
    /// </summary>
    /// <param name="channel">The activity's channel.</param>
    /// <returns>The conversation channel.</returns>
    internal static string ToConversationChannel(string channel)
        => channel switch
        {
            OmnichannelConstants.Channels.Phone => AIConversationChannels.Phone,
            OmnichannelConstants.Channels.Sms => AIConversationChannels.Sms,
            OmnichannelConstants.Channels.Email => AIConversationChannels.Email,
            _ => channel,
        };
}
