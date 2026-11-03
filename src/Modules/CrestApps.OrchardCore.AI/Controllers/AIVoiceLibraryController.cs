using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Speech;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.AI.Controllers;

/// <summary>
/// The voices a text-to-speech deployment's provider offers: search them, hear a sample, copy a voice's id to a
/// profile or persona, and clone a voice from a recording where the provider allows it.
/// </summary>
[Admin]
public sealed class AIVoiceLibraryController : Controller
{
    private const long MaxSampleBytes = 10 * 1024 * 1024;

    private readonly IAIDeploymentCapabilityService _capabilityService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IVoiceLibraryResolver _libraryResolver;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceLibraryController"/> class.
    /// </summary>
    /// <param name="capabilityService">Lists the text-to-speech deployments.</param>
    /// <param name="deploymentManager">Finds a deployment.</param>
    /// <param name="libraryResolver">Finds a deployment's voice library.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public AIVoiceLibraryController(
        IAIDeploymentCapabilityService capabilityService,
        IAIDeploymentManager deploymentManager,
        IVoiceLibraryResolver libraryResolver,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<AIVoiceLibraryController> htmlLocalizer)
    {
        _capabilityService = capabilityService;
        _deploymentManager = deploymentManager;
        _libraryResolver = libraryResolver;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Searches a deployment's voices.
    /// </summary>
    /// <param name="model">The deployment and the search.</param>
    /// <returns>The voices.</returns>
    [Admin("ai/voices", "AIVoiceLibraryIndex")]
    public async Task<IActionResult> Index(AIVoiceLibraryViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDeployments))
        {
            return Forbid();
        }

        model ??= new AIVoiceLibraryViewModel();
        var deployments = await _capabilityService.GetDeploymentsWithFeatureAsync(AIDeploymentFeatureNames.TextToSpeech);
        model.Deployments = deployments.Select(deployment => new SelectListItem(deployment.Name, deployment.Name, deployment.Name == model.DeploymentName)).ToList();
        model.DeploymentName ??= deployments.Count > 0 ? deployments[0].Name : null;

        var library = await GetLibraryAsync(model.DeploymentName);
        model.HasLibrary = library is not null;
        model.SupportsCloning = library?.SupportsCloning == true;

        if (library is not null)
        {
            try
            {
                var result = await library.SearchAsync(new VoiceSearchRequest { Query = model.Query, Language = model.Language, Cursor = model.Cursor, PageSize = 30 }, HttpContext.RequestAborted);
                model.Voices = new AdminListContext<SpeechVoice> { Source = result.Voices };
                model.NextCursor = result.NextCursor;
            }
            catch (HttpRequestException ex)
            {
                await _notifier.ErrorAsync(H["The provider did not list its voices: {0}", ex.Message]);
            }
        }

        model.Voices ??= new AdminListContext<SpeechVoice> { Source = [] };

        return View(model);
    }

    /// <summary>
    /// Clones a voice from a recording.
    /// </summary>
    /// <param name="deploymentName">The deployment whose provider clones it.</param>
    /// <param name="name">The new voice's name.</param>
    /// <param name="language">The voice's language.</param>
    /// <param name="sample">The recording.</param>
    /// <returns>A redirect to the voices.</returns>
    [HttpPost]
    [Admin("ai/voices/clone", "AIVoiceLibraryClone")]
    public async Task<IActionResult> Clone(string deploymentName, string name, string language, IFormFile sample)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDeployments))
        {
            return Forbid();
        }

        var library = await GetLibraryAsync(deploymentName);

        if (library?.SupportsCloning != true || string.IsNullOrWhiteSpace(name) || sample is null || sample.Length == 0 || sample.Length > MaxSampleBytes)
        {
            await _notifier.ErrorAsync(H["Give the voice a name and a recording of up to 10 MB, on a provider that clones voices."]);

            return RedirectToAction(nameof(Index), new { deploymentName });
        }

        using var memory = new MemoryStream();
        await sample.CopyToAsync(memory, HttpContext.RequestAborted);

        try
        {
            var voice = await library.CloneAsync(new VoiceCloneRequest
            {
                Name = name.Trim(),
                Language = string.IsNullOrWhiteSpace(language) ? null : language.Trim(),
                Samples = [new VoiceCloneSample { FileName = Path.GetFileName(sample.FileName), ContentType = sample.ContentType, Content = memory.ToArray() }],
            }, HttpContext.RequestAborted);

            await _notifier.SuccessAsync(H["The voice '{0}' was cloned. Its id is {1}; choose it as a profile's or persona's voice.", voice.Name, voice.Id]);
        }
        catch (HttpRequestException ex)
        {
            await _notifier.ErrorAsync(H["The provider did not clone the voice: {0}", ex.Message]);
        }

        return RedirectToAction(nameof(Index), new { deploymentName, query = name });
    }

    /// <summary>
    /// Deletes a cloned voice.
    /// </summary>
    /// <param name="deploymentName">The deployment whose provider holds it.</param>
    /// <param name="voiceId">The voice.</param>
    /// <returns>A redirect to the voices.</returns>
    [HttpPost]
    [Admin("ai/voices/delete", "AIVoiceLibraryDelete")]
    public async Task<IActionResult> Delete(string deploymentName, string voiceId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIDeployments))
        {
            return Forbid();
        }

        var library = await GetLibraryAsync(deploymentName);

        if (library is not null && !string.IsNullOrWhiteSpace(voiceId))
        {
            try
            {
                await library.DeleteAsync(voiceId, HttpContext.RequestAborted);
                await _notifier.SuccessAsync(H["The voice was deleted."]);
            }
            catch (HttpRequestException ex)
            {
                await _notifier.ErrorAsync(H["The provider did not delete the voice: {0}", ex.Message]);
            }
        }

        return RedirectToAction(nameof(Index), new { deploymentName });
    }

    private async Task<IVoiceLibrary> GetLibraryAsync(string deploymentName)
    {
        if (string.IsNullOrWhiteSpace(deploymentName))
        {
            return null;
        }

        var deployment = await _deploymentManager.FindByNameAsync(deploymentName, HttpContext.RequestAborted);

        return deployment is null ? null : await _libraryResolver.GetLibraryAsync(deployment, HttpContext.RequestAborted);
    }
}
