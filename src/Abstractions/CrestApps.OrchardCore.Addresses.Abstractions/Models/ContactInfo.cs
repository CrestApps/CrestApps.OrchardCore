using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.Addresses.Models;

/// <summary>
/// How to reach a person or an organization: a name, an e-mail address, a phone number and a postal address. New records
/// that keep a contact snapshot (who an order, a booking or a job was for, at the time) keep one of these instead of
/// declaring their own name, e-mail and phone fields.
/// </summary>
public sealed record ContactInfo
{
    /// <summary>
    /// Gets the name, such as a person's full name or a company's name.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the e-mail address.
    /// </summary>
    public string Email { get; init; }

    /// <summary>
    /// Gets the phone number, in E.164 form, so it matches the CRM, the do-not-call lists and caller ID.
    /// </summary>
    public PhoneNumber Phone { get; init; }

    /// <summary>
    /// Gets the postal address, or <see langword="null"/>.
    /// </summary>
    public Address Address { get; init; }

    /// <summary>
    /// Gets a value indicating whether any of the contact's details is known.
    /// </summary>
    public bool HasAnyValue
        => !string.IsNullOrWhiteSpace(Name) || !string.IsNullOrWhiteSpace(Email) || Phone.HasValue || Address is not null;

    /// <summary>
    /// Returns the contact of details kept as text: the phone is kept when it's in E.164 form, such as one normalized
    /// with <see cref="PhoneNumberNormalization"/>, and left out when it isn't.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="email">The e-mail address.</param>
    /// <param name="phone">The phone number as text.</param>
    /// <param name="address">The postal address.</param>
    public static ContactInfo From(string name, string email, string phone, Address address = null)
        => new()
        {
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Phone = PhoneNumber.TryFromE164(phone?.Trim(), out var phoneNumber) ? phoneNumber : default,
            Address = address,
        };
}
