using Microsoft.AspNetCore.Routing;

namespace CrestApps.Mvc.Web.Endpoints.Chat;

internal static class ChatApiEndpoints
{
    public static IEndpointRouteBuilder AddChatApiEndpoints(this IEndpointRouteBuilder builder)
    {
        return builder
            .AddGetChatProfilesEndpoint()
            .AddCreateChatSessionEndpoint()
            .AddGetChatSessionEndpoint();
    }
}
