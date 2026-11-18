using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals;
using CrestApps.OrchardCore.Approvals.Core.Handlers;
using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Approvals;

/// <summary>
/// Approvals is a building block other modules extend: a module registers an approval type with its handler and its
/// own approver rules, policies are chosen by type and amount and decided in stages, and a policy stored before
/// approver rules were open still loads and decides. The Ticket type here is a sample, as a help desk module would add.
/// </summary>
public sealed class ApprovalTypesTests
{
    [Fact]
    public async Task ATicketType_ChosenByTypeAndAmount_GoesThroughTwoStages_AndItsHooksFire()
    {
        // Arrange: tickets over 500 need the assignee team, then finance; smaller ones the team only.
        var harness = new Harness();

        harness.Policies.Add(new ApprovalPolicy
        {
            ItemId = "small",
            Name = "SmallTickets",
            Types = [TicketApprovalType.Name],
            Order = 20,
            Stages = [new ApprovalStage { Title = "Team", Approvers = [new ApproverRule { Kind = AssigneeTeamApproverRuleProvider.Kind }] }],
        });
        harness.Policies.Add(new ApprovalPolicy
        {
            ItemId = "large",
            Name = "LargeTickets",
            Types = [TicketApprovalType.Name],
            Order = 10,
            MinimumAmount = 500,
            Currency = "USD",
            Stages =
            [
                new ApprovalStage { Title = "Team", Approvers = [new ApproverRule { Kind = AssigneeTeamApproverRuleProvider.Kind }] },
                new ApprovalStage { Title = "Finance", Approvers = [new ApproverRule { Kind = ApproverRuleKinds.Users, Value = "fin-id" }] },
            ],
        });

        // Act: no policy named; the handler fills the title, the team and the type's own data.
        var requested = await harness.Service.RequestAsync(new ApprovalSubmission { SubjectKind = TicketApprovalType.Name, SubjectId = "T-7", Amount = 800, Currency = "USD" }, TestSecurity.User("riley"), TestContext.Current.CancellationToken);
        var request = requested.Request;
        var byFinanceFirst = await harness.Service.DecideAsync(request, TestSecurity.User("fin"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);
        var byTeam = await harness.Service.DecideAsync(request, TestSecurity.User("ana"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);
        var stageAfterTeam = request.CurrentStage;
        var teamAgain = await harness.Service.DecideAsync(request, TestSecurity.User("ana"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);
        var byFinance = await harness.Service.DecideAsync(request, TestSecurity.User("fin"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(requested.Succeeded, string.Join(" ", requested.Errors));
        Assert.Equal("large", request.PolicyId);
        Assert.Equal("Ticket T-7", request.Title);
        Assert.True(request.TryGet<TicketApprovalData>(out var data));
        Assert.Equal("support", data.Team);
        Assert.False(byFinanceFirst.Succeeded);
        Assert.True(byTeam.Succeeded);
        Assert.Equal(1, stageAfterTeam);
        Assert.False(teamAgain.Succeeded);
        Assert.True(byFinance.Succeeded);
        Assert.Equal(ApprovalStatus.Approved, request.Status);
        Assert.Equal([0, 1], request.Decisions.Select(decision => decision.Stage));
        Assert.Equal(["Requesting", "Deciding", "StageApproved", "Deciding", "Approved"], harness.Ticket.Calls);
        Assert.Equal([ApprovalChangeKind.Requested, ApprovalChangeKind.StageApproved, ApprovalChangeKind.Approved], harness.Observer.Changes.Select(change => change.Kind));
    }

    [Fact]
    public async Task ARejection_AtAnyStage_ClosesTheRequest_AndTheBuiltInRulesHoldAtEveryStage()
    {
        // Arrange
        var harness = new Harness();

        harness.Policies.Add(new ApprovalPolicy
        {
            ItemId = "two",
            Name = "TwoStages",
            Stages =
            [
                new ApprovalStage { Approvers = [new ApproverRule { Kind = ApproverRuleKinds.Users, Value = "ana-id" }] },
                new ApprovalStage { Approvers = [new ApproverRule { Kind = ApproverRuleKinds.Users, Value = "riley-id,fin-id" }] },
            ],
        });

        var request = (await harness.Service.RequestAsync(new ApprovalSubmission { Policy = "TwoStages", SubjectKind = TicketApprovalType.Name, SubjectId = "T-8" }, TestSecurity.User("riley"), TestContext.Current.CancellationToken)).Request;

        // Act: the requester is named by stage 2 but still may not approve their own request.
        await harness.Service.DecideAsync(request, TestSecurity.User("ana"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);
        var requesterEligibility = await harness.Service.GetEligibilityAsync(request, TestSecurity.User("riley"), TestContext.Current.CancellationToken);
        var rejected = await harness.Service.DecideAsync(request, TestSecurity.User("fin"), ApprovalDecisionKind.Reject, "Over budget", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(requesterEligibility.CanDecide);
        Assert.True(rejected.Succeeded);
        Assert.Equal(ApprovalStatus.Rejected, request.Status);
        Assert.Contains("Rejected", harness.Ticket.Calls);
    }

    [Fact]
    public async Task TheTypeHandler_RefusesARequestOrADecision()
    {
        // Arrange
        var harness = new Harness();

        harness.Policies.Add(new ApprovalPolicy { ItemId = "any", Name = "Any", Approvers = [new ApproverRule { Kind = ApproverRuleKinds.Users, Value = "ana-id" }] });

        // Act
        var closedTicket = await harness.Service.RequestAsync(new ApprovalSubmission { SubjectKind = TicketApprovalType.Name, SubjectId = "CLOSED" }, TestSecurity.User("riley"), TestContext.Current.CancellationToken);
        var request = (await harness.Service.RequestAsync(new ApprovalSubmission { SubjectKind = TicketApprovalType.Name, SubjectId = "T-9" }, TestSecurity.User("riley"), TestContext.Current.CancellationToken)).Request;

        harness.Ticket.RefuseDecisions = true;

        var decision = await harness.Service.DecideAsync(request, TestSecurity.User("ana"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(closedTicket.Succeeded);
        Assert.Equal(["The ticket is closed."], closedTicket.Errors);
        Assert.False(decision.Succeeded);
        Assert.Equal(["The ticket changed."], decision.Errors);
        Assert.Equal(ApprovalStatus.Pending, request.Status);
        Assert.Empty(request.Decisions);
    }

    [Theory]
    [InlineData(100, "USD", "general")]
    [InlineData(5000, "USD", "large")]
    [InlineData(5000, "EUR", "general")]
    public async Task ThePolicyResolver_PicksTheFirstEnabledMatchingPolicy_ByOrder(int amount, string currency, string expected)
    {
        // Arrange
        var harness = new Harness();

        harness.Policies.Add(new ApprovalPolicy { ItemId = "general", Name = "General", Types = [TicketApprovalType.Name], Order = 50 });
        harness.Policies.Add(new ApprovalPolicy { ItemId = "large", Name = "Large", Types = [TicketApprovalType.Name], Order = 10, MinimumAmount = 1000, Currency = "USD" });
        harness.Policies.Add(new ApprovalPolicy { ItemId = "off", Name = "Off", Types = [TicketApprovalType.Name], Order = 1, Enabled = false });
        harness.Policies.Add(new ApprovalPolicy { ItemId = "payruns", Name = "PayRuns", Types = ["PayRun"], Order = 0 });

        // Act
        var policy = await harness.Resolver.ResolveAsync(new ApprovalSubmission { SubjectKind = TicketApprovalType.Name, Amount = amount, Currency = currency }, TestContext.Current.CancellationToken);
        var named = await harness.Resolver.ResolveAsync(new ApprovalSubmission { SubjectKind = TicketApprovalType.Name, Policy = "Off" }, TestContext.Current.CancellationToken);

        // Assert: a named policy wins, even when it isn't chosen by itself.
        Assert.Equal(expected, policy?.ItemId);
        Assert.Equal("off", named?.ItemId);
    }

    [Fact]
    public async Task NoMatchingPolicy_IsAClearError()
    {
        // Arrange
        var harness = new Harness();

        harness.Policies.Add(new ApprovalPolicy { ItemId = "payruns", Name = "PayRuns", Types = ["PayRun"], Approvers = [new ApproverRule { Kind = ApproverRuleKinds.Users, Value = "ana-id" }] });

        // Act
        var result = await harness.Service.RequestAsync(new ApprovalSubmission { SubjectKind = TicketApprovalType.Name, SubjectId = "T-1" }, TestSecurity.User("riley"), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("No enabled approval policy serves Ticket", Assert.Single(result.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public async Task APolicyStoredBeforeApproverRulesWereOpen_StillLoadsAndDecides()
    {
        // Arrange: a stored policy and a pending request whose rules hold the old numbers (0 Permission ... 2 Manager).
        const string json = """
            {
              "ItemId": "legacy",
              "Name": "Legacy",
              "RequiredApprovals": 1,
              "Approvers": [ { "Kind": 0, "Value": "ApproveTickets" }, { "Kind": 2, "Value": null }, { "Kind": "Users", "Value": "ana-id" } ]
            }
            """;
        var policy = JsonSerializer.Deserialize<ApprovalPolicy>(json);
        var request = JsonSerializer.Deserialize<ApprovalRequest>("""{ "ItemId": "r-legacy", "SubjectKind": "Ticket", "SubjectId": "T-0", "Status": 0, "PolicyId": "legacy", "Approvers": [ { "Kind": 2 } ] }""");
        var harness = new Harness();

        harness.Policies.Add(policy);
        await harness.Store.CreateAsync(request, TestContext.Current.CancellationToken);

        // Act
        var roundTrip = JsonSerializer.Serialize(policy);
        var resolved = await harness.Resolver.ResolveAsync(new ApprovalSubmission { Policy = "Legacy", SubjectKind = "Ticket" }, TestContext.Current.CancellationToken);
        var managerEligibility = await harness.Service.GetEligibilityAsync(request, TestSecurity.User("boss"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([ApproverRuleKinds.Permission, ApproverRuleKinds.Manager, ApproverRuleKinds.Users], policy.Approvers.Select(rule => rule.Kind));
        Assert.Contains("\"Kind\":\"Manager\"", roundTrip, StringComparison.Ordinal);
        Assert.Same(policy, resolved);
        Assert.True(managerEligibility.CanDecide, managerEligibility.Reason);
    }

    [Fact]
    public async Task ThePolicyHandler_ChecksEachStage_AndKeepsARuleWhoseProviderIsOff_AndCapsTheRules()
    {
        // Arrange
        var handler = new ApprovalPolicyHandler(Mock.Of<IClock>(), [new UsersApproverRuleProvider()], RecipeRoundTrip.Localizer<ApprovalPolicyHandler>());
        var policy = new ApprovalPolicy
        {
            Name = "Staged",
            Stages =
            [
                new ApprovalStage { Approvers = [new ApproverRule { Kind = ApproverRuleKinds.Users }] },
                new ApprovalStage { Approvers = [new ApproverRule { Kind = "ProjectLead" }] },
                new ApprovalStage { Approvers = [] },
                new ApprovalStage { Approvers = [.. Enumerable.Range(0, 11).Select(_ => new ApproverRule { Kind = ApproverRuleKinds.Manager })] },
            ],
        };
        var context = new ValidatingContext<ApprovalPolicy>(policy);

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert: users need a value; a provider that is off is kept; a stage needs a rule.
        var messages = context.Result.Errors.Select(error => error.ErrorMessage).ToList();

        Assert.Contains("The approver rule Users at stage 1 needs a value.", messages);
        Assert.DoesNotContain(messages, message => message.Contains("ProjectLead", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.StartsWith("Say who may approve at stage 3", StringComparison.Ordinal));

        // The editor's limit holds for a recipe too: ten rules a stage at most.
        Assert.Contains("Stage 4 has ten approver rules at most.", messages);
    }

    [Fact]
    public void AddApprovalType_RegistersTheTypeAndItsHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddApprovalType<TicketApprovalType>(TicketApprovalType.Name, type => type.DisplayName = LocalizationSource.Create<ApprovalTypesTests>("Ticket"));
        services.AddApproverRuleProvider<AssigneeTeamApproverRuleProvider>();
        services.AddSingleton(new TicketCalls());

        using var provider = services.BuildServiceProvider();
        var catalog = new ApprovalTypeCatalog(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApprovalTypeOptions>>(), provider);

        // Act + Assert
        Assert.Equal([TicketApprovalType.Name], catalog.List().Select(type => type.Name));
        Assert.IsType<TicketApprovalType>(catalog.GetHandler("ticket"));
        Assert.IsAssignableFrom<IApprovalTypeHandler>(catalog.GetHandler("Unknown"));
        Assert.Contains(provider.GetServices<IApproverRuleProvider>(), rule => rule.Name == AssigneeTeamApproverRuleProvider.Kind);
    }

    [Fact]
    public async Task ANamedPolicy_IsRefused_ForATypeItDoesntServe()
    {
        // Arrange
        var harness = new Harness();

        harness.Policies.Add(new ApprovalPolicy { ItemId = "payruns", Name = "PayRunApproval", Types = ["PayRun"], Approvers = [new ApproverRule { Kind = ApproverRuleKinds.Users, Value = "ana-id" }] });

        // Act
        var result = await harness.Service.RequestAsync(new ApprovalSubmission { Policy = "PayRunApproval", SubjectKind = TicketApprovalType.Name, SubjectId = "T-2" }, TestSecurity.User("riley"), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("doesn't serve Ticket", Assert.Single(result.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnePerson_DecidesOneStage_UnlessThePolicyAllowsMore()
    {
        // Arrange: Ana is named by both stages of two policies; one lets her decide both.
        var harness = new Harness();
        ApprovalStage Stage() => new() { Approvers = [new ApproverRule { Kind = ApproverRuleKinds.Users, Value = "ana-id" }] };

        harness.Policies.Add(new ApprovalPolicy { ItemId = "strict", Name = "Strict", Stages = [Stage(), Stage()] });
        harness.Policies.Add(new ApprovalPolicy { ItemId = "small", Name = "SmallTeam", Stages = [Stage(), Stage()], AllowSamePersonAcrossStages = true });

        var strict = (await harness.Service.RequestAsync(new ApprovalSubmission { Policy = "Strict", SubjectKind = TicketApprovalType.Name, SubjectId = "T-3" }, TestSecurity.User("riley"), TestContext.Current.CancellationToken)).Request;
        var small = (await harness.Service.RequestAsync(new ApprovalSubmission { Policy = "SmallTeam", SubjectKind = TicketApprovalType.Name, SubjectId = "T-4" }, TestSecurity.User("riley"), TestContext.Current.CancellationToken)).Request;

        // Act
        await harness.Service.DecideAsync(strict, TestSecurity.User("ana"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);
        var strictAgain = await harness.Service.DecideAsync(strict, TestSecurity.User("ana"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);
        await harness.Service.DecideAsync(small, TestSecurity.User("ana"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);
        var sameStageAgain = await harness.Service.GetEligibilityAsync(small, TestSecurity.User("ana"), TestContext.Current.CancellationToken);
        var smallAgain = await harness.Service.DecideAsync(small, TestSecurity.User("ana"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(strictAgain.Succeeded);
        Assert.Equal(ApprovalStatus.Pending, strict.Status);
        Assert.True(sameStageAgain.CanDecide);
        Assert.True(smallAgain.Succeeded);
        Assert.Equal(ApprovalStatus.Approved, small.Status);
    }

    [Fact]
    public async Task ThePayRunPolicySavedBeforeTypes_IsUpgradedToServePayRunsOnly()
    {
        // Arrange: the policy a tenant seeded before approval types, which serves every type.
        var policy = new ApprovalPolicy { Name = "PayRunApproval", Approvers = [new ApproverRule { Kind = ApproverRuleKinds.Permission, Value = "ApprovePayRuns" }] };
        var policies = new Mock<INamedCatalogManager<ApprovalPolicy>>();

        policies.Setup(manager => manager.FindByNameAsync("PayRunApproval", It.IsAny<CancellationToken>())).ReturnsAsync(policy);

        var seeder = new global::CrestApps.OrchardCore.Payroll.Services.PayRunApprovalPolicySeeder(policies.Object, Mock.Of<global::OrchardCore.Settings.ISiteService>(), RecipeRoundTrip.Localizer<global::CrestApps.OrchardCore.Payroll.Services.PayRunApprovalPolicySeeder>());

        // Act
        await seeder.ServePayRunsOnlyAsync();

        // Assert: a ticket asking with no policy no longer lands on it.
        Assert.Equal("PayRun", policy.Type);
        Assert.False(ApprovalPolicyResolver.Matches(policy, new ApprovalSubmission { SubjectKind = TicketApprovalType.Name }));
        policies.Verify(manager => manager.UpdateAsync(policy, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheResolver_SkipsAPolicyTheTypesOwnConditionRulesOut()
    {
        // Arrange: the first policy by order serves tickets, but the ticket type's own condition rules it out.
        var harness = new Harness();

        harness.Policies.Add(new ApprovalPolicy { ItemId = "vip", Name = "VipQueue", Type = TicketApprovalType.Name, Order = 1 });
        harness.Policies.Add(new ApprovalPolicy { ItemId = "all", Name = "AllTickets", Type = TicketApprovalType.Name, Order = 5 });
        harness.Ticket.UnmatchedPolicies.Add("VipQueue");

        // Act
        var resolved = await harness.Resolver.ResolveAsync(new ApprovalSubmission { SubjectKind = TicketApprovalType.Name }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("all", resolved?.ItemId);
    }

    [Fact]
    public void APolicyOfOneType_ServesOnlyIt_AndAGeneralPolicyServesEveryType()
    {
        // Arrange
        var ticketPolicy = new ApprovalPolicy { Name = "Tickets", Type = TicketApprovalType.Name };
        var general = new ApprovalPolicy { Name = "General" };
        var legacyList = new ApprovalPolicy { Name = "Both", Types = [TicketApprovalType.Name, "PayRun"] };

        // Act and assert
        Assert.True(ticketPolicy.Serves(TicketApprovalType.Name));
        Assert.False(ticketPolicy.Serves("PayRun"));
        Assert.True(general.Serves("PayRun"));
        Assert.True(general.Serves(TicketApprovalType.Name));
        Assert.True(legacyList.Serves("PayRun"));
        Assert.False(legacyList.Serves("Invoice"));
    }

    [Theory]
    [InlineData(new[] { "Ticket" }, "Ticket", 0)]
    [InlineData(new[] { "Ticket", "PayRun" }, null, 2)]
    [InlineData(new string[0], null, 0)]
    public async Task APolicySavedWithAListOfTypes_LoadsAsItsOneType_OrStaysGeneral(string[] types, string expectedType, int expectedListCount)
    {
        // Arrange
        var handler = new ApprovalPolicyHandler(Mock.Of<IClock>(), [new UsersApproverRuleProvider()], RecipeRoundTrip.Localizer<ApprovalPolicyHandler>());
        var policy = new ApprovalPolicy { Name = "Old", Types = [.. types] };

        // Act
        await handler.LoadedAsync(new LoadedContext<ApprovalPolicy>(policy), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedType, policy.Type);
        Assert.Equal(expectedListCount, policy.Types.Count);
    }

    [Fact]
    public void TheConditionsEditor_ShowsOnlyForTypesThatOptIn_AndForGeneralPolicies()
    {
        // Arrange: Ticket opts into the amount only; Note opts into nothing.
        var services = new ServiceCollection();

        services.AddApprovalType<TicketApprovalType>(TicketApprovalType.Name, type => type.Conditions = ApprovalConditions.Amount);
        services.AddApprovalType<TicketApprovalType>("Note");
        services.AddSingleton(new TicketCalls());

        var provider = services.BuildServiceProvider();
        var types = new ApprovalTypeCatalog(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApprovalTypeOptions>>(), provider);
        var driver = new global::CrestApps.OrchardCore.Approvals.Drivers.ApprovalPolicyConditionsDisplayDriver(types, RecipeRoundTrip.Localizer<global::CrestApps.OrchardCore.Approvals.Drivers.ApprovalPolicyConditionsDisplayDriver>());

        // Act and assert
        Assert.True(driver.CanHandleModel(new ApprovalPolicy { Type = TicketApprovalType.Name }));
        Assert.False(driver.CanHandleModel(new ApprovalPolicy { Type = "Note" }));
        Assert.True(driver.CanHandleModel(new ApprovalPolicy()));
    }

    [Fact]
    public async Task ThePoliciesRecipeSchema_AcceptsTheOldNumberedKinds()
    {
        // Arrange
        var schema = await new global::CrestApps.OrchardCore.Approvals.Recipes.ApprovalPoliciesRecipeStepSchema().GetSchemaAsync(TestContext.Current.CancellationToken);
        var step = System.Text.Json.Nodes.JsonNode.Parse("""{ "name": "ApprovalPolicies", "Policies": [ { "Name": "Old", "Approvers": [ { "Kind": 2 }, { "Kind": "Users", "Value": "ana-id" } ] } ] }""");

        // Act
        var result = schema.Evaluate(JsonSerializer.SerializeToElement(step));

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void TheApproverRuleValueEditorModel_IsInTheAbstractions_SoOtherModulesViewsCanUseIt()
        => Assert.Same(typeof(ApprovalRequest).Assembly, typeof(ApproverRuleValueEditorViewModel).Assembly);

    [Fact]
    public void TheRequestPage_HasATemplateForTheDisplayTypeDriversAddFieldsTo()
    {
        // The request page renders the request's DetailAdmin display; without a template every request page fails.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        Assert.True(File.Exists(Path.Combine(directory.FullName, "src", "Modules", "CrestApps.OrchardCore.Approvals", "Views", "ApprovalRequest.DetailAdmin.cshtml")));
    }

    // The service with the real type catalog, directory and resolver, over in-memory stores.
    private sealed class Harness
    {
        public Harness()
        {
            var clock = new Mock<IClock>();

            clock.SetupGet(entry => entry.UtcNow).Returns(new DateTime(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc));

            var services = new ServiceCollection();

            services.AddApprovalType<TicketApprovalType>(TicketApprovalType.Name);
            services.AddSingleton(Ticket);

            var provider = services.BuildServiceProvider();
            var policies = new Mock<INamedCatalogManager<ApprovalPolicy>>();

            policies.Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => Policies.FirstOrDefault(policy => policy.ItemId == id));
            policies.Setup(manager => manager.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string name, CancellationToken _) => Policies.FirstOrDefault(policy => policy.Name == name));
            policies.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Policies);

            var types = new ApprovalTypeCatalog(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApprovalTypeOptions>>(), provider);

            Store = new InMemoryApprovalRequestStore();
            Resolver = new ApprovalPolicyResolver(policies.Object, types);

            var manager = new ApprovalRequestManager(Store, [new ApprovalRequestHandler(clock.Object)], NullLogger<CatalogManager<ApprovalRequest>>.Instance);
            var directory = new ApproverDirectory([new UsersApproverRuleProvider(), new AssigneeTeamApproverRuleProvider(), new FixedManagerApproverRuleProvider()]);

            Service = new ApprovalService(manager, policies.Object, directory, Resolver, types, new CrestApps.OrchardCore.Core.SourceAccess.SourceAccessService([]), [Observer], new KeyedDistributedLock(), new NoopApprovalCommitter(), clock.Object, RecipeRoundTrip.Localizer<ApprovalService>());
        }

        public List<ApprovalPolicy> Policies { get; } = [];

        public InMemoryApprovalRequestStore Store { get; }

        public ApprovalPolicyResolver Resolver { get; }

        public RecordingApprovalObserver Observer { get; } = new();

        public TicketCalls Ticket { get; } = new();

        public ApprovalService Service { get; }
    }

    private sealed class TicketCalls
    {
        public List<string> Calls { get; } = [];

        public bool RefuseDecisions { get; set; }

        public HashSet<string> UnmatchedPolicies { get; } = new(StringComparer.Ordinal);
    }

    private sealed class TicketApprovalData
    {
        public string Team { get; set; }
    }

    // A help desk's approval type: it names the ticket, stores the assignee team, refuses closed tickets, and follows
    // each step.
    private sealed class TicketApprovalType : ApprovalTypeHandlerBase
    {
        public const string Name = "Ticket";

        private readonly TicketCalls _calls;

        public TicketApprovalType(TicketCalls calls)
        {
            _calls = calls;
        }

        public override Task RequestingAsync(ApprovalRequestingContext context, CancellationToken cancellationToken = default)
        {
            _calls.Calls.Add("Requesting");

            if (context.Request.SubjectId == "CLOSED")
            {
                context.Refuse("The ticket is closed.");

                return Task.CompletedTask;
            }

            context.Request.Title ??= "Ticket " + context.Request.SubjectId;
            context.Request.Put(new TicketApprovalData { Team = "support" });

            return Task.CompletedTask;
        }

        public override Task DecidingAsync(ApprovalDecidingContext context, CancellationToken cancellationToken = default)
        {
            _calls.Calls.Add("Deciding");

            if (_calls.RefuseDecisions)
            {
                context.Refuse("The ticket changed.");
            }

            return Task.CompletedTask;
        }

        // The type's own condition, as a help desk's queue would be: a policy it rules out is skipped.
        public override Task<bool> MatchesAsync(ApprovalPolicy policy, ApprovalSubmission submission, CancellationToken cancellationToken = default)
            => Task.FromResult(!_calls.UnmatchedPolicies.Contains(policy.Name));

        public override Task StageApprovedAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
            => Record("StageApproved");

        public override Task ApprovedAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
            => Record("Approved");

        public override Task RejectedAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
            => Record("Rejected");

        private Task Record(string call)
        {
            _calls.Calls.Add(call);

            return Task.CompletedTask;
        }
    }

    // A module's own approver rule: the members of the ticket's assignee team, read from the type's data.
    private sealed class AssigneeTeamApproverRuleProvider : IApproverRuleProvider
    {
        public const string Kind = "AssigneeTeam";

        private static readonly Dictionary<string, string[]> _teams = new(StringComparer.Ordinal) { ["support"] = ["ana-id", "ben-id"] };

        public string Name
            => Kind;

        public LocalizationSource DisplayName { get; } = LocalizationSource.Create<AssigneeTeamApproverRuleProvider>("The ticket's assignee team");

        public bool NeedsValue
            => false;

        public bool DependsOnRequest
            => true;

        public async Task<bool> IsNamedAsync(ApproverRule rule, ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => (await GetUserIdsAsync(rule, request, cancellationToken)).Contains(user.FindFirstValue(ClaimTypes.NameIdentifier));

        public Task<IReadOnlyList<string>> GetUserIdsAsync(ApproverRule rule, ApprovalRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(request.TryGet<TicketApprovalData>(out var data) && data?.Team is { } team ? _teams.GetValueOrDefault(team, []) : []);
    }

    // The manager rule, as Staff would answer it: Boss manages everyone.
    private sealed class FixedManagerApproverRuleProvider : IApproverRuleProvider
    {
        public string Name
            => ApproverRuleKinds.Manager;

        public LocalizationSource DisplayName { get; } = LocalizationSource.Create<FixedManagerApproverRuleProvider>("Manager");

        public bool NeedsValue
            => false;

        public bool DependsOnRequest
            => true;

        public Task<bool> IsNamedAsync(ApproverRule rule, ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult(user.Identity?.Name == "boss");

        public Task<IReadOnlyList<string>> GetUserIdsAsync(ApproverRule rule, ApprovalRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(["boss-id"]);
    }
}
