namespace CrestApps.OrchardCore.Omnichannel.Models;

/// <summary>
/// A phone number a contact can be reached on.
/// </summary>
public sealed class ContactPhoneNumber
{
    /// <summary>
    /// Gets or sets the number, in E.164 form when the host was able to normalize it.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the extension to dial once the number answers, when there is one.
    /// </summary>
    public string Extension { get; set; }

    /// <summary>
    /// Gets or sets what kind of number this is, usually one of <see cref="ContactPhoneNumberType"/>.
    /// </summary>
    public string Type { get; set; }
}
