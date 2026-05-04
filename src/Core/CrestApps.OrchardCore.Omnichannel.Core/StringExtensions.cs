namespace CrestApps.OrchardCore.Omnichannel.Core;

/// <summary>
/// Provides extension methods for string.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Retrieves the cleaned phone number.
    /// </summary>
    /// <param name="phoneNumber">The phone number.</param>
    public static string GetCleanedPhoneNumber(this string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return phoneNumber;
        }

        return new string(phoneNumber.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }
}
