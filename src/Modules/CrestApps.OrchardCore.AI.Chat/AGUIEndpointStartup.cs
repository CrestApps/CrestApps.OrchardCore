using CrestApps.Core.AI.Chat.AGUI;
using CrestApps.OrchardCore.AI.Chat.Core.Hubs;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.AI.Chat;

/// <summary>
/// Maps the AG-UI endpoint, through which any AG-UI client runs the AI chat profiles over HTTP and Server-Sent
/// Events, when the AI Chat API is on.
/// </summary>
[RequireFeatures(AIConstants.Feature.ChatApi)]
public sealed class AGUIEndpointStartup : StartupBase
{
    /// <summary>
    /// The route of the AG-UI endpoint, next to the other AI APIs.
    /// </summary>
    public const string RoutePattern = "api/ai/v1/agui";

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // The hub authorizes the profile and the session; the endpoint needs an authenticated API caller (an AI API key
        // or a bearer token), and answers others with 401. Like the completion routes, it is the chat area of the API, so
        // a key or token scoped to other areas is refused, and every caller is held to the completion quota. It reads
        // JSON only, which a cross-site page cannot send without the browser asking first, so it needs no antiforgery
        // token. The chat UI and the widgets use the hub.
        routes.MapAGUIChat<AIChatHub, CrestApps.OrchardCore.AI.Chat.Hubs.IAIChatHubClient>(RoutePattern)
            .AddEndpointFilter(RequireChatScopeAsync)
            .AddEndpointFilter<IEndpointConventionBuilder, AICompletionQuotaFilter>()
            .WithRateLimitGroup(AICompletionQuotaFilter.RateLimitGroup)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = AIApiKeys.ApiSchemes });
    }

    /// <summary>
    /// Refuses a caller whose key or token may not use the chat area of the AI API.
    /// </summary>
    /// <param name="context">The endpoint invocation.</param>
    /// <param name="next">The endpoint.</param>
    /// <returns>The endpoint's result, or 403.</returns>
    internal static async ValueTask<object> RequireChatScopeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!AIApiKeys.AllowsScope(context.HttpContext.User, AIApiKeys.Scopes.Chat))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}
