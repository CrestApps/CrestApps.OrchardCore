using System.Security.Claims;
using CrestApps.OrchardCore.AI.Playwright.Models;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.OrchardCore.AI.Playwright.Controllers;

/// <summary>
/// Lightweight REST surface used by the admin widget JS to query and control Playwright sessions.
/// All endpoints require the user to be authenticated.
/// </summary>
[ApiController]
[Authorize]
[Route("api/playwright")]
public sealed class PlaywrightSessionApiController : ControllerBase
{
    private readonly IPlaywrightSessionManager _sessionManager;

    public PlaywrightSessionApiController(IPlaywrightSessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    [HttpGet("{chatSessionId}/status")]
    public IActionResult GetStatus(string chatSessionId)
    {
        var session = _sessionManager.GetSession(chatSessionId, GetOwnerId());

        return session == null
            ? Ok(PlaywrightStatusResponse.Inactive(chatSessionId))
            : Ok(PlaywrightStatusResponse.FromSession(chatSessionId, session));
    }

    [HttpGet("active")]
    public IActionResult GetActiveSessions()
    {
        var sessions = _sessionManager.GetActiveSessions(GetOwnerId());
        var result = sessions.Select(kvp => PlaywrightStatusResponse.FromSession(kvp.Key, kvp.Value));

        return Ok(result);
    }

    [HttpPost("{chatSessionId}/stop")]
    public IActionResult Stop(string chatSessionId)
    {
        _sessionManager.Stop(chatSessionId, GetOwnerId());

        var session = _sessionManager.GetSession(chatSessionId, GetOwnerId());

        return session == null
            ? Ok(PlaywrightStatusResponse.Inactive(chatSessionId))
            : Ok(PlaywrightStatusResponse.FromSession(chatSessionId, session));
    }

    [HttpPost("{chatSessionId}/close")]
    public async Task<IActionResult> Close(string chatSessionId)
    {
        await _sessionManager.CloseAsync(chatSessionId, GetOwnerId());

        return Ok(PlaywrightStatusResponse.Inactive(chatSessionId));
    }

    private string GetOwnerId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name;
}
