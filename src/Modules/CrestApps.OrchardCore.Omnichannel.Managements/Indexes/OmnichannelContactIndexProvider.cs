using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Indexes;

internal sealed class OmnichannelContactIndexProvider : IndexProvider<ContentItem>
{
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IContactMethodsAccessor _contactMethods;

    public OmnichannelContactIndexProvider(
        IPhoneNumberService phoneNumberService,
        IContactMethodsAccessor contactMethods)
    {
        _phoneNumberService = phoneNumberService;
        _contactMethods = contactMethods;
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
            ContentType = Truncate(contact.ContentType, 255),
            IsConverted = contact.TryGet<LeadPart>(out var leadPart) && leadPart.IsConverted,
            Published = contact.Published,
            Latest = contact.Latest,
            TimeZoneId = TruncateTrimmed(contactPart.TimeZoneId, 64),
        };

        var contactMethods = _contactMethods.Get(contact);

        var primaryEmailAddress = contactMethods.GetPrimaryEmailAddress();

        index.PrimaryEmailAddress = Truncate(primaryEmailAddress, 255);
        index.NormalizedPrimaryEmailAddress = Truncate(OmnichannelEmailAddress.Normalize(primaryEmailAddress), 255);

        if (contactMethods.GetFirstPhoneNumber(ContactPhoneTypes.Cell) is { } cellPhoneNumber)
        {
            SetPrimaryCellPhoneNumber(index, cellPhoneNumber);
        }

        if (contactMethods.GetFirstPhoneNumber(ContactPhoneTypes.Home) is { } homePhoneNumber)
        {
            SetPrimaryHomePhoneNumber(index, homePhoneNumber);
        }

        return index;
    }

    private void SetPrimaryCellPhoneNumber(OmnichannelContactIndex index, ContactPhoneNumber field)
    {
        index.NormalizedPrimaryCellPhoneNumber = NormalizeToE164(field);
        index.PrimaryCellPhoneNumber = NormalizeNationalNumber(
            field,
            index.NormalizedPrimaryCellPhoneNumber);
    }

    private void SetPrimaryHomePhoneNumber(OmnichannelContactIndex index, ContactPhoneNumber field)
    {
        index.NormalizedPrimaryHomePhoneNumber = NormalizeToE164(field);
        index.PrimaryHomePhoneNumber = NormalizeNationalNumber(
            field,
            index.NormalizedPrimaryHomePhoneNumber);
    }

    private string NormalizeToE164(ContactPhoneNumber field)
        => NormalizeToE164(_phoneNumberService, field);

    /// <summary>
    /// Formats a saved phone number in E.164, the form every lookup by number compares, or returns
    /// <see langword="null"/> when it cannot be read as a number.
    /// </summary>
    /// <param name="phoneNumberService">The phone number service.</param>
    /// <param name="field">The saved phone number.</param>
    internal static string NormalizeToE164(IPhoneNumberService phoneNumberService, ContactPhoneNumber field)
    {
        var phoneNumber = field.PhoneNumber?.Trim();

        if (phoneNumberService.TryParse(phoneNumber, field.CountryCode, out var canonicalNumber))
        {
            return Truncate(canonicalNumber.Value, 50);
        }

        if (PhoneNumberSearchTerm.TryParse(phoneNumber, out var searchTerm) && searchTerm.IsE164)
        {
            return Truncate(searchTerm.Value, 50);
        }

        return null;
    }

    private string NormalizeNationalNumber(ContactPhoneNumber field, string e164Number)
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
