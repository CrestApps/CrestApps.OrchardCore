using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Finds the number the dialer can call for an activity.
/// </summary>
internal static class ActivityDialDestinations
{
    /// <summary>
    /// The number the dialer can call for an activity: its own when it is already a phone activity with a number,
    /// otherwise its contact's preferred phone number, unless the contact has opted out of calls.
    /// </summary>
    /// <remarks>
    /// A record loaded by hand on a subject with no phone channel has no number, and one loaded for SMS or email has an
    /// address the dialer cannot call, so the contact's phone number is looked up. Such records used to be queued
    /// without it, offered once, and failed with "The activity has no destination to dial".
    /// </remarks>
    /// <param name="activity">The activity the dialer is to call.</param>
    /// <param name="contentManager">The content manager, which reads the activity's contact.</param>
    /// <param name="contactMethods">The contact methods accessor, which reads the contact's numbers.</param>
    /// <returns>The number, or <see langword="null"/> when there is none the dialer can call.</returns>
    public static async Task<string> FindAsync(OmnichannelActivity activity, IContentManager contentManager, IContactMethodsAccessor contactMethods)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (string.Equals(activity.Channel, OmnichannelConstants.Channels.Phone, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(activity.PreferredDestination))
        {
            return activity.PreferredDestination;
        }

        if (string.IsNullOrEmpty(activity.ContactContentItemId))
        {
            return null;
        }

        var contact = await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

        return contact is null
            ? null
            : OmnichannelHelper.GetPreferredDestenation(contact, contactMethods.Get(contact), OmnichannelConstants.Channels.Phone);
    }
}
