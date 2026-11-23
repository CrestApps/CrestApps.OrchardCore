using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// The answer of <c>/institutions/get_by_id</c>.
/// </summary>
internal sealed class PlaidInstitutionResponse
{
    /// <summary>
    /// Gets or sets the bank.
    /// </summary>
    [JsonPropertyName("institution")]
    public PlaidInstitution Institution { get; set; }
}
