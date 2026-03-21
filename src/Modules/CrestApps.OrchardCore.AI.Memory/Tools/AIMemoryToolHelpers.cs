using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Memory.Tools;

internal static class AIMemoryToolHelpers
{
    public static string GetCurrentUserId(IServiceProvider services = null)
    {
        var invocationContext = AIInvocationScope.Current;

        if (invocationContext?.Items.TryGetValue(MemoryConstants.CompletionContextKeys.UserId, out var userId) == true)
        {
            return userId as string;
        }

        return services?
            .GetService<IHttpContextAccessor>()?
            .HttpContext?
            .User?
            .FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
