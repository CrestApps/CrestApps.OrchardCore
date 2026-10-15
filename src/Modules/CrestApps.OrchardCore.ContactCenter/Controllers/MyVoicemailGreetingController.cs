using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Lets a signed-in agent manage their own voicemail greeting: record one with the microphone, upload an audio
/// file, set a spoken (text-to-speech) greeting, or clear it back to the queue/system default. An audio greeting
/// is kept as a master copy in the platform's media store and sent to the telephony provider (via
/// <see cref="IVoiceMediaProvisioningService"/>), so it is played to callers without the platform hosting a public
/// URL and keeps playing after the provider changes.
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

    // Labels the provider's copy of the greeting by purpose so provider media storage stays identifiable.
    private const string GreetingMediaNamePrefix = "cc-vm-greeting";

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
    private readonly IVoiceMediaProvisioningService _voiceMediaProvisioning;
    private readonly IAuthorizationService _authorizationService;
    private readonly IClock _clock;
    private readonly INotifier _notifier;
    private readonly IHtmlLocalizer H;
    private readonly IStringLocalizer S;

    public MyVoicemailGreetingController(
        IAgentProfileManager agentProfileManager,
        IVoiceMediaProvisioningService voiceMediaProvisioning,
        IAuthorizationService authorizationService,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<MyVoicemailGreetingController> htmlLocalizer,
        IStringLocalizer<MyVoicemailGreetingController> stringLocalizer)
    {
        _agentProfileManager = agentProfileManager;
        _voiceMediaProvisioning = voiceMediaProvisioning;
        _authorizationService = authorizationService;
        _clock = clock;
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

        var hasMasterCopy = agent.VoicemailGreetingMedia is not null;

        return View(new MyVoicemailGreetingViewModel
        {
            HasAudioGreeting = hasMasterCopy ||
                !string.IsNullOrWhiteSpace(agent.VoicemailGreetingMediaName) ||
                !string.IsNullOrWhiteSpace(agent.VoicemailGreetingMediaUrl),
            IsProviderCopyOnly = !hasMasterCopy && !string.IsNullOrWhiteSpace(agent.VoicemailGreetingMediaName),
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

        if (!_voiceMediaProvisioning.CanUpload)
        {
            return BadRequest(S["Audio greetings are unavailable because no media storage and no telephony provider capable of hosting media is configured."].Value);
        }

        var media = new VoiceMediaAsset();
        VoiceMediaStoreResult result;

        await using (var stream = audio.OpenReadStream())
        {
            result = await _voiceMediaProvisioning.StoreAsync(media, stream, audio.ContentType, VoiceMediaUploadFormat.Resolve(audio), GreetingMediaNamePrefix, HttpContext.RequestAborted);
        }

        if (!result.Succeeded)
        {
            return StatusCode(StatusCodes.Status502BadGateway, S["The greeting could not be uploaded. Please try again."].Value);
        }

        // Swap in the new greeting, then best-effort delete the copies it replaces so they do not linger. The
        // provider's copy name follows the copy the active provider was just given, for readers of that member.
        var replaced = CaptureGreetingCopies(agent, result.ProviderReference);

        agent.VoicemailGreetingMedia = media;
        agent.VoicemailGreetingMediaName = result.ProviderReference;
        agent.VoicemailGreetingMediaUrl = null;

        await _agentProfileManager.UpdateAsync(agent);

        if (replaced is not null)
        {
            await _voiceMediaProvisioning.DeleteEverywhereAsync(replaced, HttpContext.RequestAborted);
        }

        if (result.ProviderUploadFailed)
        {
            await _notifier.WarningAsync(H["Your voicemail greeting was saved, but {0} did not accept a copy yet. It is sent to the provider when a caller next reaches your voicemail.", result.ProviderName]);
        }
        else
        {
            await _notifier.SuccessAsync(H["Your voicemail greeting was saved."]);
        }

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

        var removed = CaptureGreetingCopies(agent, keepReference: null);

        agent.VoicemailGreetingMedia = null;
        agent.VoicemailGreetingMediaName = null;
        agent.VoicemailGreetingMediaUrl = null;

        await _agentProfileManager.UpdateAsync(agent);

        if (removed is not null)
        {
            await _voiceMediaProvisioning.DeleteEverywhereAsync(removed, HttpContext.RequestAborted);
        }

        await _notifier.SuccessAsync(H["Your recorded greeting was removed. Callers will hear the default greeting."]);

        return RedirectToAction(nameof(Index));
    }

    // Every copy the agent's current greeting is held as: its master copy and provider copies, and the copy a
    // greeting recorded before master copies were kept left at the provider uploads go to. Null when there is none.
    private VoiceMediaAsset CaptureGreetingCopies(AgentProfile agent, string keepReference)
    {
        var current = agent.VoicemailGreetingMedia;

        var copies = new VoiceMediaAsset
        {
            MasterStorageReference = current?.MasterStorageReference,
            ContentType = current?.ContentType,
            Format = current?.Format,
            ProviderReferences = current?.ProviderReferences is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(current.ProviderReferences),
        };

        var legacyName = agent.VoicemailGreetingMediaName;
        var activeProvider = _voiceMediaProvisioning.ActiveProviderName;

        if (!string.IsNullOrWhiteSpace(legacyName) &&
            !string.Equals(legacyName, keepReference, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(activeProvider) &&
            !copies.ProviderReferences.ContainsValue(legacyName))
        {
            copies.ProviderReferences.TryAdd(activeProvider, legacyName);
        }

        if (string.IsNullOrWhiteSpace(copies.MasterStorageReference) && copies.ProviderReferences.Count == 0)
        {
            return null;
        }

        return copies;
    }

    // The signed-in user's own agent profile, or null when they may not work as an agent. Every action goes through
    // here, so the permission is checked before the profile is read. Having a profile was the only check once: a user
    // whose agent role was taken away, but whose profile remained, could still upload and set the greeting callers hear.
    // A user the role does allow, but who has no profile yet, gets one: the menu offers them this page, and refusing it
    // sent a new agent to the access-denied page before they had ever been onboarded or opened the messaging workspace.
    private async Task<AgentProfile> GetCurrentAgentAsync()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return null;
        }

        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var agent = await _agentProfileManager.FindByUserIdAsync(userId, HttpContext.RequestAborted);

        if (agent is not null)
        {
            return agent;
        }

        var userName = User.Identity?.Name;

        agent = await _agentProfileManager.NewAsync();
        agent.UserId = userId;
        agent.UserName = userName;
        agent.DisplayName = userName;
        agent.Name = userId;
        agent.CreatedUtc = _clock.UtcNow;

        await _agentProfileManager.CreateAsync(agent);

        return agent;
    }
}
