using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Describes who or what handles an activity, and who dispositioned it once completed, in the words the activity
/// screens show.
/// </summary>
/// <remarks>
/// Registered per request and shared by every row of a list: the AI profiles, campaigns and dialer profiles are each
/// read once, the first time a row needs one, rather than once per row.
/// </remarks>
public sealed class ActivityHandlerDescriber
{
    private readonly IAIProfileManager _aiProfileManager;
    private readonly ICatalog<OmnichannelCampaign> _campaigns;
    private readonly IActivityDialerContributor _dialerContributor;

    private Dictionary<string, string> _aiProfileNames;
    private Dictionary<string, string> _campaignNames;
    private Dictionary<string, string> _dialerProfileNames;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityHandlerDescriber"/> class.
    /// </summary>
    /// <param name="aiProfileManagers">The AI profile manager, when the AI features are enabled.</param>
    /// <param name="campaigns">The campaigns.</param>
    /// <param name="dialerContributors">The dialer profiles, when a dialer feature is enabled.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ActivityHandlerDescriber(
        IEnumerable<IAIProfileManager> aiProfileManagers,
        ICatalog<OmnichannelCampaign> campaigns,
        IEnumerable<IActivityDialerContributor> dialerContributors,
        IStringLocalizer<ActivityHandlerDescriber> stringLocalizer)
    {
        _aiProfileManager = aiProfileManagers.FirstOrDefault();
        _campaigns = campaigns;
        _dialerContributor = dialerContributors.FirstOrDefault();
        S = stringLocalizer;
    }

    /// <summary>
    /// Describes who will handle an open activity: its interaction type and source, the AI profile for automated
    /// work, the campaign and dialer profile for dialed work, and the user it is assigned to.
    /// </summary>
    /// <param name="model">The view model to fill.</param>
    /// <param name="activity">The activity.</param>
    /// <param name="assignedToName">The assigned user's display name, when the caller has already resolved it.</param>
    public async Task DescribeHandlerAsync(ActivityHandlerViewModel model, OmnichannelActivity activity, string assignedToName)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(activity);

        model.Activity = activity;
        model.InteractionTypeName = GetInteractionTypeName(activity.InteractionType);
        model.SourceName = GetSourceName(activity.Source);
        model.CampaignName = await GetCampaignNameAsync(activity.CampaignId);
        model.AssignedToName = string.IsNullOrWhiteSpace(assignedToName)
            ? activity.AssignedToUsername
            : assignedToName;

        if (activity.InteractionType == ActivityInteractionType.Automated)
        {
            model.AIProfileName = await GetAIProfileNameAsync(activity.AIProfileId);
        }

        if (ActivitySources.IsDialer(activity.Source))
        {
            model.DialerProfileName = await GetDialerProfileNameAsync(activity.DialerProfileId);
        }
    }

    /// <summary>
    /// Describes who dispositioned a completed activity.
    /// </summary>
    /// <param name="model">The view model to fill.</param>
    /// <param name="activity">The completed activity.</param>
    /// <param name="completedByName">The completing user's display name, when the caller has already resolved it.</param>
    public async Task DescribeDispositionAsync(ActivityHandlerViewModel model, OmnichannelActivity activity, string completedByName)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(activity);

        model.Activity = activity;
        model.InteractionTypeName = GetInteractionTypeName(activity.InteractionType);
        model.SourceName = GetSourceName(activity.Source);
        model.DispositionedBy = ActivityDispositionActors.Resolve(activity);
        model.DispositionedByName = await GetDispositionedByNameAsync(activity, completedByName);
    }

    /// <summary>
    /// Gets the description of who dispositioned a completed activity: the user's name, the AI agent and its
    /// profile, the dialer, or the platform.
    /// </summary>
    /// <param name="activity">The completed activity.</param>
    /// <param name="completedByName">The completing user's display name, when the caller has already resolved it.</param>
    /// <returns>The localized description.</returns>
    public async Task<string> GetDispositionedByNameAsync(OmnichannelActivity activity, string completedByName)
    {
        ArgumentNullException.ThrowIfNull(activity);

        switch (ActivityDispositionActors.Resolve(activity))
        {
            case ActivityDispositionActor.User:
                if (!string.IsNullOrWhiteSpace(completedByName))
                {
                    return completedByName;
                }

                return string.IsNullOrWhiteSpace(activity.CompletedByUsername)
                    ? S["A user"]
                    : activity.CompletedByUsername;

            case ActivityDispositionActor.AIAgent:
                var profileName = await GetAIProfileNameAsync(ActivityDispositionActors.ResolveAIProfileId(activity));
                var isVoice = string.Equals(activity.Channel, OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase);

                if (string.IsNullOrEmpty(profileName))
                {
                    return isVoice ? S["AI voice agent"] : S["AI agent"];
                }

                return isVoice
                    ? S["AI voice agent (profile: {0})", profileName]
                    : S["AI agent (profile: {0})", profileName];

            case ActivityDispositionActor.Dialer:
                return S["Dialer (automatic)"];

            default:
                return S["Automatically (system)"];
        }
    }

    /// <summary>
    /// Gets the localized name of an activity source.
    /// </summary>
    /// <param name="source">The activity source, one of <see cref="ActivitySources"/>.</param>
    /// <returns>The localized name, the source itself when it is not a known one, or <see langword="null"/> when empty.</returns>
    public string GetSourceName(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        return source switch
        {
            ActivitySources.Manual => S["Manual"],
            ActivitySources.Automatic => S["Automatic"],
            ActivitySources.Dialer => S["Dialer"],
            ActivitySources.PreviewDial => S["Preview dialer"],
            ActivitySources.PowerDial => S["Power dialer"],
            ActivitySources.ProgressiveDial => S["Progressive dialer"],
            ActivitySources.PredictiveDial => S["Predictive dialer"],
            ActivitySources.Callback => S["Callback"],
            ActivitySources.Inbound => S["Inbound"],
            ActivitySources.Workflow => S["Workflow"],
            ActivitySources.Api => S["API"],
            _ => source,
        };
    }

    private string GetInteractionTypeName(ActivityInteractionType interactionType)
        => interactionType == ActivityInteractionType.Automated
            ? S["Automated (AI)"]
            : S["Manual"];

    private async Task<string> GetAIProfileNameAsync(string profileId)
    {
        if (string.IsNullOrEmpty(profileId) || _aiProfileManager is null)
        {
            return null;
        }

        if (_aiProfileNames is null)
        {
            _aiProfileNames = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var profile in await _aiProfileManager.GetAllAsync())
            {
                if (!string.IsNullOrEmpty(profile?.ItemId))
                {
                    _aiProfileNames.TryAdd(profile.ItemId, string.IsNullOrWhiteSpace(profile.DisplayText) ? profile.Name : profile.DisplayText);
                }
            }
        }

        return _aiProfileNames.GetValueOrDefault(profileId);
    }

    private async Task<string> GetCampaignNameAsync(string campaignId)
    {
        if (string.IsNullOrEmpty(campaignId))
        {
            return null;
        }

        if (_campaignNames is null)
        {
            _campaignNames = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var campaign in await _campaigns.GetAllAsync())
            {
                if (!string.IsNullOrEmpty(campaign?.ItemId))
                {
                    _campaignNames.TryAdd(campaign.ItemId, campaign.DisplayText);
                }
            }
        }

        return _campaignNames.GetValueOrDefault(campaignId);
    }

    private async Task<string> GetDialerProfileNameAsync(string dialerProfileId)
    {
        if (string.IsNullOrEmpty(dialerProfileId) || _dialerContributor is null)
        {
            return null;
        }

        if (_dialerProfileNames is null)
        {
            _dialerProfileNames = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var profile in await _dialerContributor.GetProfilesAsync())
            {
                if (!string.IsNullOrEmpty(profile?.ProfileId))
                {
                    _dialerProfileNames.TryAdd(profile.ProfileId, profile.DisplayName);
                }
            }
        }

        return _dialerProfileNames.GetValueOrDefault(dialerProfileId);
    }
}
