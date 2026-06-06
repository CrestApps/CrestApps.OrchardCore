using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Indexes;

internal sealed class OmnichannelContactIndexProvider : IndexProvider<ContentItem>
{
    private readonly IPhoneNumberService _phoneNumberService;

    public OmnichannelContactIndexProvider(IPhoneNumberService phoneNumberService)
    {
        _phoneNumberService = phoneNumberService;
    }

    public override void Describe(DescribeContext<ContentItem> context)
    {
        context
            .For<OmnichannelContactIndex>()
            .Map(contact =>
            {
                if (!contact.Published || !contact.Has<OmnichannelContactPart>())
                {
                    return null;
                }

                var index = new OmnichannelContactIndex
                {
                    ContentItemId = contact.ContentItemId,
                };

                if (contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bagPart) &&
                    bagPart.ContentItems is not null &&
                        bagPart.ContentItems.Count > 0)
                {
                    foreach (var contentMethod in bagPart.ContentItems)
                    {
                        if (contentMethod.ContentType == OmnichannelConstants.ContentTypes.EmailAddress &&
                            string.IsNullOrEmpty(index.PrimaryEmailAddress) &&
                            contentMethod.TryGet<EmailInfoPart>(out var emailPart) &&
                            !string.IsNullOrEmpty(emailPart.Email?.Text))
                        {
                            index.PrimaryEmailAddress = emailPart.Email.Text.Substring(0, Math.Min(255, emailPart.Email.Text.Length));
                        }

                        if (contentMethod.ContentType == OmnichannelConstants.ContentTypes.PhoneNumber &&
                            contentMethod.TryGet<PhoneNumberInfoPart>(out var phonePart) &&
                            !string.IsNullOrEmpty(phonePart.Number?.Text))
                        {
                            if (string.IsNullOrEmpty(index.PrimaryCellPhoneNumber) && phonePart.Type?.Text == "Cell")
                            {
                                index.PrimaryCellPhoneNumber = phonePart.Number.Text.Substring(0, Math.Min(50, phonePart.Number.Text.Length));
                                index.NormalizedPrimaryCellPhoneNumber = NormalizeToE164(phonePart.Number.Text);
                            }

                            if (string.IsNullOrEmpty(index.PrimaryHomePhoneNumber) && phonePart.Type?.Text == "Home")
                            {
                                index.PrimaryHomePhoneNumber = phonePart.Number.Text.Substring(0, Math.Min(50, phonePart.Number.Text.Length));
                                index.NormalizedPrimaryHomePhoneNumber = NormalizeToE164(phonePart.Number.Text);
                            }
                        }
                    }
                }

                return index;
            });
    }

    private string NormalizeToE164(string phoneNumber)
    {
        if (_phoneNumberService.TryFormatToE164(phoneNumber, null, out var e164))
        {
            return e164;
        }

        // If the number is already in E.164 format, return it as-is.
        return phoneNumber;
    }
}
