using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Revalidates outbound dialer compliance before a recovered pending command contacts a provider.
/// </summary>
public sealed class DialerProviderCommandDispatchValidator : IProviderCommandDispatchValidator
{
    private readonly IDialerProfileManager _profileManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IDialerEligibilityService _eligibilityService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProviderCommandDispatchValidator"/> class.
    /// </summary>
    /// <param name="profileManager">The manager used to load the governing dialer profile.</param>
    /// <param name="activityManager">The manager used to load the current CRM activity.</param>
    /// <param name="eligibilityService">The compliance gate used to revalidate outbound eligibility.</param>
    /// <param name="logger">The logger used to surface why a dial dispatch was refused.</param>
    public DialerProviderCommandDispatchValidator(
        IDialerProfileManager profileManager,
        IOmnichannelActivityManager activityManager,
        IDialerEligibilityService eligibilityService,
        ILogger<DialerProviderCommandDispatchValidator> logger)
    {
        _profileManager = profileManager;
        _activityManager = activityManager;
        _eligibilityService = eligibilityService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> CanDispatchAsync(ProviderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Refusing dispatch here compensates the dial command and fails the interaction, so an agent who clicked
        // Dial sees nothing happen. Log every refusal reason so a silent no-dial is diagnosable from the log.
        if (string.IsNullOrWhiteSpace(command.DialerProfileId) ||
            string.IsNullOrWhiteSpace(command.ActivityItemId))
        {
            _logger.LogWarning(
                "Refused to dispatch dial command '{CommandId}' because it is missing its dialer profile or activity reference (profile '{DialerProfileId}', activity '{ActivityItemId}').",
                command.CommandId.SanitizeLogValue(),
                command.DialerProfileId.SanitizeLogValue(),
                command.ActivityItemId.SanitizeLogValue());

            return false;
        }

        var profile = await _profileManager.FindByIdAsync(command.DialerProfileId, cancellationToken);
        var activity = await _activityManager.FindByIdAsync(command.ActivityItemId, cancellationToken);

        if (profile is null || activity is null)
        {
            _logger.LogWarning(
                "Refused to dispatch dial command '{CommandId}' because its dialer profile '{DialerProfileId}' ({ProfileState}) or activity '{ActivityItemId}' ({ActivityState}) no longer exists.",
                command.CommandId.SanitizeLogValue(),
                command.DialerProfileId.SanitizeLogValue(),
                profile is null ? "missing" : "found",
                command.ActivityItemId.SanitizeLogValue(),
                activity is null ? "missing" : "found");

            return false;
        }

        var eligibility = await _eligibilityService.EvaluateAsync(new DialerEligibilityContext
        {
            Profile = profile,
            Activity = activity,

            // This runs after the dial attempt was recorded but before it reaches the provider, so the
            // maximum-attempts gate must not count the in-flight attempt against the limit.
            AttemptAlreadyCounted = true,
        }, cancellationToken);

        if (!eligibility.IsEligible)
        {
            _logger.LogWarning(
                "Refused to dispatch dial command '{CommandId}' for activity '{ActivityItemId}' under dialer profile '{ProfileName}' because the compliance gate suppressed it: {SuppressionReason} - {SuppressionDescription}.",
                command.CommandId.SanitizeLogValue(),
                command.ActivityItemId.SanitizeLogValue(),
                profile.Name.SanitizeLogValue(),
                eligibility.Reason,
                eligibility.Description.SanitizeLogValue());
        }

        return eligibility.IsEligible;
    }
}
