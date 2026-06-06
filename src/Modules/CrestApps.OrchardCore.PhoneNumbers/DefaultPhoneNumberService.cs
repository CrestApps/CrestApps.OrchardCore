using PhoneNumbers;
using PhoneNumberToTimeZonesMapper = PhoneNumbers.PhoneNumberToTimeZonesMapper;

namespace CrestApps.OrchardCore.PhoneNumbers;

/// <summary>
/// Default implementation of <see cref="IPhoneNumberService"/> backed by libphonenumber.
/// Provides E.164 formatting, validation, timezone, and region detection.
/// </summary>
public sealed class DefaultPhoneNumberService : IPhoneNumberService
{
    private static readonly PhoneNumberUtil _phoneUtil = PhoneNumberUtil.GetInstance();

    /// <inheritdoc/>
    public bool TryFormatToE164(string phoneNumber, string regionCode, out string e164Number)
    {
        e164Number = null;

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return false;
        }

        try
        {
            var parsed = _phoneUtil.Parse(phoneNumber, regionCode?.ToUpperInvariant());

            if (!_phoneUtil.IsValidNumber(parsed))
            {
                return false;
            }

            e164Number = _phoneUtil.Format(parsed, PhoneNumberFormat.E164);

            return true;
        }
        catch (NumberParseException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public bool IsValidNumber(string phoneNumber, string regionCode)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return false;
        }

        try
        {
            var parsed = _phoneUtil.Parse(phoneNumber, regionCode?.ToUpperInvariant());

            return _phoneUtil.IsValidNumber(parsed);
        }
        catch (NumberParseException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetTimeZones(string e164Number)
    {
        if (string.IsNullOrWhiteSpace(e164Number))
        {
            return [];
        }

        try
        {
            var parsed = _phoneUtil.Parse(e164Number, null);
            var timeZones = PhoneNumberToTimeZonesMapper.GetInstance()
                .GetTimeZonesForNumber(parsed);

            return timeZones?.Count > 0
                ? timeZones.ToList()
                : [];
        }
        catch (NumberParseException)
        {
            return [];
        }
    }

    /// <inheritdoc/>
    public string GetRegionCode(string e164Number)
    {
        if (string.IsNullOrWhiteSpace(e164Number))
        {
            return null;
        }

        try
        {
            var parsed = _phoneUtil.Parse(e164Number, null);

            return _phoneUtil.GetRegionCodeForNumber(parsed);
        }
        catch (NumberParseException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public int GetCountryCode(string regionCode)
    {
        if (string.IsNullOrWhiteSpace(regionCode))
        {
            return 0;
        }

        return _phoneUtil.GetCountryCodeForRegion(regionCode.ToUpperInvariant());
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> GetSupportedRegions()
        => _phoneUtil.GetSupportedRegions().ToList();
}
