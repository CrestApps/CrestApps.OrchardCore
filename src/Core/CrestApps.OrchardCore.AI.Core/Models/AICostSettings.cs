namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// What the AI and the phone lines cost, for the estimated cost of each AI call.
/// </summary>
public sealed class AICostSettings
{
    /// <summary>
    /// Gets or sets the currency the prices are in, such as <c>USD</c>.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Gets or sets what a minute of a phone call costs with the carrier.
    /// </summary>
    public decimal TelephonyPerMinute { get; set; }

    /// <summary>
    /// Gets or sets what one text message costs with the carrier.
    /// </summary>
    public decimal SmsPerMessage { get; set; }

    /// <summary>
    /// Gets or sets the price of each AI deployment.
    /// </summary>
    public List<AIDeploymentPrice> Deployments { get; set; } = [];
}

/// <summary>
/// What an AI deployment costs: per million tokens of each kind, and per minute for models billed by the minute.
/// </summary>
public sealed class AIDeploymentPrice
{
    /// <summary>
    /// Gets or sets the deployment's name.
    /// </summary>
    public string DeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the price of a million text input tokens.
    /// </summary>
    public decimal InputPerMillion { get; set; }

    /// <summary>
    /// Gets or sets the price of a million text output tokens.
    /// </summary>
    public decimal OutputPerMillion { get; set; }

    /// <summary>
    /// Gets or sets the price of a million audio input tokens.
    /// </summary>
    public decimal AudioInputPerMillion { get; set; }

    /// <summary>
    /// Gets or sets the price of a million audio output tokens.
    /// </summary>
    public decimal AudioOutputPerMillion { get; set; }

    /// <summary>
    /// Gets or sets the price of a minute, for models billed by the minute rather than by the token.
    /// </summary>
    public decimal PerMinute { get; set; }
}
