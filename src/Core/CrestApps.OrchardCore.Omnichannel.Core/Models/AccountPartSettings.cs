namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the settings for <see cref="AccountPart"/> on an account content type.
/// </summary>
public sealed class AccountPartSettings
{
    /// <summary>
    /// Gets or sets the content types that were already added to the account's list. A contact or opportunity
    /// type is added once, when it first appears, and never again, so an administrator who removes a type from the
    /// list keeps it removed.
    /// </summary>
    public string[] OfferedContentTypes { get; set; } = [];
}
