namespace CrestApps.OrchardCore.Telephony.Sms.Core.Models;

/// <summary>
/// Tenant-level settings for the SMS Communication Portal, stored as site settings and surfaced in the portal
/// administration.
/// </summary>
public sealed class SmsPortalSettings
{
    /// <summary>
    /// Gets or sets the technical name of the SMS provider used when no specific number pins a provider — a
    /// brand-new provider-agnostic send, or a number whose <c>ProviderName</c> is left unset (back-compat for
    /// existing endpoints). When empty, the built-in tenant-default provider (<c>SmsSettings.DefaultProviderName</c>)
    /// is used.
    /// </summary>
    public string DefaultProviderName { get; set; }
}
