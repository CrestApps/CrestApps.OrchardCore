using CrestApps.Core.AI.Copilot.Models;
using CrestApps.Core.AI.Copilot.Services;
using CrestApps.OrchardCore.AI.Chat.Copilot.Services;
using CrestApps.OrchardCore.AI.Chat.Copilot.Settings;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Settings;
using USR = OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Endpoints;

internal static class CopilotAuthEndpoints
{
    public static IEndpointRouteBuilder AddCopilotAuthEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("copilot/api/status", HandleStatusAsync)
            .RequireAuthorization();

        builder.MapGet("copilot/api/models", HandleModelsAsync)
            .RequireAuthorization();

        builder.MapPost("copilot/api/disconnect", HandleDisconnectAsync)
            .RequireAuthorization();

        return builder;
    }

    private static async Task<IResult> HandleStatusAsync(
        GitHubOAuthService oauthService,
        UserManager<USR.IUser> userManager,
        ISiteService siteService,
        HttpContext httpContext)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var userId = await userManager.GetUserIdAsync(user);
        var isAuthenticated = await oauthService.IsAuthenticatedAsync(userId);
        var settings = await siteService.GetSettingsAsync<CopilotSettings>();
        string gitHubUsername = null;

        if (isAuthenticated)
        {
            var credential = await oauthService.GetCredentialAsync(userId);
            gitHubUsername = credential?.GitHubUsername;
        }

        return TypedResults.Ok(new
        {
            isAuthenticated,
            gitHubUsername,
            isConfigured = settings.IsConfigured(),
        });
    }

    private static async Task<IResult> HandleModelsAsync(
        GitHubOAuthService oauthService,
        UserManager<USR.IUser> userManager,
        HttpContext httpContext)
    {
        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var userId = await userManager.GetUserIdAsync(user);
        var models = await oauthService.ListModelsAsync(userId);

        return TypedResults.Ok(models.Select(model => new
        {
            model.Id,
            model.Name,
            model.CostMultiplier,
        }));
    }

    private static async Task<IResult> HandleDisconnectAsync(
        GitHubOAuthService oauthService,
        UserManager<USR.IUser> userManager,
        IAntiforgery antiforgery,
        HttpContext httpContext)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return TypedResults.BadRequest();
        }

        var user = await userManager.GetUserAsync(httpContext.User);

        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        await oauthService.DisconnectAsync(await userManager.GetUserIdAsync(user));

        return TypedResults.Ok(new
        {
            success = true,
        });
    }
}
