using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Voice.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
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
        services.AddContactMethods();
        services.AddScoped<VoiceAgentConversationLoop>();
        services.AddScoped<IVoiceAgentConversationLoop>(serviceProvider => serviceProvider.GetRequiredService<VoiceAgentConversationLoop>());
        services.AddScoped<IRealtimeCallCompletionRunner, RealtimeCallCompletionRunner>();
        services.AddScoped<ITurnBasedSilenceWatchdog, TurnBasedSilenceWatchdog>();

        // A live conversation runs in this process, so when the node begins to stop each one is ended and its
        // caller handed on, rather than left on a line that falls silent. A singleton, so the registration on the
        // host's stopping token goes with the shell; activation is what makes it. Its tenant-events side is a
        // singleton too: a scope disposes what a scoped factory returns, so the activation scope would dispose the
        // signal every answered call reads.
        services.AddSingleton<AIVoiceCallShutdown>();
        services.AddSingleton<IAIVoiceCallShutdown>(serviceProvider => serviceProvider.GetRequiredService<AIVoiceCallShutdown>());
        services.AddSingleton<IModularTenantEvents>(serviceProvider => serviceProvider.GetRequiredService<AIVoiceCallShutdown>());

        // One per call, so the tool and the session holding the line share an instance and two calls running at
        // once cannot end each other.
        services.TryAddScoped<IVoiceCallEndTurn, VoiceCallEndTurn>();

        // The provider's word that a machine answered reaches the node on a webhook request of its own, while a live
        // session holds the call in another. A tenant singleton is what both requests can see; it holds the live
        // calls only, and forgets each as it ends.
        services.AddSingleton<IAnsweringMachineSignals, AnsweringMachineSignals>();

        // A supervisor's request reaches the live session holding an AI call through this registry, a singleton
        // because the session and the request run in different scopes.
        services.AddSingleton<IAIVoiceCallCommands, AIVoiceCallCommands>();
        services.AddScoped<IAIVoiceCallSupervision, AIVoiceCallSupervision>();

        // The AI usage report's per-call summaries. The tracker outlives requests, because a turn-based call's
        // measurements span many of them; the store is the analytics feature's, and records nothing without it.
        services.AddSingleton<IAIVoiceSessionTracker, AIVoiceSessionTracker>();
        services.AddScoped<AIVoiceSessionSummaryWriter>();
        services.TryAddScoped<IAIVoiceSessionSummaryStore, NullAIVoiceSessionSummaryStore>();

        // The end-call tool is turned on by the call itself rather than by an administrator - every automated
        // call has to be endable - so it is registered without being selectable, like the transfer tool.
        services.AddCoreAITool<EndCallTool>(EndCallTool.ToolName)
            .WithTitle(S["End the call"])
            .WithDescription(S["Lets an automated call hang up once the conversation is over."])
            .WithCategory(S["Omnichannel"]);

        // Pressing keys is part of every automated call too: the AI may reach a phone menu on the other end.
        services.AddCoreAITool<PressKeysTool>(PressKeysTool.ToolName)
            .WithTitle(S["Press keys"])
            .WithDescription(S["Lets an automated call press keys to get through a phone menu."])
            .WithCategory(S["Omnichannel"]);

        // Speech-to-speech needs a bidirectional media path to the caller's leg, which only the Contact Center
        // Voice Media feature provides. This feature does not depend on it - automated calls run perfectly well
        // on speak and transcribe alone - so the capability is optional: without it, this reports that no
        // realtime session ran and the turn-based loop takes the call.
        services.TryAddScoped<IRealtimeVoiceConversationRunner, NoRealtimeVoiceConversationRunner>();

        // A call whose hangup was never reported is concluded here, or it stays in progress for good.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, StrandedVoiceCallRecoveryBackgroundTask>());
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
