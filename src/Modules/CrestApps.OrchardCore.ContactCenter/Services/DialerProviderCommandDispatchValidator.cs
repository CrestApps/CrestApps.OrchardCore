using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Revalidates outbound dialer compliance before a recovered pending command contacts a provider.
/// </summary>
public sealed class DialerProviderCommandDispatchValidator : IProviderCommandDispatchValidator
{
    private readonly IDialerProfileManager _profileManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IDialerEligibilityService _eligibilityService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProviderCommandDispatchValidator"/> class.
    /// </summary>
    /// <param name="profileManager">The manager used to load the governing dialer profile.</param>
    /// <param name="activityManager">The manager used to load the current CRM activity.</param>
    /// <param name="eligibilityService">The compliance gate used to revalidate outbound eligibility.</param>
    public DialerProviderCommandDispatchValidator(
        IDialerProfileManager profileManager,
        IOmnichannelActivityManager activityManager,
        IDialerEligibilityService eligibilityService)
    {
        _profileManager = profileManager;
        _activityManager = activityManager;
        _eligibilityService = eligibilityService;
    }

    /// <inheritdoc/>
    public async Task<bool> CanDispatchAsync(ProviderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.DialerProfileId) ||
            string.IsNullOrWhiteSpace(command.ActivityItemId))
        {
            return false;
        }

        var profile = await _profileManager.FindByIdAsync(command.DialerProfileId, cancellationToken);
        var activity = await _activityManager.FindByIdAsync(command.ActivityItemId, cancellationToken);

        if (profile is null || activity is null)
        {
            return false;
        }

        var eligibility = await _eligibilityService.EvaluateAsync(new DialerEligibilityContext
        {
            Profile = profile,
            Activity = activity,
        }, cancellationToken);

        return eligibility.IsEligible;
    }
}
