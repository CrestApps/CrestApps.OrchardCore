using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal static class OmnichannelHelper
{
    public static string GetPreferredDestenation(ContentItem contact, string channel)
    {
        if (!contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bagPart) ||
            bagPart.ContentItems is null ||
            bagPart.ContentItems.Count == 0)
        {
            return null;
        }

        if (channel == OmnichannelConstants.Channels.Email)
        {
            foreach (var contentMethod in bagPart.ContentItems)
            {
                var emailPart = contentMethod.As<EmailInfoPart>();

                if (!string.IsNullOrEmpty(emailPart.Email?.Text))
                {
                    return emailPart.Email.Text;
                }
            }

            return null;
        }

        if (channel == OmnichannelConstants.Channels.Phone)
        {
            var phoneNumbers = new PriorityQueue<string, int>();
            foreach (var contentMethod in bagPart.ContentItems)
            {
                var phonePart = contentMethod.As<PhoneNumberInfoPart>();

                if (phonePart?.Type is null || string.IsNullOrEmpty(phonePart.Number?.Text))
                {
                    continue;
                }

                switch (phonePart.Type.Text)
                {
                    case "Cell":
                        phoneNumbers.Enqueue(phonePart.Number.Text, 1);
                        break;
                    case "Home":
                        phoneNumbers.Enqueue(phonePart.Number.Text, 2);
                        break;
                    case "Office":
                        phoneNumbers.Enqueue(phonePart.Number.Text, 3);
                        break;
                    case "Work":
                        phoneNumbers.Enqueue(phonePart.Number.Text, 4);
                        break;
                    case "Other":
                        phoneNumbers.Enqueue(phonePart.Number.Text, 5);
                        break;
                    default:
                        continue;
                }
            }

            return phoneNumbers.Dequeue();
        }
        else if (channel == OmnichannelConstants.Channels.Sms)
        {
            foreach (var contentMethod in bagPart.ContentItems)
            {
                var phonePart = contentMethod.As<PhoneNumberInfoPart>();

                if (phonePart?.Type is null || phonePart.Type.Text != "Cell" || string.IsNullOrEmpty(phonePart.Number?.Text))
                {
                    continue;
                }

                return phonePart.Number.Text;
            }
        }

        return null;
    }
}
