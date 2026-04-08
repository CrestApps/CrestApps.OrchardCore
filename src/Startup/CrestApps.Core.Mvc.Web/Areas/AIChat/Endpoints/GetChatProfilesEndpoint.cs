using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Endpoints;

internal static class GetChatProfilesEndpoint
{
    public static IEndpointRouteBuilder AddGetChatProfilesEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapGet("api/chat/profiles", HandleAsync)
            .RequireAuthorization();

        return builder;
    }

    private static async Task<IResult> HandleAsync(IAIProfileManager profileManager)
    {
        var profiles = await profileManager.GetAsync(AIProfileType.Chat);

        return TypedResults.Ok(profiles
            .Where(profile => profile.GetSettings<AIProfileSettings>().IsListable)
            .Select(profile => new
            {
                id = profile.ItemId,
                name = profile.Name,
                displayText = profile.DisplayText,
                welcomeMessage = profile.WelcomeMessage,
            }));
    }
}
