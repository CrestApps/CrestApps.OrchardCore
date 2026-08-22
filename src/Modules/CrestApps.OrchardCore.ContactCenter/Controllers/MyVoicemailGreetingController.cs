using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Telephony;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Lets a signed-in agent manage their own voicemail greeting: record one with the microphone, upload an audio
/// file, set a spoken (text-to-speech) greeting, or clear it back to the queue/system default. An audio greeting
/// is uploaded to the telephony provider's media storage (via <see cref="IVoicemailGreetingMediaProvisioner"/>) so
/// it can be played back to callers without the platform hosting a public URL.
/// </summary>
[Admin]
[RequireFeatures(
    ContactCenterConstants.Feature.Agents,
    ContactCenterConstants.Feature.Voice)]
public sealed class MyVoicemailGreetingController : Controller
{
    // Reject oversized uploads early: Telnyx Media Storage caps media at 20 MB, and a greeting is a few seconds of
    // speech, so anything approaching this is not a greeting.
    private const long MaxGreetingBytes = 20L * 1024 * 1024;

    private static readonly string[] _allowedContentTypes =
    [
        "audio/mpeg",
        "audio/mp3",
        "audio/wav",
        "audio/x-wav",
        "audio/wave",
        "audio/webm",
        "audio/ogg",
    ];

    private readonly IAgentProfileManager _agentProfileManager;
    private readonly INotifier _notifier;
    private readonly IHtmlLocalizer H;
    private readonly IStringLocalizer S;

    public MyVoicemailGreetingController(
        IAgentProfileManager agentProfileManager,
        INotifier notifier,
        IHtmlLocalizer<MyVoicemailGreetingController> htmlLocalizer,
        IStringLocalizer<MyVoicemailGreetingController> stringLocalizer)
    {
        _agentProfileManager = agentProfileManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("contact-center/my-voicemail-greeting", "ContactCenterMyVoicemailGreeting")]
    public async Task<IActionResult> Index()
    {
        var agent = await GetCurrentAgentAsync();

        if (agent is null)
        {
            return Forbid();
        }

        return View(new MyVoicemailGreetingViewModel
        {
            HasAudioGreeting = !string.IsNullOrWhiteSpace(agent.VoicemailGreetingMediaName) ||
                !string.IsNullOrWhiteSpace(agent.VoicemailGreetingMediaUrl),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAudio(IFormFile audio)
    {
        var agent = await GetCurrentAgentAsync();

        if (agent is null)
        {
            return Forbid();
        }

        if (audio is null || audio.Length == 0)
        {
            return BadRequest(S["No audio was provided."].Value);
        }

        if (audio.Length > MaxGreetingBytes)
        {
            return BadRequest(S["The greeting is too large. It must be 20 MB or less."].Value);
        }

        if (!_allowedContentTypes.Contains(audio.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(S["Unsupported audio format."].Value);
        }

        var provisioner = HttpContext.RequestServices.GetService<IVoicemailGreetingMediaProvisioner>();

        if (provisioner is null)
        {
            return BadRequest(S["Audio greetings are not supported by the configured telephony provider."].Value);
        }

        string mediaName;

        await using (var stream = audio.OpenReadStream())
        {
            mediaName = await provisioner.UploadAsync(stream, audio.ContentType, HttpContext.RequestAborted);
        }

        if (string.IsNullOrWhiteSpace(mediaName))
        {
            return StatusCode(StatusCodes.Status502BadGateway, S["The greeting could not be uploaded. Please try again."].Value);
        }

        // Swap in the new greeting, then best-effort delete the previous provider media so it does not linger.
        var previousMediaName = agent.VoicemailGreetingMediaName;
        agent.VoicemailGreetingMediaName = mediaName;
        agent.VoicemailGreetingMediaUrl = null;

        await _agentProfileManager.UpdateAsync(agent);

        if (!string.IsNullOrWhiteSpace(previousMediaName) && !string.Equals(previousMediaName, mediaName, StringComparison.Ordinal))
        {
            await provisioner.DeleteAsync(previousMediaName, HttpContext.RequestAborted);
        }

        await _notifier.SuccessAsync(H["Your voicemail greeting was saved."]);

        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear()
    {
        var agent = await GetCurrentAgentAsync();

        if (agent is null)
        {
            return Forbid();
        }

        var previousMediaName = agent.VoicemailGreetingMediaName;

        agent.VoicemailGreetingMediaName = null;
        agent.VoicemailGreetingMediaUrl = null;

        await _agentProfileManager.UpdateAsync(agent);

        if (!string.IsNullOrWhiteSpace(previousMediaName))
        {
            var provisioner = HttpContext.RequestServices.GetService<IVoicemailGreetingMediaProvisioner>();

            if (provisioner is not null)
            {
                await provisioner.DeleteAsync(previousMediaName, HttpContext.RequestAborted);
            }
        }

        await _notifier.SuccessAsync(H["Your recorded greeting was removed. Callers will hear the default greeting."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task<AgentProfile> GetCurrentAgentAsync()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return string.IsNullOrEmpty(userId)
            ? null
            : await _agentProfileManager.FindByUserIdAsync(userId, HttpContext.RequestAborted);
    }
}
