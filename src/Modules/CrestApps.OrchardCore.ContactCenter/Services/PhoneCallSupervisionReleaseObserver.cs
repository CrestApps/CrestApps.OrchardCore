using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Lets every supervisor still listening to an agent's own phone call go when the call ends. A Contact Center call's
/// supervisors are released when its interaction ends; a phone call has none, so its end in the soft phone's call
/// history does it instead.
/// </summary>
internal sealed class PhoneCallSupervisionReleaseObserver : ITelephonyCallObserver
{
    private readonly Lazy<IContactCenterPhoneCallSupervisionService> _phoneCalls;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneCallSupervisionReleaseObserver"/> class.
    /// </summary>
    /// <param name="phoneCalls">The phone call supervision. Lazy because it reads the call history store that tells this observer.</param>
    public PhoneCallSupervisionReleaseObserver(Lazy<IContactCenterPhoneCallSupervisionService> phoneCalls)
    {
        _phoneCalls = phoneCalls;
    }

    /// <inheritdoc/>
    public Task CallStartedAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task CallEndedAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(interaction?.CallId)
            ? Task.CompletedTask
            : _phoneCalls.Value.ReleaseCallAsync(interaction.CallId, cancellationToken);
}
