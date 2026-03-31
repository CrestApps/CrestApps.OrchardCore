using CrestApps.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.Mvc.Web.Endpoints.Chat;

internal static class GetChatSessionEndpoint
{
    public static IEndpointRouteBuilder AddGetChatSessionEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapGet("api/chat/session", HandleAsync)
            .RequireAuthorization();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        string sessionId,
        IAIChatSessionManager sessionManager,
        IAIChatSessionPromptStore promptStore)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return TypedResults.BadRequest();
        }

        var session = await sessionManager.FindAsync(sessionId);
        if (session == null)
        {
            return TypedResults.NotFound();
        }

        var prompts = await promptStore.GetPromptsAsync(session.SessionId);

        return TypedResults.Ok(new
        {
            sessionId = session.SessionId,
            profile = new
            {
                id = session.ProfileId,
            },
            messages = prompts.Select(message => new
            {
                id = message.ItemId,
                role = message.Role.Value,
                content = message.Content,
                isGeneratedPrompt = message.IsGeneratedPrompt,
            }),
        });
    }
}
