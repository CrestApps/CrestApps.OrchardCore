using CrestApps.Core.AI;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Voice;

/// <summary>
/// Registers the provider-neutral automated voice conversation loop. The audio itself comes from whichever
/// telephony provider registers an <c>IVoiceAgentMediaProvider</c>.
/// </summary>
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // Registered as itself as well as behind the interface: when a live session ends, the call is finished
        // in a child scope that resolves a fresh loop of its own, because the scope the session ran in belongs to
        // a webhook request the provider has long since abandoned.
        services.AddScoped<VoiceAgentConversationLoop>();
        services.AddScoped<IVoiceAgentConversationLoop>(serviceProvider => serviceProvider.GetRequiredService<VoiceAgentConversationLoop>());
        services.AddScoped<IRealtimeCallCompletionRunner, RealtimeCallCompletionRunner>();

        // One per call, so the tool and the session holding the line share an instance and two calls running at
        // once cannot end each other.
        services.TryAddScoped<IVoiceCallEndTurn, VoiceCallEndTurn>();

        // The end-call tool is turned on by the call itself rather than by an administrator - every automated
        // call has to be endable - so it is registered without being selectable, like the transfer tool.
        services.AddCoreAITool<EndCallTool>(EndCallTool.ToolName)
            .WithTitle(S["End the call"])
            .WithDescription(S["Lets an automated call hang up once the conversation is over."])
            .WithCategory(S["Omnichannel"]);

        // Speech-to-speech needs a bidirectional media path to the caller's leg, which only the Contact Center
        // Voice Media feature provides. This feature does not depend on it - automated calls run perfectly well
        // on speak and transcribe alone - so the capability is optional: without it, this reports that no
        // realtime session ran and the turn-based loop takes the call.
        services.TryAddScoped<IRealtimeVoiceConversationRunner, NoRealtimeVoiceConversationRunner>();
    }
}

/// <summary>
/// Adds the live speech-to-speech path, which is only possible where something can carry the call's audio both
/// ways.
/// </summary>
/// <remarks>
/// Replaces the reporting no-op the feature registers by default. <c>Replace</c> rather than <c>TryAdd</c>
/// because that default is always registered, and this exists precisely to take over from it whichever startup
/// runs first.
/// </remarks>
[RequireFeatures(ContactCenterConstants.Feature.VoiceMedia)]
public sealed class RealtimeVoiceStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IRealtimeVoiceConversationRunner, RealtimeVoiceConversationRunner>());
    }
}
