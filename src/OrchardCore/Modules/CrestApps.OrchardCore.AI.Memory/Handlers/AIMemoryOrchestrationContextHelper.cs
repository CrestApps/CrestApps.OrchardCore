using System.Security.Claims;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Memory.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Handlers;

internal static class AIMemoryOrchestrationContextHelper
{
    public static string GetAuthenticatedUserId(IHttpContextAccessor httpContextAccessor)
        => httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true
    ? httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
    : null;

    public static async Task<bool> IsEnabledAsync(object resource, ISiteService siteService)
    {
        if (resource is AIProfile profile)
        {
            return profile.GetSettings<AIProfileMemorySettings>().EnableUserMemory;
        }

        if (resource is ChatInteraction)
        {
            return (await siteService.GetSettingsAsync<ChatInteractionMemorySettings>()).EnableUserMemory;
        }

        return false;
    }
}
