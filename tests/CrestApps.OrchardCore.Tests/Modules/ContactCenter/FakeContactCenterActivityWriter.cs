using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An <see cref="IContactCenterActivityWriter"/> that applies the scheduled CRM mutation immediately, which
/// is what the production writer falls back to when there is no shell scope to defer to.
/// </summary>
internal sealed class FakeContactCenterActivityWriter : IContactCenterActivityWriter
{
    private readonly IOmnichannelActivityManager _activityManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeContactCenterActivityWriter"/> class.
    /// </summary>
    /// <param name="activityManager">The activity manager used to load and persist the activity.</param>
    public FakeContactCenterActivityWriter(IOmnichannelActivityManager activityManager)
    {
        _activityManager = activityManager;
    }

    /// <inheritdoc/>
    public Task ScheduleUpdateAsync(
        string activityItemId,
        Action<OmnichannelActivity> mutate,
        CancellationToken cancellationToken = default)
        => UpdateAsync(activityItemId, mutate, cancellationToken);

    /// <inheritdoc/>
    public async Task UpdateAsync(
        string activityItemId,
        Action<OmnichannelActivity> mutate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        if (string.IsNullOrEmpty(activityItemId))
        {
            return;
        }

        var activity = await _activityManager.FindByIdAsync(activityItemId, cancellationToken);

        if (activity is null)
        {
            return;
        }

        mutate(activity);

        await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);
    }
}
