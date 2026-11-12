using System.Security.Claims;
using CrestApps.OrchardCore.AI.Core;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Memory.Services;

/// <summary>
/// Picks the permission that clearing a user's AI memory needs. Orchard Core grants a permission whatever resource is
/// passed with it, so the target user must choose the permission; it can't be passed as a resource.
/// </summary>
internal static class ClearAIMemoryPermission
{
    /// <summary>
    /// Returns <see cref="AIPermissions.ClearAIMemory"/> when the target is the caller, otherwise
    /// <see cref="AIPermissions.ClearAIMemoryForOthers"/>.
    /// </summary>
    /// <param name="user">The caller.</param>
    /// <param name="targetUserId">The user whose memory is cleared.</param>
    public static Permission For(ClaimsPrincipal user, string targetUserId)
    {
        var currentUserId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        return !string.IsNullOrEmpty(currentUserId) && string.Equals(currentUserId, targetUserId, StringComparison.Ordinal)
            ? AIPermissions.ClearAIMemory
            : AIPermissions.ClearAIMemoryForOthers;
    }
}
