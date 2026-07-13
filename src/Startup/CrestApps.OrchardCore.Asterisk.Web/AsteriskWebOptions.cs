namespace CrestApps.OrchardCore.Asterisk.Web;

/// <summary>
/// Provides configuration-backed defaults for the Asterisk sample web application.
/// </summary>
public sealed class AsteriskWebOptions
{
    /// <summary>
    /// Gets or sets the Orchard Core site root URL.
    /// </summary>
    public string OrchardBaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the login path the simulator uses when signing in.
    /// </summary>
    public string LoginPath { get; set; }

    /// <summary>
    /// Gets or sets the authenticated inbound voice ingress path.
    /// </summary>
    public string InboundPath { get; set; }

    /// <summary>
    /// Gets or sets the default provider name stamped onto simulated inbound events.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the default Asterisk extension or destination to originate when the simulator uses the Asterisk loopback path.
    /// </summary>
    public string AsteriskDestination { get; set; }

    /// <summary>
    /// Gets or sets the Asterisk endpoint template used when the simulator originates loopback calls.
    /// </summary>
    public string AsteriskEndpointTemplate { get; set; }

    /// <summary>
    /// Gets or sets the Asterisk Stasis application name used by local simulations and event subscriptions.
    /// </summary>
    public string AsteriskApplicationName { get; set; }

    /// <summary>
    /// Gets or sets the timeout, in seconds, used for Asterisk originate requests.
    /// </summary>
    public int AsteriskTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets how long the simulator waits for the Stasis listener to forward the originated call into Orchard.
    /// </summary>
    public int SimulationTimeoutSeconds { get; set; } = 45;

    /// <summary>
    /// Gets or sets the default dialed service address or DID.
    /// </summary>
    public string ToAddress { get; set; }

    /// <summary>
    /// Gets or sets the default caller-number seed used for generating unique callers.
    /// </summary>
    public string CallerNumberSeed { get; set; }

    /// <summary>
    /// Gets or sets the default caller-name prefix.
    /// </summary>
    public string CallerNamePrefix { get; set; }

    /// <summary>
    /// Gets or sets the default Asterisk endpoint used for party A in a two-party simulation.
    /// </summary>
    public string TwoPartyEndpointA { get; set; }

    /// <summary>
    /// Gets or sets the default caller identifier used for party A in a two-party simulation.
    /// </summary>
    public string TwoPartyCallerIdA { get; set; }

    /// <summary>
    /// Gets or sets the default Asterisk endpoint used for party B in a two-party simulation.
    /// </summary>
    public string TwoPartyEndpointB { get; set; }

    /// <summary>
    /// Gets or sets the default caller identifier used for party B in a two-party simulation.
    /// </summary>
    public string TwoPartyCallerIdB { get; set; }

    /// <summary>
    /// Gets or sets the Asterisk ARI base URL used by the diagnostics panel.
    /// </summary>
    public string AsteriskBaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the Asterisk ARI user name used by the diagnostics panel.
    /// </summary>
    public string AsteriskUserName { get; set; }

    /// <summary>
    /// Gets or sets the Asterisk ARI password used by the diagnostics panel.
    /// </summary>
    public string AsteriskPassword { get; set; }

    /// <summary>
    /// Gets or sets how often the live dashboard performs a reconciliation refresh from Asterisk.
    /// </summary>
    public int AsteriskRefreshSeconds { get; set; } = 15;
}
