using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Fluid;
using Fluid.Values;
using OrchardCore.ContentManagement;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// How a turn-based call speaks: the opening line rendered from the profile's template, and every line spoken in the
/// voice the activity was loaded with.
/// </summary>
/// <remarks>
/// Split from the loop because its file is at the size the architecture guard allows.
/// </remarks>
public sealed partial class VoiceAgentConversationLoop
{
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

    private static Task<bool> SpeakAsync(IVoiceAgentMediaProvider media, string providerCallId, OmnichannelActivity activity, string text, CancellationToken cancellationToken)
    {
        // Honor the configured neural text-to-speech voice for a natural (non-robotic) delivery, falling back to
        // the provider's own default. The value must be a voice the provider supports for its speak command.
        var voice = string.IsNullOrWhiteSpace(activity?.TextToSpeechVoiceId)
            ? media.DefaultVoice
            : activity.TextToSpeechVoiceId.Trim();

        return media.SpeakAsync(providerCallId, text, voice: voice, language: "en-US", cancellationToken: cancellationToken);
    }
}
