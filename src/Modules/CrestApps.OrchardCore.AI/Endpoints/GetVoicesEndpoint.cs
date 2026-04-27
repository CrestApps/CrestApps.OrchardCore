using System.Globalization;
using System.Text.Json;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Services;
using CrestApps.Core.AI.Speech;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Endpoints;

internal static class GetVoicesEndpoint
{
    /// <summary>
    /// Adds the get voices endpoint.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public static IEndpointRouteBuilder AddGetVoicesEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapGet("ai/api/voices", HandleAsync)
            .WithName(AIConstants.RouteNames.GetVoices)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        [FromQuery] string deploymentName,
        [FromQuery] string deploymentId,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] IAIDeploymentManager deploymentManager,
        [FromServices] ISpeechVoiceResolver speechVoiceResolver,
        [FromServices] ILocalizationService localizationService,
        [FromServices] ILogger<Startup> logger)
    {
        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return TypedResults.Forbid();
        }

        var deploymentSelector = string.IsNullOrWhiteSpace(deploymentName) ? deploymentId : deploymentName;

        if (string.IsNullOrWhiteSpace(deploymentSelector))
        {
            return TypedResults.Ok(new { voices = Array.Empty<object>() });
        }

        var deployment = !string.IsNullOrWhiteSpace(deploymentName)
        ? await deploymentManager.FindByNameAsync(deploymentSelector)
        : await deploymentManager.FindByIdAsync(deploymentSelector)
        ?? await deploymentManager.FindByNameAsync(deploymentSelector);

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
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve speech voices for deployment '{DeploymentName}'.", deploymentSelector);

            return TypedResults.Ok(new { voices = Array.Empty<object>() });
        }
    }
}
