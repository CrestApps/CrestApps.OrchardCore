
namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// A bank Plaid connects to, from <c>/institutions/get_by_id</c>.
/// </summary>
public sealed class PlaidInstitution
{
    /// <summary>
    /// Gets or sets Plaid's id of the bank.
    /// </summary>
    public string InstitutionId { get; set; }

    /// <summary>
    /// Gets or sets the bank's name.
    /// </summary>
    public string Name { get; set; }
}
