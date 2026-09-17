using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Whether a contact has asked not to be reached, asked the same way everywhere.
/// </summary>
/// <remarks>
/// This question is asked in at least four places on two channels — when a batch decides who to load, when the
/// periodic pass places what was loaded, when a cadence decides whether to follow up, and immediately before a
/// message reaches the carrier — and the answer has to be the same in all of them. It lived as a few lines inside
/// a destination lookup in one module, which is why the other three did not ask it at all.
/// </remarks>
public static class OmnichannelContactPreferences
{
    /// <summary>
    /// Whether this contact has asked not to be reached on this channel.
    /// </summary>
    /// <remarks>
    /// Deliberately separate from whether an address can be found for them: somebody with no number on file is
    /// unreachable, which is not the same as somebody who has told us to stop, and the two call for different
    /// handling.
    /// </remarks>
    /// <param name="contact">The contact, or <see langword="null"/> when there is none to ask.</param>
    /// <param name="channel">The channel they would be reached on.</param>
    public static bool HasOptedOut(ContentItem contact, string channel)
    {
        if (contact is null || !contact.TryGet<OmnichannelContactPart>(out var contactPart))
        {
            return false;
        }

        return (channel == OmnichannelConstants.Channels.Phone && contactPart.DoNotCall) ||
               (channel == OmnichannelConstants.Channels.Sms && contactPart.DoNotSms) ||
               (channel == OmnichannelConstants.Channels.Email && contactPart.DoNotEmail);
    }
}
