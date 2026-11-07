using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.Outreach.Core.Services;

/// <summary>
/// Skips the recipients who opted out of a run's <see cref="OutreachRun.ConsentChannel"/>, by asking the consent
/// service, so every bulk send honors the same opt-outs, unsubscribes and consent records as every other send.
/// </summary>
/// <remarks>
/// A run without a consent channel is not checked here, and nor is any run while no feature registers the consent
/// service.
/// </remarks>
public sealed class ConsentOutreachRecipientPolicy : IOutreachRecipientPolicy
{
    private readonly IConsentService _consentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentOutreachRecipientPolicy"/> class.
    /// </summary>
    /// <param name="consentServices">The consent service, when a feature registers it.</param>
    public ConsentOutreachRecipientPolicy(IEnumerable<IConsentService> consentServices)
    {
        _consentService = consentServices.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<string> GetSkipReasonAsync(OutreachRun run, OutreachRecipient recipient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(recipient);

        if (_consentService is null || string.IsNullOrWhiteSpace(run.ConsentChannel))
        {
            return null;
        }

        var status = await _consentService.GetAsync(new ConsentQuery
        {
            Channel = run.ConsentChannel,
            Address = recipient.Address,
            ContactKind = run.ConsentContactKind,
            ContactId = recipient.ContactId,
        }, cancellationToken);

        return status?.HasOptedOut == true
            ? OutreachConstants.SkipReasons.OptedOut
            : null;
    }
}
