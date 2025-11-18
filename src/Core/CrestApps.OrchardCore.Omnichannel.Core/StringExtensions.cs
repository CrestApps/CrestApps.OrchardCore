namespace CrestApps.OrchardCore.Omnichannel.Core;
public static class StringExtensions
{
    public static string GetCleanedPhoneNumber(this string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return phoneNumber;
        }

        return new string(phoneNumber.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }
}
