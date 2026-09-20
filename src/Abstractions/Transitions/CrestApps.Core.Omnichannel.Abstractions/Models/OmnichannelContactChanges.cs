namespace CrestApps.Core.Omnichannel.Models;

/// <summary>
/// What to change about a contact.
/// </summary>
/// <remarks>
/// <para>
/// A change set rather than a whole contact, because the callers are automated conversations applying
/// one thing they learned. Handing back a whole contact would let a stale read silently revert every
/// other field on it.
/// </para>
/// <para>
/// Every property left unset means "leave it alone". That is why the preference flags are nullable:
/// <see langword="false"/> is a request to clear an opt-out, which is not the same as saying nothing.
/// </para>
/// </remarks>
public sealed class OmnichannelContactChanges
{
    /// <summary>
    /// Gets or sets the first name to apply.
    /// </summary>
    public string FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name to apply.
    /// </summary>
    public string LastName { get; set; }

    /// <summary>
    /// Gets or sets the time zone identifier to apply.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the phone-call preference to apply.
    /// </summary>
    public bool? DoNotCall { get; set; }

    /// <summary>
    /// Gets or sets the SMS preference to apply.
    /// </summary>
    public bool? DoNotSms { get; set; }

    /// <summary>
    /// Gets or sets the email preference to apply.
    /// </summary>
    public bool? DoNotEmail { get; set; }

    /// <summary>
    /// Gets or sets an email address to record for follow-up.
    /// </summary>
    /// <remarks>
    /// Replaces the address on file rather than adding another, because appending is how a contact ends
    /// up with the same address several times over.
    /// </remarks>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets a phone number to record.
    /// </summary>
    /// <remarks>
    /// Replaces the number of the same <see cref="ContactPhoneNumber.Type"/> when there is one, and is
    /// added otherwise.
    /// </remarks>
    public ContactPhoneNumber PhoneNumber { get; set; }
}
