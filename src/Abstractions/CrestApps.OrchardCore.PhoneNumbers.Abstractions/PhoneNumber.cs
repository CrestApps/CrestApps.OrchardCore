using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.PhoneNumbers;

/// <summary>
/// A phone number that is in E.164 form by construction. It exists so that a number cannot be compared,
/// stored, or checked against a do-not-call registry unless it has already been canonicalized: the type
/// system, rather than each caller's memory, is what guarantees that two numbers written differently by
/// two systems are the same value.
/// </summary>
/// <remarks>
/// The shape invariant — a leading <c>+</c>, a non-zero country digit, and up to fifteen digits in total —
/// is enforced here and always holds. Whether the number is *assignable* under its country's numbering plan
/// is a separate question that only <see cref="IPhoneNumberService"/> can answer, so a number entered by a
/// person or read from a file must be created through <see cref="PhoneNumberServiceExtensions.TryParse"/>.
/// <see cref="FromE164(string)"/> exists for values that were canonicalized once already and persisted, so
/// reading them back does not require the number-plan metadata to still recognize them.
/// </remarks>
[JsonConverter(typeof(PhoneNumberJsonConverter))]
public readonly record struct PhoneNumber
{
    /// <summary>
    /// The maximum number of digits an E.164 number may carry, excluding the leading <c>+</c>.
    /// </summary>
    public const int MaxDigits = 15;

    private readonly string _value;

    private PhoneNumber(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Gets the number in E.164 form, or <see langword="null"/> when this is the default value.
    /// </summary>
    public string Value => _value;

    /// <summary>
    /// Gets a value indicating whether this instance carries a number. A default <see cref="PhoneNumber"/>
    /// carries none, which is how "no number" is represented without reintroducing a null string.
    /// </summary>
    public bool HasValue => _value is not null;

    /// <summary>
    /// Gets the digits of the number without the leading <c>+</c>, or an empty string when this is the
    /// default value. Country prefix rules are expressed against these digits, so exposing them here keeps
    /// callers from slicing the value themselves and disagreeing about whether the plus sign is included.
    /// </summary>
    public string Digits => _value is null
        ? string.Empty
        : _value.Substring(1);

    /// <summary>
    /// Creates a phone number from a value that is already in E.164 form.
    /// </summary>
    /// <param name="e164Number">The number in E.164 form, including the leading <c>+</c>.</param>
    /// <returns>The phone number.</returns>
    /// <exception cref="ArgumentException">The value is not in E.164 form.</exception>
    public static PhoneNumber FromE164(string e164Number)
    {
        if (!TryFromE164(e164Number, out var phoneNumber))
        {
            throw new ArgumentException($"'{e164Number}' is not a phone number in E.164 form.", nameof(e164Number));
        }

        return phoneNumber;
    }

    /// <summary>
    /// Attempts to create a phone number from a value that is already in E.164 form.
    /// </summary>
    /// <param name="e164Number">The number in E.164 form, including the leading <c>+</c>.</param>
    /// <param name="phoneNumber">The phone number when the value is in E.164 form; otherwise the default value.</param>
    /// <returns><see langword="true"/> when the value is in E.164 form; otherwise <see langword="false"/>.</returns>
    public static bool TryFromE164(string e164Number, out PhoneNumber phoneNumber)
    {
        phoneNumber = default;

        if (!IsE164(e164Number))
        {
            return false;
        }

        phoneNumber = new PhoneNumber(e164Number);

        return true;
    }

    /// <summary>
    /// Determines whether a value is in E.164 form.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns><see langword="true"/> when the value is in E.164 form; otherwise <see langword="false"/>.</returns>
    public static bool IsE164([NotNullWhen(true)] string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 2 || value.Length > MaxDigits + 1)
        {
            return false;
        }

        if (value[0] != '+' || value[1] == '0')
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns the number in E.164 form, or an empty string when this is the default value.
    /// </summary>
    /// <returns>The number in E.164 form.</returns>
    public override string ToString() => _value ?? string.Empty;
}
