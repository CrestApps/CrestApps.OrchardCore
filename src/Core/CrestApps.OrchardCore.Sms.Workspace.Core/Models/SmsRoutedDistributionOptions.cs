namespace CrestApps.OrchardCore.Sms.Workspace.Core.Models;

/// <summary>
/// Tunables for routed (push) SMS distribution. Bound from the <c>CrestApps:Sms:RoutedDistribution</c>
/// configuration section; the defaults are sensible for most deployments.
/// </summary>
public sealed class SmsRoutedDistributionOptions
{
    /// <summary>
    /// Gets or sets how many minutes a routed conversation may sit unpicked before it is re-routed or returned to
    /// the shared pool. Defaults to 5.
    /// </summary>
    public int PickupGraceMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of times a routed conversation is re-routed to another agent before it
    /// falls back to the shared pool. Defaults to 2.
    /// </summary>
    public int MaxReassignmentAttempts { get; set; } = 2;

    /// <summary>
    /// Gets or sets how many SMS-equivalent concurrency slots each live voice interaction consumes when weighing
    /// an agent's capacity for routed SMS. Defaults to 3.
    /// </summary>
    public int VoiceCapacityWeight { get; set; } = 3;
}
