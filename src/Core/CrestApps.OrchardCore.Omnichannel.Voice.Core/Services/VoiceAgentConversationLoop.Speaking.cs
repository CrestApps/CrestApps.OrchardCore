using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Liquid;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// How a turn-based call speaks: the opening line rendered from the profile's template, and every line spoken in the
/// voice and language chosen for the call.
/// </summary>
/// <remarks>
/// Split from the loop because its file is at the size the architecture guard allows.
/// </remarks>
public sealed partial class VoiceAgentConversationLoop
{
    // How this scope's call event speaks, chosen on its first line; see GetCallSpeechAsync.
    private CallSpeech _callSpeech;
    private string _callSpeechActivityId;

    // Whether the persona the call speaks for holds it in English, kept apart from _callSpeech: a refused line turns
    // that into an English choice without a persona, and the call is not in English because of it.
    private bool _callHeldInEnglish = true;

    private async Task<string> RenderInitialPromptAsync(OmnichannelActivity activity, AIProfile profile, AIChatSession session, CancellationToken cancellationToken)
    {
        var metadata = profile.GetOrCreate<AIProfileMetadata>();
        var pattern = metadata.InitialPrompt?.Trim();

        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        var contact = string.IsNullOrWhiteSpace(activity.ContactContentItemId)
            ? null
            : await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

        var templateContext = new Dictionary<string, FluidValue>
        {
            ["Activity"] = new ObjectValue(activity),
            ["Profile"] = new ObjectValue(profile),
            ["Session"] = new ObjectValue(session),
        };

        if (contact is not null)
        {
            templateContext["Contact"] = new ObjectValue(contact);
        }

        var rendered = await _liquidTemplateManager.RenderStringAsync(pattern, NullEncoder.Default, templateContext);

        return rendered?.Trim();
    }

    private async Task<bool> SpeakAsync(IVoiceAgentMediaProvider media, string providerCallId, OmnichannelActivity activity, string text, CancellationToken cancellationToken)
    {
        var speech = await GetCallSpeechAsync(media, activity, cancellationToken);
        var spoken = await media.SpeakAsync(providerCallId, text, voice: speech.Voice, language: speech.SpeakLanguage, cancellationToken: cancellationToken);

        if (spoken || !speech.UsesPersona)
        {
            return spoken;
        }

        // The provider refused the persona's voice or language. The persona should cost the call its character, not
        // its voice: the line, and the rest of this call event, is spoken the way it would be without a persona.
        _logger.LogWarning(
            "The provider refused to speak with voice '{Voice}' in '{Language}' on automated call activity '{ActivityId}', which take them from the AI persona; speaking without the persona.",
            speech.Voice.SanitizeLogValue(),
            speech.SpeakLanguage.SanitizeLogValue(),
            activity?.ItemId.SanitizeLogValue());

        _callSpeech = CallSpeech.WithoutPersona(speech.IsPersonaVoice ? media.DefaultVoice : speech.Voice);

        return await media.SpeakAsync(providerCallId, text, voice: _callSpeech.Voice, language: _callSpeech.SpeakLanguage, cancellationToken: cancellationToken);
    }

    private async Task<string> GetListenLanguageAsync(IVoiceAgentMediaProvider media, OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        var speech = await GetCallSpeechAsync(media, activity, cancellationToken);

        return speech.ListenLanguage;
    }

    private async Task<CallSpeech> GetCallSpeechAsync(IVoiceAgentMediaProvider media, OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        // Chosen once per call event: every line of one event is spoken the same way, and the persona is not looked
        // up again for each of them.
        if (_callSpeech is not null && string.Equals(_callSpeechActivityId, activity?.ItemId, StringComparison.Ordinal))
        {
            return _callSpeech;
        }

        var flowSettings = string.IsNullOrWhiteSpace(activity?.SubjectContentType)
            ? null
            : await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(activity.SubjectContentType, cancellationToken);
        var profileId = string.IsNullOrWhiteSpace(activity?.AIProfileId)
            ? flowSettings?.ProfileId
            : activity.AIProfileId;
        var profile = string.IsNullOrWhiteSpace(profileId)
            ? null
            : await _profileManager.FindByIdAsync(profileId, cancellationToken);

        _callSpeech = await SelectCallSpeechAsync(ShellScope.Services, activity, flowSettings, profile, media.DefaultVoice, cancellationToken);
        _callSpeechActivityId = activity?.ItemId;
        _callHeldInEnglish = _callSpeech.IsEnglish;

        return _callSpeech;
    }

    /// <summary>
    /// Chooses how a turn-based call speaks and listens.
    /// </summary>
    /// <remarks>
    /// The voice is the first of the activity's, the subject flow's, the AI persona's and the provider's default. The
    /// persona's voice is used only when it was chosen on a deployment of the same provider as the call's
    /// text-to-speech deployment (the activity's, the subject flow's, then the site default). The persona's language,
    /// when it has one, is what the call speaks and listens in. Without the AI Personas feature there is no persona.
    /// </remarks>
    /// <param name="services">The tenant's services, or <see langword="null"/> outside a shell scope.</param>
    /// <param name="activity">The call's activity.</param>
    /// <param name="flowSettings">The activity subject's flow settings, if any.</param>
    /// <param name="profile">The AI profile the call speaks for, if any.</param>
    /// <param name="providerDefaultVoice">The telephony provider's own default voice.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    internal static async Task<CallSpeech> SelectCallSpeechAsync(
        IServiceProvider services,
        OmnichannelActivity activity,
        SubjectFlowSettings flowSettings,
        AIProfile profile,
        string providerDefaultVoice,
        CancellationToken cancellationToken)
    {
        var persona = services is null || profile is null
            ? null
            : await services.ResolvePersonaAsync(profile, cancellationToken);
        var language = FirstNonEmpty(persona?.Language);
        var explicitVoice = FirstNonEmpty(activity?.TextToSpeechVoiceId, flowSettings?.TextToSpeechVoiceId);

        if (explicitVoice is not null)
        {
            return new CallSpeech(explicitVoice, language);
        }

        if (!string.IsNullOrWhiteSpace(persona?.VoiceName))
        {
            var speakingDeploymentName = FirstNonEmpty(activity?.TextToSpeechDeploymentName, flowSettings?.TextToSpeechDeploymentName)
                ?? await GetSiteTextToSpeechDeploymentNameAsync(services);
            var personaVoice = await services.SelectPersonaVoiceAsync(persona, explicitVoice: null, speakingDeploymentName, defaultVoice: null, cancellationToken);

            if (!string.IsNullOrWhiteSpace(personaVoice))
            {
                return new CallSpeech(personaVoice.Trim(), language, IsPersonaVoice: true);
            }
        }

        return new CallSpeech(providerDefaultVoice, language);
    }

    private static async Task<string> GetSiteTextToSpeechDeploymentNameAsync(IServiceProvider services)
    {
        var siteService = services.GetService<ISiteService>();

        if (siteService is null)
        {
            return null;
        }

        var settings = await siteService.GetSettingsAsync<DefaultAIDeploymentSettings>();

        return FirstNonEmpty(settings.DefaultTextToSpeechDeploymentName);
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    /// <summary>
    /// How a turn-based call speaks and listens.
    /// </summary>
    /// <param name="Voice">The voice the provider speaks with.</param>
    /// <param name="PersonaLanguage">The AI persona's BCP-47 language, or <see langword="null"/> when it has none.</param>
    /// <param name="IsPersonaVoice">Whether <paramref name="Voice"/> is the AI persona's.</param>
    internal sealed record CallSpeech(string Voice, string PersonaLanguage, bool IsPersonaVoice = false)
    {
        /// <summary>
        /// The language the provider speaks in when the persona names none.
        /// </summary>
        public const string DefaultSpeakLanguage = "en-US";

        /// <summary>
        /// The language transcription listens for when the persona names none.
        /// </summary>
        public const string DefaultListenLanguage = "en";

        /// <summary>
        /// Gets the language the provider speaks in: the persona's, otherwise <see cref="DefaultSpeakLanguage"/>.
        /// </summary>
        public string SpeakLanguage => PersonaLanguage ?? DefaultSpeakLanguage;

        /// <summary>
        /// Gets the language transcription listens for: the language part of the persona's tag (<c>es</c> for
        /// <c>es-MX</c>), otherwise <see cref="DefaultListenLanguage"/>.
        /// </summary>
        public string ListenLanguage => PersonaLanguage is null
            ? DefaultListenLanguage
            : PersonaLanguage.Split('-', '_')[0].ToLowerInvariant();

        /// <summary>
        /// Gets a value indicating whether the call is held in English: the persona names no language, or an English one.
        /// </summary>
        public bool IsEnglish => string.Equals(ListenLanguage, DefaultListenLanguage, StringComparison.Ordinal);

        /// <summary>
        /// Gets a value indicating whether anything about how the call speaks came from the persona.
        /// </summary>
        public bool UsesPersona => IsPersonaVoice || PersonaLanguage is not null;

        /// <summary>
        /// Creates a choice that speaks with a voice in the default language, taking nothing from a persona.
        /// </summary>
        /// <param name="voice">The voice.</param>
        public static CallSpeech WithoutPersona(string voice) => new(voice, PersonaLanguage: null);
    }
}
