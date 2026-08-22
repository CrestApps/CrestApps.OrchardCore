using System.Text;

namespace CrestApps.OrchardCore.Dialpad.Services;

internal static class DialpadAddressNormalizer
{
    public static string NormalizePhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var builder = new StringBuilder(trimmed.Length);

        foreach (var character in trimmed)
        {
            if (char.IsDigit(character) || character == '+' && builder.Length == 0)
            {
                builder.Append(character);
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }
}
