namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents the provider's reconciliation result for a previously sent voice command.
/// </summary>
public sealed class ContactCenterVoiceCommandReconciliationResult
{
    /// <summary>
    /// Gets or sets the provider's knowledge of the command outcome.
    /// </summary>
    public ContactCenterVoiceCommandReconciliationOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets the provider-assigned call identifier when the command is confirmed.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets a provider-safe explanation of the reconciliation result.
    /// </summary>
    public string Message { get; set; }
}
