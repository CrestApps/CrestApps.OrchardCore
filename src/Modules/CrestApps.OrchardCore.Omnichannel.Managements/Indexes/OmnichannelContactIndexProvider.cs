using CrestApps.Core;
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
            .Map(CreateIndex);
    }

    internal OmnichannelContactIndex CreateIndex(ContentItem contact)
    {
        if ((!contact.Published && !contact.Latest) ||
            !contact.TryGet<OmnichannelContactPart>(out var contactPart))
        {
            return null;
        }

        var index = new OmnichannelContactIndex
        {
            ContentItemId = contact.ContentItemId,
            Published = contact.Published,
            Latest = contact.Latest,
            TimeZoneId = TruncateTrimmed(contactPart.TimeZoneId, 64),
        };

        if (!contact.TryGet<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, out var bagPart) ||
            bagPart.ContentItems is null ||
            bagPart.ContentItems.Count == 0)
        {
            return index;
        }

        foreach (var contactMethod in bagPart.ContentItems)
        {
            if (string.Equals(contactMethod.ContentType, OmnichannelConstants.ContentTypes.EmailAddress, StringComparison.Ordinal) &&
                string.IsNullOrEmpty(index.PrimaryEmailAddress) &&
                contactMethod.TryGet<EmailInfoPart>(out var emailPart) &&
                !string.IsNullOrEmpty(emailPart.Email?.Text))
            {
                index.PrimaryEmailAddress = Truncate(emailPart.Email.Text, 255);
            }

            if (!string.Equals(contactMethod.ContentType, OmnichannelConstants.ContentTypes.PhoneNumber, StringComparison.Ordinal) ||
                !contactMethod.TryGet<PhoneNumberInfoPart>(out var phonePart) ||
                string.IsNullOrWhiteSpace(phonePart.Number?.PhoneNumber))
            {
                continue;
            }

            if (string.Equals(phonePart.Type?.Text, "Cell", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(index.PrimaryCellPhoneNumber))
            {
                SetPrimaryCellPhoneNumber(index, phonePart.Number);
            }
            else if (string.Equals(phonePart.Type?.Text, "Home", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(index.PrimaryHomePhoneNumber))
            {
                SetPrimaryHomePhoneNumber(index, phonePart.Number);
            }
        }

        return index;
    }

    private void SetPrimaryCellPhoneNumber(OmnichannelContactIndex index, PhoneField field)
    {
        index.NormalizedPrimaryCellPhoneNumber = NormalizeToE164(field);
        index.PrimaryCellPhoneNumber = NormalizeNationalNumber(
            field,
            index.NormalizedPrimaryCellPhoneNumber);
    }

    private void SetPrimaryHomePhoneNumber(OmnichannelContactIndex index, PhoneField field)
    {
        index.NormalizedPrimaryHomePhoneNumber = NormalizeToE164(field);
        index.PrimaryHomePhoneNumber = NormalizeNationalNumber(
            field,
            index.NormalizedPrimaryHomePhoneNumber);
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
        var countryCodeText = countryCode > 0
            ? countryCode.ToString()
            : null;

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

    private static string TruncateTrimmed(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return Truncate(trimmed, maxLength);
    }
}
