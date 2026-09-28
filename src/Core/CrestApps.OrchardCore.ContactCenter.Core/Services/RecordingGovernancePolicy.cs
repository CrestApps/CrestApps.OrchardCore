using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default, fail-closed implementation of <see cref="IRecordingGovernancePolicy"/> that evaluates the
/// tenant recording governance settings.
/// </summary>
public sealed class RecordingGovernancePolicy : IRecordingGovernancePolicy
{
    private readonly ISiteService _siteService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingGovernancePolicy"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read the tenant recording governance settings.</param>
    /// <param name="clock">The clock used to resolve the recording retention window.</param>
    public RecordingGovernancePolicy(
        ISiteService siteService,
        IClock clock)
    {
        _siteService = siteService;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<RecordingGovernanceDecision> EvaluateStartAsync(Interaction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<ContactCenterRecordingSettings>();

        if (!settings.RecordingEnabled)
        {
            return RecordingGovernanceDecision.Deny(ContactCenterConstants.RecordingGovernanceDenyReason.RecordingDisabled);
        }

        // Fail closed for any consent model that is not explicitly single-party (including an undefined persisted
        // value): when explicit consent is required and none has been captured, recording is denied.
        if (settings.RequireExplicitConsent &&
            settings.ConsentModel != RecordingConsentModel.SingleParty &&
            interaction.RecordingConsentCapturedUtc is null)
        {
            return RecordingGovernanceDecision.Deny(ContactCenterConstants.RecordingGovernanceDenyReason.ConsentRequired);
        }

        var retentionDays = Math.Clamp(settings.RetentionDays, 0, ContactCenterRecordingSettings.MaxRetentionDays);

        var retainUntilUtc = retentionDays > 0
            ? _clock.UtcNow.AddDays(retentionDays)
            : (DateTime?)null;

        return RecordingGovernanceDecision.Allow(retainUntilUtc, settings.LegalHoldByDefault);
    }
}
