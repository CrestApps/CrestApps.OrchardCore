using CrestApps.AI;
using CrestApps.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.Mvc.Web.Controllers;

[Route("api/chat")]
[ApiController]
[Authorize]
public sealed class ChatApiController : ControllerBase
{
    private readonly IAIProfileManager _profileManager;
    private readonly IAIChatSessionManager _sessionManager;

    public ChatApiController(
        IAIProfileManager profileManager,
        IAIChatSessionManager sessionManager)
    {
        _profileManager = profileManager;
        _sessionManager = sessionManager;
    }

    [HttpGet("profiles")]
    public async Task<IActionResult> GetProfiles()
    {
        var profiles = await _profileManager.GetAsync(AIProfileType.Chat);

        var result = profiles
            .Where(p => p.GetSettings<AIProfileSettings>().IsListable)
            .Select(p => new
            {
                id = p.ItemId,
                name = p.Name,
                displayText = p.DisplayText,
                welcomeMessage = p.WelcomeMessage,
            });

        return Ok(result);
    }

    [HttpPost("create-session")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        if (string.IsNullOrEmpty(request?.ProfileId))
        {
            return BadRequest(new { error = "ProfileId is required." });
        }

        var profile = await _profileManager.FindByIdAsync(request.ProfileId);

        if (profile == null)
        {
            return NotFound(new { error = "Profile not found." });
        }

        var session = await _sessionManager.NewAsync(profile, new NewAIChatSessionContext());

        session.Title = profile.DisplayText ?? profile.Name;
        session.UserId = User.Identity?.Name ?? "anonymous";

        await _sessionManager.SaveAsync(session);

        return Ok(new { sessionId = session.SessionId });
    }

    public sealed class CreateSessionRequest
    {
        public string ProfileId { get; set; }
    }
}
