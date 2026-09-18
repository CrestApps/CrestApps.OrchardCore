using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Omnichannel.Models;

/// <summary>
/// A person the organization talks to.
/// </summary>
/// <remarks>
/// A projection: in a host that stores contacts as content items this is built on read and never
/// saved, and in a host that has no content model this is the record itself. Either way the services
/// that dial, message and report on a contact see only this.
/// </remarks>
public sealed class OmnichannelContact
{
    /// <summary>
    /// Gets or sets the identifier the rest of the suite stores against activities and conversations.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the <see cref="ContactDefinition"/> this contact is an instance of.
    /// </summary>
    public string DefinitionName { get; set; }

    /// <summary>
    /// Gets or sets the name to show.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the first name.
    /// </summary>
    public string FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name.
    /// </summary>
    public string LastName { get; set; }

    /// <summary>
    /// Gets or sets the contact's local time zone identifier.
    /// </summary>
    /// <remarks>
    /// What decides whether it is a reasonable hour to call them, so it is the contact's own time zone
    /// and never the operator's.
    /// </remarks>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether phone calls are blocked for this contact.
    /// </summary>
    public bool DoNotCall { get; set; }

    /// <summary>
    /// Gets or sets when phone calls were blocked, in UTC.
    /// </summary>
    public DateTime? DoNotCallUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether SMS is blocked for this contact.
    /// </summary>
    public bool DoNotSms { get; set; }

    /// <summary>
    /// Gets or sets when SMS was blocked, in UTC.
    /// </summary>
    public DateTime? DoNotSmsUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether email is blocked for this contact.
    /// </summary>
    public bool DoNotEmail { get; set; }

    /// <summary>
    /// Gets or sets when email was blocked, in UTC.
    /// </summary>
    public DateTime? DoNotEmailUtc { get; set; }

    /// <summary>
    /// Gets or sets the phone numbers this contact can be reached on.
    /// </summary>
    public IList<ContactPhoneNumber> PhoneNumbers { get; set; } = [];

    /// <summary>
    /// Gets or sets the email addresses this contact can be reached at.
    /// </summary>
    public IList<ContactEmail> Emails { get; set; } = [];

    /// <summary>
    /// Gets or sets whatever else the host carries on this contact.
    /// </summary>
    /// <remarks>
    /// Opaque to the suite. It exists so a caller that needs a field the contract does not name can
    /// still reach it, rather than the contract growing a property per tenant.
    /// </remarks>
    public JsonObject Properties { get; set; } = [];

    /// <summary>
    /// Gets the number to dial or message, preferring a mobile number.
    /// </summary>
    /// <remarks>
    /// Mobile first because it is the only kind that can receive a text; failing that, the first number
    /// on file, so a contact with one unclassified number is still reachable.
    /// </remarks>
    /// <returns>The number, or <see langword="null"/> when there is none.</returns>
    public ContactPhoneNumber GetPrimaryPhoneNumber()
    {
        if (PhoneNumbers is null || PhoneNumbers.Count == 0)
        {
            return null;
        }

        return PhoneNumbers.FirstOrDefault(number =>
                   !string.IsNullOrWhiteSpace(number?.Number) &&
                   string.Equals(number.Type, ContactPhoneNumberType.Cell, StringComparison.OrdinalIgnoreCase))
               ?? PhoneNumbers.FirstOrDefault(number => !string.IsNullOrWhiteSpace(number?.Number));
    }

    /// <summary>
    /// Gets the address to email.
    /// </summary>
    /// <returns>The address, or <see langword="null"/> when there is none.</returns>
    public string GetPrimaryEmail()
        => Emails?.FirstOrDefault(email => !string.IsNullOrWhiteSpace(email?.Email))?.Email?.Trim();

    /// <summary>
    /// Sets the phone-call preference, keeping the original opt-out time.
    /// </summary>
    /// <remarks>
    /// The stamp is kept because it records when the person asked, which is what a compliance audit
    /// looks at; re-stamping it on every save would erase that.
    /// </remarks>
    /// <param name="value">The value to apply.</param>
    /// <param name="utcNow">The current UTC time.</param>
    public void SetDoNotCall(bool value, DateTime utcNow)
    {
        if (value)
        {
            DoNotCall = true;
            DoNotCallUtc ??= utcNow;

            return;
        }

        DoNotCall = false;
        DoNotCallUtc = null;
    }

    /// <summary>
    /// Sets the SMS preference, keeping the original opt-out time.
    /// </summary>
    /// <param name="value">The value to apply.</param>
    /// <param name="utcNow">The current UTC time.</param>
    public void SetDoNotSms(bool value, DateTime utcNow)
    {
        if (value)
        {
            DoNotSms = true;
            DoNotSmsUtc ??= utcNow;

            return;
        }

        DoNotSms = false;
        DoNotSmsUtc = null;
    }

    /// <summary>
    /// Sets the email preference, keeping the original opt-out time.
    /// </summary>
    /// <param name="value">The value to apply.</param>
    /// <param name="utcNow">The current UTC time.</param>
    public void SetDoNotEmail(bool value, DateTime utcNow)
    {
        if (value)
        {
            DoNotEmail = true;
            DoNotEmailUtc ??= utcNow;

            return;
        }

        DoNotEmail = false;
        DoNotEmailUtc = null;
    }
}
