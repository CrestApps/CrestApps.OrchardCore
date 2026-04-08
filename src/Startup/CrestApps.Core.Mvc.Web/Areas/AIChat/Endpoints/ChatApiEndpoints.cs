namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Endpoints;

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
