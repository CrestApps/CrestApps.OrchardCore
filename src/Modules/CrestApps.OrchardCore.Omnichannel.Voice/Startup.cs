using CrestApps.Core.ContactCenter;
using CrestApps.Core.AI;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using CrestApps.OrchardCore.Omnichannel.Voice.Core;
using CrestApps.Core.Telephony.Services;

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
        services.AddCoreHostSeams();

        services.AddCoreOmnichannelAutomatedVoice();

        // The end-call tool is turned on by the call itself rather than by an administrator - every automated
        // call has to be endable - so it is registered without being selectable, like the transfer tool.
        services.AddCoreAITool<EndCallTool>(EndCallTool.ToolName)
            .WithTitle(S["End the call"])
            .WithDescription(S["Lets an automated call hang up once the conversation is over."])
            .WithCategory(S["Omnichannel"]);
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
