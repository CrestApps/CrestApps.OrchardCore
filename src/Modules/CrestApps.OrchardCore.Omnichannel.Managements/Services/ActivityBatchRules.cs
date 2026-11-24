using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Why an activity load may not be loaded.
/// </summary>
internal enum ActivityBatchLoadRefusal
{
    /// <summary>
    /// The load may be started.
    /// </summary>
    None,

    /// <summary>
    /// The load already ran; a load runs once.
    /// </summary>
    AlreadyLoaded,

    /// <summary>
    /// The load is running now.
    /// </summary>
    Loading,
}

/// <summary>
/// Why an activity load may not be removed.
/// </summary>
internal enum ActivityBatchDeleteRefusal
{
    /// <summary>
    /// The load may be removed.
    /// </summary>
    None,

    /// <summary>
    /// The load is running now.
    /// </summary>
    Loading,

    /// <summary>
    /// The load already ran, and the user may not remove a loaded batch.
    /// </summary>
    LoadedNotAllowed,
}

/// <summary>
/// The rules of the Load Activities page for starting and removing a load, shared by the page and the AI tools.
/// </summary>
internal static class ActivityBatchRules
{
    /// <summary>
    /// Whether the load may be started.
    /// </summary>
    public static ActivityBatchLoadRefusal GetLoadRefusal(OmnichannelActivityBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.Status == OmnichannelActivityBatchStatus.Loaded)
        {
            return ActivityBatchLoadRefusal.AlreadyLoaded;
        }

        if (batch.Status == OmnichannelActivityBatchStatus.Started || batch.Status == OmnichannelActivityBatchStatus.Loading)
        {
            return ActivityBatchLoadRefusal.Loading;
        }

        return ActivityBatchLoadRefusal.None;
    }

    /// <summary>
    /// Marks the load as started by the user. A new load starts from the beginning, with no error from the last one,
    /// and records who started it and when, so the watchdog can tell it from a load that stalled and resume it as the
    /// same user. The caller saves the batch and queues the load.
    /// </summary>
    public static void MarkStarted(OmnichannelActivityBatch batch, string loaderId, string loaderUserName, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(batch);

        batch.ResetLoadProgress();
        batch.Status = OmnichannelActivityBatchStatus.Started;
        batch.LoadStartedUtc = utcNow;
        batch.LoadStartedById = loaderId;
        batch.LoadStartedByUsername = loaderUserName;
    }

    /// <summary>
    /// Whether the user may remove the load. A batch is never removed while it is being loaded: the load reads it again
    /// as it commits, and would store it a second time. A loaded batch is only the record of a finished load. Nothing
    /// refers to it afterwards -- the activities it created carry no reference back to it and stay exactly as they are
    /// -- so it may be removed, but only by someone allowed to remove that record.
    /// </summary>
    public static async Task<ActivityBatchDeleteRefusal> GetDeleteRefusalAsync(
        IAuthorizationService authorizationService,
        ClaimsPrincipal user,
        OmnichannelActivityBatch batch)
    {
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.Status == OmnichannelActivityBatchStatus.Started || batch.Status == OmnichannelActivityBatchStatus.Loading)
        {
            return ActivityBatchDeleteRefusal.Loading;
        }

        if (batch.Status == OmnichannelActivityBatchStatus.Loaded &&
            !await authorizationService.AuthorizeAsync(user, OmnichannelConstants.Permissions.DeleteLoadedActivityBatches))
        {
            return ActivityBatchDeleteRefusal.LoadedNotAllowed;
        }

        return ActivityBatchDeleteRefusal.None;
    }
}
