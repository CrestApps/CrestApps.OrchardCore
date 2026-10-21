using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal static class OmnichannelHelper
{
    /// <summary>
    /// Whether this contact has asked not to be reached on this channel.
    /// </summary>
    /// <remarks>
    /// Asked in two places, which is why it is one method. A batch reads it when it decides who to load, and the
    /// pass that places the call reads it again when the activity comes due -- and those can be hours apart. Two
    /// copies of this rule would be two chances for the second reading to disagree with the first.
    /// <para>
    /// Deliberately separate from whether a destination can be found for them: a contact with no number on file is
    /// unreachable, which is not the same as a contact who has told us to stop.
    /// </para>
    /// </remarks>
    /// <param name="contact">The contact, or <see langword="null"/> when there is none to ask.</param>
    /// <param name="channel">The channel they would be reached on.</param>
    public static bool HasOptedOut(ContentItem contact, string channel)
    {
        return OmnichannelContactPreferences.HasOptedOut(contact, channel);
    }

    /// <summary>
    /// Every phone number on file for this contact, in the order they were entered.
    /// </summary>
    /// <param name="contactMethods">The contact's contact methods.</param>
    public static IReadOnlyList<string> GetPhoneNumbers(ContactMethodSet contactMethods)
    {
        if (contactMethods is null || contactMethods.PhoneNumbers.Count == 0)
        {
            return [];
        }

        return contactMethods.PhoneNumbers
            .Select(phoneNumber => phoneNumber.PhoneNumber)
            .ToArray();
    }

    /// <summary>
    /// Retrieves the preferred destenation.
    /// </summary>
    /// <param name="contact">The contact.</param>
    /// <param name="contactMethods">The contact's contact methods.</param>
    /// <param name="channel">The channel.</param>
    public static string GetPreferredDestenation(ContentItem contact, ContactMethodSet contactMethods, string channel)
    {
        if (HasOptedOut(contact, channel))
        {
            return null;
        }

        return FindDestination(contactMethods, channel);
    }

    /// <summary>
    /// Where this contact would be reached on this channel, without asking whether they want to be.
    /// </summary>
    /// <remarks>
    /// Two questions that used to be one. Answering "may we?" by returning no address conflated a person who has
    /// asked to be left alone with a person who has no number on file, and left a caller that had already decided
    /// the consent question -- because an operator ticked the batch's own override -- unable to find the address
    /// it was entitled to use. Callers who have not made that decision should keep calling
    /// <see cref="GetPreferredDestenation"/>, which asks both.
    /// </remarks>
    /// <param name="contactMethods">The contact's contact methods.</param>
    /// <param name="channel">The channel.</param>
    public static string FindDestination(ContactMethodSet contactMethods, string channel)
        => FindDestination(contactMethods, channel, isExcluded: null);

    /// <summary>
    /// Where this contact would be reached on this channel, passing over the numbers that cannot be used.
    /// </summary>
    /// <remarks>
    /// A contact whose cell number is dead is still reachable at home. Passing over an excluded number keeps the same
    /// order of preference for the ones left, rather than giving up on the contact.
    /// </remarks>
    /// <param name="contactMethods">The contact's contact methods.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="isExcluded">Whether a phone number must be passed over, or <see langword="null"/> to use every number.</param>
    public static string FindDestination(ContactMethodSet contactMethods, string channel, Func<string, bool> isExcluded)
    {
        if (contactMethods is null)
        {
            return null;
        }

        if (channel == OmnichannelConstants.Channels.Email)
        {
            foreach (var emailAddress in contactMethods.EmailAddresses)
            {
                // The destination is the canonical form of the address, which is what a reply from the contact is
                // matched to its activity by. An entry that is not an address at all is passed over.
                if (OmnichannelEmailAddress.IsValid(emailAddress?.Email) &&
                    isExcluded?.Invoke(emailAddress.Email) != true)
                {
                    return OmnichannelEmailAddress.Normalize(emailAddress.Email);
                }
            }

            return null;
        }

        if (channel == OmnichannelConstants.Channels.Phone)
        {
            return contactMethods.GetPreferredPhoneNumber(isExcluded)?.PhoneNumber;
        }

        if (channel == OmnichannelConstants.Channels.Sms)
        {
            // Only a cell number can receive a text.
            return contactMethods.GetFirstPhoneNumber(ContactPhoneTypes.Cell, isExcluded)?.PhoneNumber;
        }

        return null;
    }
}
