using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Endpoints;

internal static class GetVoicesEndpoint
{
    public static IEndpointRouteBuilder AddGetVoicesEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapGet("ai/api/voices", HandleAsync)
            .AllowAnonymous()
            .WithName(AIConstants.RouteNames.GetVoices)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        [FromQuery] string deploymentId,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] IAIDeploymentManager deploymentManager,
        [FromServices] ISpeechVoiceResolver speechVoiceResolver,
        [FromServices] ILocalizationService localizationService)
    {
        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return TypedResults.Forbid();
        }

        if (string.IsNullOrWhiteSpace(deploymentId))
        {
            return TypedResults.Ok(new { voices = Array.Empty<object>() });
        }

        var deployment = await deploymentManager.FindByIdAsync(deploymentId);

        if (deployment is null)
        {
            return TypedResults.Ok(new { voices = Array.Empty<object>() });
        }

        try
        {
            var allVoices = await speechVoiceResolver.GetSpeechVoicesAsync(deployment);

            var supportedCultures = await localizationService.GetSupportedCulturesAsync();
            var supportedSet = SpeechVoiceLocalizationHelper.CreateAllowedCultures(
                supportedCultures,
                CultureInfo.CurrentCulture,
                CultureInfo.CurrentUICulture);

            var voices = allVoices
                .Where(v => SpeechVoiceLocalizationHelper.IsLanguageAllowed(v.Language, supportedSet))
                .OrderBy(v => v.Language)
                .ThenBy(v => v.Name)
                .Select(v => new
                {
                    v.Id,
                    v.Name,
                    v.Language,
                    LanguageDisplayName = SpeechVoiceLocalizationHelper.GetCultureDisplayName(v.Language),
                    Gender = v.Gender.ToString(),
                });

            return TypedResults.Json(new { voices }, JOptions.CamelCase);
        }
        catch
        {
            return TypedResults.Ok(new { voices = Array.Empty<object>() });
        }
    }
}
