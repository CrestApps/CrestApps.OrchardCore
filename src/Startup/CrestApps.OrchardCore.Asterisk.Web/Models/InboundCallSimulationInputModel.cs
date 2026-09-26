using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.Asterisk.Web.Models;

/// <summary>
/// Captures the operator-supplied settings for a burst of simulated inbound calls.
/// </summary>
public sealed class InboundCallSimulationInputModel
{
    /// <summary>
    /// Gets or sets the Orchard Core site root URL.
    /// </summary>
    [Required]
    [Url]
    [Display(Name = "Orchard base URL")]
    public string OrchardBaseUrl { get; set; } = "https://localhost:5001";

    /// <summary>
    /// Gets or sets the login path used to sign in before posting the simulated calls.
    /// </summary>
    [Required]
    [Display(Name = "Login path")]
    public string LoginPath { get; set; } = "/Login";

    /// <summary>
    /// Gets or sets the authenticated inbound voice ingress path.
    /// </summary>
    [Required]
    [Display(Name = "Inbound path")]
    public string InboundPath { get; set; } = "/api/contact-center/voice/inbound";

    /// <summary>
    /// Gets or sets the Orchard user name that has permission to manage interactions.
    /// </summary>
    [Required]
    [Display(Name = "User name")]
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the Orchard password.
    /// </summary>
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets the provider name recorded on each normalized inbound call event.
    /// </summary>
    [Required]
    [Display(Name = "Provider name")]
    public string ProviderName { get; set; } = "Default Asterisk";

    /// <summary>
    /// Gets or sets the Asterisk extension or destination to dial when the simulator uses the loopback path.
    /// </summary>
    [Required]
    [Display(Name = "Asterisk destination")]
    public string AsteriskDestination { get; set; } = "1000";

    /// <summary>
    /// Gets or sets the dialed service address or DID that the Contact Center entry point resolves.
    /// </summary>
    [Required]
    [Display(Name = "To address")]
    public string ToAddress { get; set; } = "+15550001000";

    /// <summary>
    /// Gets or sets the base caller number used to generate unique caller identities.
    /// </summary>
    [Required]
    [Display(Name = "Caller number seed")]
    public string CallerNumberSeed { get; set; } = "+15551230000";

    /// <summary>
    /// Gets or sets the prefix used to generate caller display names.
    /// </summary>
    [Required]
    [Display(Name = "Caller name prefix")]
    public string CallerNamePrefix { get; set; } = "Sim Caller";

    /// <summary>
    /// Gets or sets how many inbound calls to create in the burst.
    /// </summary>
    [Range(1, 100)]
    [Display(Name = "Call count")]
    public int Count { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether the simulator should fire all requests concurrently.
    /// </summary>
    [Display(Name = "Send concurrently")]
    public bool SendConcurrently { get; set; } = true;
}
