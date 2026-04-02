using CrestApps.AI.Chat;
using CrestApps.AI.Models;
using CrestApps.AI.Profiles;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.Mvc.Web.Areas.AIChat.Endpoints;

internal static class CreateChatSessionEndpoint
{
    public static IEndpointRouteBuilder AddCreateChatSessionEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("api/chat/create-session", HandleAsync)
            .RequireAuthorization()
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] CreateChatSessionRequest request,
        HttpContext httpContext,
        IAIProfileManager profileManager,
        IAIChatSessionManager sessionManager)
    {
        if (string.IsNullOrWhiteSpace(request?.ProfileId))
        {
            return TypedResults.BadRequest(new { error = "ProfileId is required." });
        }

        var profile = await profileManager.FindByIdAsync(request.ProfileId);

        if (profile == null)
        {
            return TypedResults.NotFound(new { error = "Profile not found." });
        }

        var session = await sessionManager.NewAsync(profile, new NewAIChatSessionContext());
        session.Title = profile.DisplayText ?? profile.Name;
        session.UserId = httpContext.User.Identity?.Name ?? "anonymous";

        await sessionManager.SaveAsync(session);

        return TypedResults.Ok(new { sessionId = session.SessionId });
    }

    private sealed class CreateChatSessionRequest
    {
        public string ProfileId { get; set; }
    }
}
