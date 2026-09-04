using System.Text;

namespace CrestApps.OrchardCore.Sms.Workspace.Support;

/// <summary>
/// Formats an E.164 phone number (for example <c>+17789012046</c>) into a friendlier display form
/// (<c>+1 (778) 901-2046</c>) for the SMS portal UI. North American Numbering Plan numbers get the familiar
/// grouped layout; every other number is returned with its digits lightly grouped so it stays readable without
/// pretending to know a country-specific convention. The original value is returned unchanged when it is not a
/// recognizable E.164 string, so nothing is ever lost.
/// </summary>
public static class PhoneDisplayFormatter
{
    public static string Format(string e164)
    {
        if (string.IsNullOrWhiteSpace(e164))
        {
            return e164;
        }

        var trimmed = e164.Trim();

        if (!trimmed.StartsWith('+'))
        {
            return trimmed;
        }

        var digits = new string([.. trimmed[1..].Where(char.IsDigit)]);

        if (digits.Length == 0)
        {
            return trimmed;
        }

        // North American Numbering Plan: +1 NXX NXX XXXX -> +1 (NXX) NXX-XXXX.
        if (digits.Length == 11 && digits[0] == '1')
        {
            return $"+1 ({digits[1..4]}) {digits[4..7]}-{digits[7..]}";
        }

        // Unknown plan: keep the leading '+' and group the national digits in threes for legibility.
        var builder = new StringBuilder("+");
        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0)
            {
                builder.Append(' ');
            }

            builder.Append(digits[i]);
        }

        return builder.ToString();
    }
}
