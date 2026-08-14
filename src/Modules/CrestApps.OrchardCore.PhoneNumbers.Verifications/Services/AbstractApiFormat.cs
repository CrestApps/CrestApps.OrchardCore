namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Represents the formatted phone number values nested under the AbstractAPI <c>format</c> object.
/// </summary>
internal sealed class AbstractApiFormat
{
    /// <summary>
    /// Gets or sets the international (E.164) format of the phone number.
    /// </summary>
    public string International { get; set; }

    /// <summary>
    /// Gets or sets the local format of the phone number.
    /// </summary>
    public string Local { get; set; }
}
