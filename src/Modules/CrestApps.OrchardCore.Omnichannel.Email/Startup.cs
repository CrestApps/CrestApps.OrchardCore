using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Email.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Email.Drivers;
using CrestApps.OrchardCore.Omnichannel.Email.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Email;

/// <summary>
/// Automates the email channel: the AI sends an activity's opening email, answers the customer's replies, recovers a
/// reply lost to a restart, follows up on the campaign's cadence, and an email entry point can route a customer's first
/// email to an AI agent.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAutomatedMessagingChannel<EmailAutomatedMessagingChannel>();

        // Asked before any automated email goes out, so a stop said on any record that holds the address is honoured.
        services.TryAddScoped<IContactOptOutResolver, ContactOptOutResolver>();

        services.AddSingleton<IBackgroundTask, AutomatedConversationRecoveryBackgroundTask>();
        services.AddSingleton<IBackgroundTask, AutomatedConversationFollowUpBackgroundTask>();

        // An email entry point can route to an AI agent, which takes the customer's first email itself.
        services.AddEntryPointAIAgentChannel(OmnichannelConstants.Channels.Email);
        services.AddDisplayDriver<ContactCenterEntryPoint, EmailEntryPointAIAgentDisplayDriver>();
    }
}
