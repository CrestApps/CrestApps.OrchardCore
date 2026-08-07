using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// View model for the Contact Center recording governance settings page.
/// </summary>
public class ContactCenterRecordingSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether recording is permitted for this tenant.
    /// </summary>
    public bool RecordingEnabled { get; set; }

    /// <summary>
    /// Gets or sets the consent model that governs whether a call may be recorded.
    /// </summary>
    public RecordingConsentModel ConsentModel { get; set; } = RecordingConsentModel.AllParties;

    /// <summary>
    /// Gets or sets a value indicating whether explicit, recorded consent must be captured before recording starts.
    /// </summary>
    public bool RequireExplicitConsent { get; set; }

    /// <summary>
    /// Gets or sets the number of days a captured recording is retained before it becomes eligible for erasure.
    /// </summary>
    public int RetentionDays { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether captured recordings begin under legal hold.
    /// </summary>
    public bool LegalHoldByDefault { get; set; }
}
