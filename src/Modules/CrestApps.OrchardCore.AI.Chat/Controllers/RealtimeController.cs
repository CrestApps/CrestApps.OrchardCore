using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Speech;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.OrchardCore.AI.Chat.Controllers;

/// <summary>
/// Serves the realtime (speech-to-speech) voice list consumed by the chat UI when a realtime-capable
/// deployment is selected, to signed-in users who may use AI profiles (the profile, persona and chat interaction
/// editors), via <c>~/CrestApps.OrchardCore.AI.Chat/Realtime/Voices</c>.
/// </summary>
[Authorize]
public sealed class RealtimeController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIDeploymentCapabilityService _capabilityService;
    private readonly IRealtimeVoiceResolver _realtimeVoiceResolver;

    public RealtimeController(
        IAuthorizationService authorizationService,
        IAIDeploymentManager deploymentManager,
        IAIDeploymentCapabilityService capabilityService,
        IRealtimeVoiceResolver realtimeVoiceResolver)
    {
        _authorizationService = authorizationService;
        _deploymentManager = deploymentManager;
        _capabilityService = capabilityService;
        _realtimeVoiceResolver = realtimeVoiceResolver;
    }

    public async Task<IActionResult> Voices(string deploymentName)
    {
        // The list names the site's deployments and their voices, so only someone who may use AI profiles, or chat
        // interactions (whose page shows the same list), sees it.
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.QueryAnyAIProfile) &&
            !await _authorizationService.AuthorizeAsync(User, AIPermissions.EditOwnChatInteractions))
        {
            return Forbid();
        }

        // An empty deployment name means "use the site's default realtime deployment"; resolve it so the
        // voice list still populates for that default selection.
        var deployment = string.IsNullOrWhiteSpace(deploymentName)
            ? await _capabilityService.ResolveDeploymentWithFeatureAsync(AIDeploymentFeatureNames.Realtime)
            : await _deploymentManager.FindByNameAsync(deploymentName);

        // Only expose voices for a deployment whose model declares the realtime capability.
        if (deployment is null || !_capabilityService.GetCapabilities(deployment).SupportsFeature(AIDeploymentFeatureNames.Realtime))
        {
            return Json(new { voices = Array.Empty<object>() });
        }

        var voices = (await _realtimeVoiceResolver.GetVoicesAsync(deployment))
            .OrderBy(voice => voice.Name, StringComparer.OrdinalIgnoreCase)
            .Select(voice => new
            {
                voice.Id,
                voice.Name,
                Gender = voice.Gender.ToString(),
            });

        return Json(new { voices });
    }
}
