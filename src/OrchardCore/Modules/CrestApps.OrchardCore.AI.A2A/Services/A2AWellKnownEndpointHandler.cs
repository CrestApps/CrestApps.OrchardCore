using A2A;
using CrestApps.AI.A2A.Models;
using CrestApps.AI.Models;
using CrestApps.AI.Profiles;
using CrestApps.OrchardCore.AI.A2A;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.AI.A2A.Services;

/// <summary>
/// Handles GET requests to <c>/.well-known/agent-card.json</c>.
/// Returns individual agent cards (multi-agent mode) or a single card with skills (skill mode).
/// Includes security scheme information so clients know how to authenticate.
/// </summary>
internal static class A2AWellKnownEndpointHandler
{
    public static async Task HandleAsync(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<A2AHostOptions>>().Value;
        var profileManager = context.RequestServices.GetRequiredService<IAIProfileManager>();
        var profiles = await profileManager.GetAsync(AIProfileType.Agent);
        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
        context.Response.ContentType = "application/json";

        if (options.ExposeAgentsAsSkill)
        {
            var card = A2ATaskManagerFactory.BuildSkillModeCard($"{baseUrl}/a2a", profiles);
            ApplySecuritySchemes(card, options, baseUrl);
            await context.Response.WriteAsJsonAsync(card, A2AJsonOptions.Default, context.RequestAborted);
        }
        else
        {
            var cards = BuildAgentCards(profiles, baseUrl, options);
            await context.Response.WriteAsJsonAsync(cards, A2AJsonOptions.Default, context.RequestAborted);
        }
    }

    private static List<AgentCard> BuildAgentCards(IEnumerable<AIProfile> profiles, string baseUrl, A2AHostOptions options)
    {
        var cards = new List<AgentCard>();

        if (profiles is null)
        {
            return cards;
        }

        foreach (var profile in profiles)
        {
            var agentUrl = $"{baseUrl}/a2a?agent={Uri.EscapeDataString(profile.Name)}";
            var card = A2ATaskManagerFactory.BuildAgentCard(profile, agentUrl);
            ApplySecuritySchemes(card, options, baseUrl);
            cards.Add(card);
        }

        return cards;
    }

    /// <summary>
    /// Populates the <see cref="AgentCard.SecuritySchemes"/> and <see cref="AgentCard.Security"/>
    /// fields based on the configured authentication type so clients know how to authenticate.
    /// </summary>
    private static void ApplySecuritySchemes(AgentCard card, A2AHostOptions options, string baseUrl)
    {
        switch (options.AuthenticationType)
        {
            case A2AHostAuthenticationType.ApiKey:
                card.SecuritySchemes = new Dictionary<string, SecurityScheme>
                {
                    ["apiKey"] = new ApiKeySecurityScheme(
                        name: "Authorization",
                        keyLocation: "header",
                        description: "API key authentication. Send as 'Bearer {key}' or 'ApiKey {key}' in the Authorization header."),
                };

                card.Security =
                [
                    new Dictionary<string, string[]> { ["apiKey"] = [] },
                ];
                break;
            case A2AHostAuthenticationType.OpenId:
                card.SecuritySchemes = new Dictionary<string, SecurityScheme>
                {
                    ["openId"] = new OpenIdConnectSecurityScheme(
                        openIdConnectUrl: new Uri($"{baseUrl}/.well-known/openid-configuration"),
                    description: "OpenID Connect authentication. Obtain an access token from the OpenID Connect provider and send it as a Bearer token."),
                };

                card.Security =
                [
                    new Dictionary<string, string[]> { ["openId"] = [] },
                ];
                break;
                // AuthenticationType.None — no security schemes needed.
        }
    }
}
