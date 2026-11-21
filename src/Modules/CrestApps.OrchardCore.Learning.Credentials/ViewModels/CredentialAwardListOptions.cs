namespace CrestApps.OrchardCore.Learning.Credentials.ViewModels;

/// <summary>
/// The filters of the credential awards list.
/// </summary>
public class CredentialAwardListOptions
{
    /// <summary>
    /// Gets or sets the recipient's user name.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the definition.
    /// </summary>
    public string Definition { get; set; }

    /// <summary>
    /// Gets or sets the state: active, expired or revoked; empty for every state.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the first day of issue, in the user's time zone, as yyyy-MM-dd.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the last day of issue, in the user's time zone, as yyyy-MM-dd.
    /// </summary>
    public string To { get; set; }
}
