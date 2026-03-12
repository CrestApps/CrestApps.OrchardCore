using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.AI.Controllers;

[Admin("ai/api/{action}", "AIApi{action}")]
public sealed class ApiController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _clientFactory;

    public ApiController(
        IAuthorizationService authorizationService,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory clientFactory)
    {
        _authorizationService = authorizationService;
        _deploymentManager = deploymentManager;
        _clientFactory = clientFactory;
    }

    [HttpGet]
    public async Task<IActionResult> Voices(string deploymentId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(deploymentId))
        {
            return Json(new { voices = Array.Empty<object>() });
        }

        var deployment = await _deploymentManager.FindByIdAsync(deploymentId);

        if (deployment is null)
        {
            return Json(new { voices = Array.Empty<object>() });
        }

        try
        {
            using var client = await _clientFactory.CreateTextToSpeechClientAsync(deployment);
            var voices = await client.GetVoicesAsync();

            return Json(new
            {
                voices = voices
                    .OrderBy(v => v.Language)
                    .ThenBy(v => v.Name)
                    .Select(v => new
                    {
                        v.Id,
                        v.Name,
                        v.Language,
                        Gender = v.Gender.ToString(),
                    }),
            });
        }
        catch
        {
            return Json(new { voices = Array.Empty<object>() });
        }
    }
}
