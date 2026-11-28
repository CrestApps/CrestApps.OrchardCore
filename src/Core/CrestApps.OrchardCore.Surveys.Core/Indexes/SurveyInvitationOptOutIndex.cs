using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Finds the opt-out records of an address by its hash.
/// </summary>
public sealed class SurveyInvitationOptOutIndex : MapIndex
{
    /// <summary>
    /// The longest hash: the <c>h1:</c> prefix and 64 hex characters.
    /// </summary>
    public const int HashLength = 80;

    /// <summary>
    /// Gets or sets the hash of the address.
    /// </summary>
    public string AddressHash { get; set; }
}
