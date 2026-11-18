using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Tests.Agent.AIConfiguration;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Agent.AIQuality;

/// <summary>
/// Builds the services the test case, test run, alert rule and QA cohort tools use, on in-memory stores, with a fixed
/// clock, a conversation to start a test from and the permission rule of the test.
/// </summary>
internal sealed class AIQualityToolHarness
{
    public static readonly DateTime Now = new(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);

    public bool SignedIn { get; set; } = true;

    public Func<Permission, object, bool> Authorize { get; set; } = (_, _) => true;

    public List<string> Checks { get; } = [];

    public InMemoryProfileManager Profiles { get; } = new();

    public List<AITestCase> TestCases { get; } = [];

    public List<AITestRun> Runs { get; } = [];

    public List<AIAlertRule> Rules { get; } = [];

    public List<AIQACohort> Cohorts { get; } = [];

    public List<AIChatSession> Sessions { get; } = [];

    public List<AIChatSessionPrompt> Prompts { get; } = [];

    public int Writes { get; private set; }

    public ServiceProvider Build()
    {
        var services = AgentToolTestContext.CreateServices(SignedIn);
        services.AddSingleton<IAuthorizationService>(new HarnessAuthorizationService(this));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddSingleton<IAIProfileManager>(Profiles);

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(Now);
        services.AddSingleton(clock.Object);

        services.AddSingleton(Catalog<IAITestCaseStore, AITestCase>(TestCases).Object);
        services.AddSingleton(Catalog<IAIAlertRuleStore, AIAlertRule>(Rules).Object);
        services.AddSingleton(Catalog<IAIQACohortStore, AIQACohort>(Cohorts).Object);

        var runStore = Catalog<IAITestRunStore, AITestRun>(Runs);
        runStore.Setup(s => s.ListByBatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string batchId, CancellationToken _) => Runs.Where(run => run.BatchId == batchId).OrderBy(run => run.QueuedUtc).ToList());
        runStore.Setup(s => s.PageAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string testCaseId, int page, int pageSize, CancellationToken _) =>
            {
                var runs = Runs.Where(run => testCaseId is null || run.TestCaseId == testCaseId).OrderByDescending(run => run.QueuedUtc).ToList();

                return ((IReadOnlyList<AITestRun>)runs.Skip((page - 1) * pageSize).Take(pageSize).ToList(), runs.Count);
            });
        services.AddSingleton(runStore.Object);

        var sessionManager = new Mock<IAIChatSessionManager>();
        sessionManager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => Sessions.FirstOrDefault(session => session.SessionId == id));
        services.AddSingleton(sessionManager.Object);

        var promptStore = new Mock<IAIChatSessionPromptStore>();
        promptStore.Setup(s => s.GetPromptsAsync(It.IsAny<string>()))
            .ReturnsAsync((string sessionId) => Prompts.Where(prompt => prompt.SessionId == sessionId).ToList());
        services.AddSingleton(promptStore.Object);

        return services.BuildServiceProvider();
    }

    public void AddConversation(string sessionId, string profileId, params string[] customerLines)
    {
        Sessions.Add(new AIChatSession { SessionId = sessionId, ProfileId = profileId, Title = "Angry customer" });

        foreach (var line in customerLines)
        {
            Prompts.Add(new AIChatSessionPrompt { SessionId = sessionId, Role = ChatRole.User, Content = line });
            Prompts.Add(new AIChatSessionPrompt { SessionId = sessionId, Role = ChatRole.Assistant, Content = "I am sorry to hear that." });
        }
    }

    private Mock<TStore> Catalog<TStore, TEntry>(List<TEntry> entries)
        where TStore : class, ICatalog<TEntry>
        where TEntry : CatalogItem
    {
        var store = new Mock<TStore>();

        store.Setup(s => s.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(entries.FirstOrDefault(entry => entry.ItemId == id)));
        store.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<TEntry>>(entries.ToList()));
        store.Setup(s => s.CreateAsync(It.IsAny<TEntry>(), It.IsAny<CancellationToken>()))
            .Returns((TEntry entry, CancellationToken _) =>
            {
                Writes++;
                entries.Add(entry);

                return ValueTask.CompletedTask;
            });
        store.Setup(s => s.UpdateAsync(It.IsAny<TEntry>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Writes++;

                return ValueTask.CompletedTask;
            });
        store.Setup(s => s.DeleteAsync(It.IsAny<TEntry>(), It.IsAny<CancellationToken>()))
            .Returns((TEntry entry, CancellationToken _) =>
            {
                Writes++;

                return ValueTask.FromResult(entries.Remove(entry));
            });

        return store;
    }

    private sealed class HarnessAuthorizationService : IAuthorizationService
    {
        private readonly AIQualityToolHarness _harness;

        public HarnessAuthorizationService(AIQualityToolHarness harness)
        {
            _harness = harness;
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            foreach (var requirement in requirements.OfType<PermissionRequirement>())
            {
                _harness.Checks.Add(requirement.Permission.Name);

                if (user?.Identity?.IsAuthenticated != true || !_harness.Authorize(requirement.Permission, resource))
                {
                    return Task.FromResult(AuthorizationResult.Failed());
                }
            }

            return Task.FromResult(AuthorizationResult.Success());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }
}
