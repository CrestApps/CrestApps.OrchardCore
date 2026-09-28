using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.Asterisk.Web.Models;

/// <summary>
/// Represents the two endpoints used to create a bridged Asterisk call simulation.
/// </summary>
public sealed class TwoPartyCallSimulationInputModel
{
    /// <summary>
    /// Gets or sets the Asterisk endpoint for the first party.
    /// </summary>
    [Required]
    [Display(Name = "Party A endpoint")]
    public string PartyAEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the caller identifier presented for the first party.
    /// </summary>
    [Display(Name = "Party A caller id")]
    public string PartyACallerId { get; set; }

    /// <summary>
    /// Gets or sets the Asterisk endpoint for the second party.
    /// </summary>
    [Required]
    [Display(Name = "Party B endpoint")]
    public string PartyBEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the caller identifier presented for the second party.
    /// </summary>
    [Display(Name = "Party B caller id")]
    public string PartyBCallerId { get; set; }
}
