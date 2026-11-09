using CrestApps.Core.AI.Completions;
using CrestApps.OrchardCore.AI.Workflows.Flows;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// The AI workflow activities on a real tenant, with and without AI chat.
/// </summary>
/// <remarks>
/// Every workflow trigger, the one Orchard Core raises at sign-in included, builds the activity library, which
/// creates every registered activity. The AI module registered activities that need the chat session manager for
/// every tenant with Workflows, but only the AI Chat Services feature registers that manager. So on a tenant with
/// AI and Workflows and no chat, the workflow editor and the sign-in failed.
/// </remarks>
public sealed class AIWorkflowsFeatureActivationTests
{
    // AI Services is enabled by dependency only. Connection Management depends on it and not on chat.
    private const string AIConnectionManagement = "CrestApps.OrchardCore.AI.ConnectionManagement";
    private const string AIChat = "CrestApps.OrchardCore.AI.Chat";
    private const string Workflows = "OrchardCore.Workflows";

    [Fact]
    public async Task WithoutChat_TheActivityLibraryBuilds_WithoutTheChatSessionActivities()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(Profile("ai-workflows-without-chat", AIConnectionManagement, Workflows));

        await host.ExecuteInTenantScopeAsync(tenant, services =>
        {
            var activityNames = ListActivityNames(services);

            Assert.Contains(nameof(AICompletionFromProfileTask), activityNames);
            Assert.Contains(nameof(SplitTrafficTask), activityNames);
            Assert.DoesNotContain(nameof(AIConversationStageTask), activityNames);
            Assert.DoesNotContain(nameof(SetConversationVariablesTask), activityNames);
            Assert.DoesNotContain(nameof(UpdateContactMemoryTask), activityNames);

            // Built for every completion; it used to need the conversation flows, and so the chat session manager.
            Assert.DoesNotContain(
                services.GetServices<IAICompletionContextBuilderHandler>(),
                handler => handler is AIConversationFlowCompletionHandler);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task WithChat_TheActivityLibraryBuilds_WithTheChatSessionActivities()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(Profile("ai-workflows-with-chat", AIChat, Workflows));

        await host.ExecuteInTenantScopeAsync(tenant, services =>
        {
            var activityNames = ListActivityNames(services);

            Assert.Contains(nameof(AICompletionFromProfileTask), activityNames);
            Assert.Contains(nameof(AIConversationFlowStartedEvent), activityNames);
            Assert.Contains(nameof(AIConversationStageTask), activityNames);
            Assert.Contains(nameof(SetConversationVariablesTask), activityNames);
            Assert.Contains(nameof(EvaluateConversationTask), activityNames);
            Assert.Contains(nameof(SimulateConversationTask), activityNames);
            Assert.Contains(nameof(RerunConversationAnalysisTask), activityNames);
            Assert.Contains(nameof(UpdateContactMemoryTask), activityNames);
            Assert.Single(
                services.GetServices<IAICompletionContextBuilderHandler>(),
                handler => handler is AIConversationFlowCompletionHandler);

            return Task.CompletedTask;
        });
    }

    private static string[] ListActivityNames(IServiceProvider services)
        => services.GetRequiredService<IActivityLibrary>()
            .ListActivities()
            .Select(activity => activity.Name)
            .ToArray();

    private static ContactCenterTenantProfile Profile(string id, params string[] features)
        => new()
        {
            Id = id,
            ProviderProfile = "none",
            Features = features,
        };
}
