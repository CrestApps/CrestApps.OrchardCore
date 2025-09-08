using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Indexes;

internal sealed class OmnichannelContactIndexProvider : IndexProvider<ContentItem>
{
    public OmnichannelContactIndexProvider()
    {
        CollectionName = OmnichannelConstants.CollectionName;
    }

    public override void Describe(DescribeContext<ContentItem> context)
    {
        context
            .For<OmnichannelContactIndex>()
            .Map(message =>
            {
                var part = message.As<OmnichannelContactInfoPart>();

                var index = new OmnichannelContactIndex
                {
                    FirstName = part?.FirstName?.Text,
                    LastName = part?.LastName?.Text
                };

                if (message.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bagPart) &&
                    bagPart.ContentItems is not null &&
                    bagPart.ContentItems.Count > 0)
                {
                    foreach (var contentMethod in bagPart.ContentItems)
                    {
                        if (string.IsNullOrEmpty(index.PrimaryEmailAddress) && contentMethod.ContentType == OmnichannelConstants.ContentTypes.EmailAddress)
                        {
                            var emailPart = contentMethod.As<EmailInfoPart>();

                            if (!string.IsNullOrEmpty(emailPart.Email?.Text))
                            {
                                index.PrimaryEmailAddress = emailPart.Email.Text.Substring(0, Math.Min(255, emailPart.Email.Text.Length));
                            }
                        }

                        if (string.IsNullOrEmpty(index.PrimaryCellPhoneNumber) && contentMethod.ContentType == OmnichannelConstants.ContentTypes.PhoneNumber)
                        {
                            var phonePart = contentMethod.As<PhoneNumberInfoPart>();

                            if (phonePart is not null && phonePart.Type?.Text == "Cell" && !string.IsNullOrEmpty(phonePart.Number?.Text))
                            {
                                index.PrimaryCellPhoneNumber = phonePart.Number.Text.Substring(0, Math.Min(50, phonePart.Number.Text.Length));
                            }
                        }

                        if (string.IsNullOrEmpty(index.PrimaryHomePhoneNumber) && contentMethod.ContentType == OmnichannelConstants.ContentTypes.PhoneNumber)
                        {
                            var phonePart = contentMethod.As<PhoneNumberInfoPart>();

                            if (phonePart is not null && phonePart.Type?.Text == "Home" && !string.IsNullOrEmpty(phonePart.Number?.Text))
                            {
                                index.PrimaryHomePhoneNumber = phonePart.Number.Text.Substring(0, Math.Min(50, phonePart.Number.Text.Length));
                            }
                        }
                    }
                }

                return index;
            });
    }
}
