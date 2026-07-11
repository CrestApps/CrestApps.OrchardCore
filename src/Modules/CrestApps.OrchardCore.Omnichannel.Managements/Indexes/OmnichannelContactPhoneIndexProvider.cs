using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Indexes;

internal sealed class OmnichannelContactPhoneIndexProvider : IndexProvider<ContentItem>
{
    private readonly IPhoneNumberService _phoneNumberService;

    public OmnichannelContactPhoneIndexProvider(IPhoneNumberService phoneNumberService)
    {
        _phoneNumberService = phoneNumberService;
    }

    public override void Describe(DescribeContext<ContentItem> context)
    {
        context
            .For<OmnichannelContactPhoneIndex>()
            .Map(CreateIndex);
    }

    internal OmnichannelContactPhoneIndex CreateIndex(ContentItem contact)
    {
        if ((!contact.Published && !contact.Latest) ||
            !contact.TryGet<OmnichannelContactPart>(out _) ||
            !contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bagPart) ||
            bagPart.ContentItems is null ||
            bagPart.ContentItems.Count == 0)
        {
            return null;
        }

        var index = new OmnichannelContactPhoneIndex
        {
            ContentItemId = contact.ContentItemId,
            Published = contact.Published,
            Latest = contact.Latest,
        };

        foreach (var contactMethod in bagPart.ContentItems)
        {
            if (!string.Equals(contactMethod.ContentType, OmnichannelConstants.ContentTypes.PhoneNumber, StringComparison.Ordinal) ||
                !contactMethod.TryGet<PhoneNumberInfoPart>(out var phonePart) ||
                string.IsNullOrWhiteSpace(phonePart.Number?.PhoneNumber))
            {
                continue;
            }

            var e164Number = NormalizeToE164(phonePart.Number);
            var nationalNumber = NormalizeNationalNumber(phonePart.Number, e164Number);

            if (string.Equals(phonePart.Type?.Text, "Cell", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(index.E164PrimaryCellPhoneNumber))
            {
                index.E164PrimaryCellPhoneNumber = e164Number;
                index.NationalPrimaryCellPhoneNumber = nationalNumber;
            }
            else if (string.Equals(phonePart.Type?.Text, "Home", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(index.E164PrimaryHomePhoneNumber))
            {
                index.E164PrimaryHomePhoneNumber = e164Number;
                index.NationalPrimaryHomePhoneNumber = nationalNumber;
            }
        }

        return index;
    }

    private string NormalizeToE164(PhoneField field)
    {
        var phoneNumber = field.PhoneNumber?.Trim();

        if (_phoneNumberService.TryFormatToE164(phoneNumber, field.CountryCode, out var e164Number))
        {
            return Truncate(e164Number, 50);
        }

        if (PhoneNumberSearchTerm.TryParse(phoneNumber, out var searchTerm) && searchTerm.IsE164)
        {
            return Truncate(searchTerm.Value, 50);
        }

        return null;
    }

    private string NormalizeNationalNumber(PhoneField field, string e164Number)
    {
        var nationalNumber = PhoneNumberSearchTerm.NormalizeDigits(field.NationalNumber);

        if (!string.IsNullOrEmpty(nationalNumber))
        {
            return Truncate(nationalNumber, 50);
        }

        var digits = PhoneNumberSearchTerm.NormalizeDigits(e164Number ?? field.PhoneNumber);

        if (string.IsNullOrEmpty(digits) || string.IsNullOrEmpty(e164Number))
        {
            return Truncate(digits, 50);
        }

        var regionCode = string.IsNullOrWhiteSpace(field.CountryCode)
            ? _phoneNumberService.GetRegionCode(e164Number)
            : field.CountryCode;

        if (string.IsNullOrWhiteSpace(regionCode))
        {
            return Truncate(digits, 50);
        }

        var countryCode = _phoneNumberService.GetCountryCode(regionCode);
        var countryCodeText = countryCode > 0 ? countryCode.ToString() : null;

        if (!string.IsNullOrEmpty(countryCodeText) &&
            digits.Length > countryCodeText.Length &&
            digits.StartsWith(countryCodeText, StringComparison.Ordinal))
        {
            digits = digits.Substring(countryCodeText.Length);
        }

        return Truncate(digits, 50);
    }

    private static string Truncate(string value, int maxLength)
    {
        return string.IsNullOrEmpty(value)
            ? null
            : value.Substring(0, Math.Min(maxLength, value.Length));
    }
}
