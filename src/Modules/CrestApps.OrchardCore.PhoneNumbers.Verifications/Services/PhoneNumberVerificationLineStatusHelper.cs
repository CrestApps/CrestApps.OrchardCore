namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

internal static class PhoneNumberVerificationLineStatusHelper
{
    public static bool IsActiveOrUnknown(string lineStatus)
    {
        return string.IsNullOrWhiteSpace(lineStatus)
            || string.Equals(lineStatus.Trim(), "active", StringComparison.OrdinalIgnoreCase);
    }
}
