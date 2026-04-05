using System.Security.Claims;
using CrestApps.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CrestApps.AI.Handlers;

internal static class AIMemoryOrchestrationContextHelper
{
    public static string GetAuthenticatedUserId(IHttpContextAccessor httpContextAccessor)
        => httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true
            ? httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Name)
            : null;

    public static bool IsEnabled(
        object resource,
        IOptions<ChatInteractionMemoryOptions> chatInteractionMemoryOptions)
    {
        if (resource is AIProfile profile)
        {
            return profile.GetMemoryMetadata().EnableUserMemory ?? false;
        }

        if (resource is ChatInteraction)
        {
            return chatInteractionMemoryOptions.Value.EnableUserMemory;
        }

        return false;
    }
}
