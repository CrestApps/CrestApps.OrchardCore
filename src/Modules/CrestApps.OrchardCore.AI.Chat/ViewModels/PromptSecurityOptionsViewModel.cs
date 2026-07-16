using CrestApps.Core.AI.Security;

namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the site settings editor model for AI chat prompt security.
/// </summary>
public sealed class PromptSecurityOptionsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether injection detection is enabled.
    /// </summary>
    public bool EnableInjectionDetection { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether output filtering is enabled.
    /// </summary>
    public bool EnableOutputFiltering { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the security preamble is enabled.
    /// </summary>
    public bool EnableSecurityPreamble { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether input delimiters are enabled.
    /// </summary>
    public bool EnableInputDelimiters { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether audit logging is enabled.
    /// </summary>
    public bool EnableAuditLogging { get; set; }

    /// <summary>
    /// Gets or sets the maximum prompt length.
    /// </summary>
    public int MaxPromptLength { get; set; }

    /// <summary>
    /// Gets or sets the blocking threshold.
    /// </summary>
    public PromptRiskLevel BlockingThreshold { get; set; }

    /// <summary>
    /// Gets or sets the maximum messages per rate-limit window.
    /// </summary>
    public int MaxMessagesPerWindow { get; set; }

    /// <summary>
    /// Gets or sets the rate-limit window length in seconds.
    /// </summary>
    public int RateLimitWindowSeconds { get; set; }

    /// <summary>
    /// Gets or sets the maximum anonymous sessions per rate-limit window.
    /// </summary>
    public int MaxAnonymousSessionsPerWindow { get; set; }

    /// <summary>
    /// Gets or sets the anonymous session-start rate-limit window length in seconds.
    /// </summary>
    public int AnonymousSessionRateLimitWindowSeconds { get; set; }
}
