using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Puts an automated voice call through the same screening a person's outbound call goes through.
/// </summary>
/// <remarks>
/// An agent dialling from the soft phone is screened at the shared telephony boundary: the contact's own
/// preference, every configured national do-not-call registry, and the calling window for their region. An
/// automated call reached the provider by a different road — the voice client is called directly, never through
/// that boundary — and so was screened by none of it. The obligation does not depend on who dialled, and a
/// machine placing thousands of calls has more need of the check than a person placing one.
/// <para>
/// Refusals fail closed by construction: the underlying screener already refuses a number it cannot canonicalize
/// and a registry that cannot answer, because a registry that says "not listed" for a number it never compared is
/// exactly how a call reaches somebody on the list.
/// </para>
/// </remarks>
public sealed class AutomatedVoiceCallScreener : IAutomatedActivityScreener
{
    private readonly IOutboundCallScreeningService _screeningService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutomatedVoiceCallScreener"/> class.
    /// </summary>
    public AutomatedVoiceCallScreener(
        IOutboundCallScreeningService screeningService,
        ILogger<AutomatedVoiceCallScreener> logger)
    {
        _screeningService = screeningService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Channel => OmnichannelConstants.Channels.Phone;

    /// <inheritdoc/>
    public async Task<AutomatedActivityScreeningResult> ScreenAsync(OmnichannelActivity activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (string.IsNullOrWhiteSpace(activity.PreferredDestination))
        {
            // There is nothing to dial, so there is nothing to screen. The absence of a destination is handled
            // where the call is placed; it is not a compliance decision.
            return AutomatedActivityScreeningResult.Allow();
        }

        var screening = await _screeningService.ScreenAsync(
            new OutboundCallScreeningContext
            {
                Request = new DialRequest
                {
                    To = activity.PreferredDestination,
                },
                Origin = OutboundCallOrigin.AutomatedVoice,
            },
            cancellationToken);

        if (screening.IsAllowed)
        {
            return AutomatedActivityScreeningResult.Allow();
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Automated call for activity '{ActivityId}' was refused by screening: {Reason}.",
                activity.ItemId.SanitizeLogValue(),
                screening.Reason.SanitizeLogValue());
        }

        return AutomatedActivityScreeningResult.Deny(screening.Reason, screening.Description);
    }
}
