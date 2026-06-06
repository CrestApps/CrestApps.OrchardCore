using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the omnichannel contact part.
/// </summary>
public sealed class OmnichannelContactPart : ContentPart
{
    /// <summary>
    /// Gets or sets a value indicating whether phone calls are blocked for this contact.
    /// </summary>
    public bool DoNotCall { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when phone calls were blocked for this contact.
    /// </summary>
    public DateTime? DoNotCallUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether email is blocked for this contact.
    /// </summary>
    public bool DoNotEmail { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when email was blocked for this contact.
    /// </summary>
    public DateTime? DoNotEmailUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether SMS is blocked for this contact.
    /// </summary>
    public bool DoNotSms { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when SMS was blocked for this contact.
    /// </summary>
    public DateTime? DoNotSmsUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether chat is blocked for this contact.
    /// </summary>
    public bool DoNotChat { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when chat was blocked for this contact.
    /// </summary>
    public DateTime? DoNotChatUtc { get; set; }

    /// <summary>
    /// Updates the phone-call preference while preserving the original opt-out timestamp.
    /// </summary>
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
    /// Updates the email preference while preserving the original opt-out timestamp.
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

    /// <summary>
    /// Updates the SMS preference while preserving the original opt-out timestamp.
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
    /// Updates the chat preference while preserving the original opt-out timestamp.
    /// </summary>
    /// <param name="value">The value to apply.</param>
    /// <param name="utcNow">The current UTC time.</param>
    public void SetDoNotChat(bool value, DateTime utcNow)
    {
        if (value)
        {
            DoNotChat = true;
            DoNotChatUtc ??= utcNow;

            return;
        }

        DoNotChat = false;
        DoNotChatUtc = null;
    }
}
