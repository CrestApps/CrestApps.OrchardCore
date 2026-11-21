using CrestApps.OrchardCore.Learning.Credentials.Models;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// The filters of the credential awards admin list.
/// </summary>
public sealed class CredentialAwardAdminFilter
{
    /// <summary>
    /// Gets or sets the definition.
    /// </summary>
    public string DefinitionId { get; set; }

    /// <summary>
    /// Gets or sets the recipient.
    /// </summary>
    public string RecipientUserId { get; set; }

    /// <summary>
    /// Gets or sets the state, or <see langword="null"/> for every state.
    /// </summary>
    public CredentialAwardStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the first moment of issue, in UTC.
    /// </summary>
    public DateTime? IssuedFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the moment of issue the list stops before, in UTC.
    /// </summary>
    public DateTime? IssuedToUtc { get; set; }
}
