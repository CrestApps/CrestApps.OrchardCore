using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.RecordTools;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.Records;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.AI;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Drivers;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.TimeTracker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Handlers;
using TicketsAIToolsStartup = CrestApps.OrchardCore.Tickets.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Tickets.AI;

/// <summary>
/// The Tickets module exposes desks, statuses, priorities and saved views to the generic record tools with the permission
/// of the Service Desk settings pages, written through the same editors. The chat may not change a desk's AI settings or
/// make it erase protected information sooner, the ids a page offers only through pickers must exist, and a priority a
/// desk's matrix names is not deleted. The six ticket tools stay registered as they were.
/// </summary>
public sealed class TicketRecordKindsTests
{
    [Fact]
    public void TheStartup_RegistersTheFourKinds_WithTheTicketSettingsPermission_AndKeepsTheTicketTools()
    {
        // Arrange
        using var provider = BuildStartup();

        // Act
        var kinds = provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;
        var tools = provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;

        // Assert
        Assert.Equal(
            [TicketRecordKinds.DeskKind, TicketRecordKinds.PriorityKind, TicketRecordKinds.StatusKind, TicketRecordKinds.ViewKind],
            kinds.Keys.Order(StringComparer.Ordinal));

        foreach (var kind in kinds.Values)
        {
            Assert.Equal(TicketsPermissions.ManageTicketSettings.Name, kind.Permission.Name);
            Assert.Equal(TicketsPermissions.ManageTicketSettings.Name, kind.EffectiveReadPermission.Name);
            Assert.True(kind.CanWrite);
            Assert.False(string.IsNullOrWhiteSpace(kind.Description));
        }

        Assert.Equal(
            ["addTicketReply", "createTicket", "getTicket", "listTicketOptions", "searchTickets", "updateTicket"],
            tools.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task SaveRecord_CreatesAStatus_ThroughTheStatusEditor()
    {
        // Arrange
        var statuses = new InMemoryRecordManager<TicketStatus>();
        using var services = BuildRecordTools(collection =>
        {
            collection.AddScoped<IDisplayDriver<TicketStatus>, TicketStatusDisplayDriver>();
            collection.AddSingleton<ICatalogManager<TicketStatus>>(statuses);
        });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = TicketRecordKinds.StatusKind,
            ["values"] = JsonNode.Parse("""{ "Name": " Waiting on vendor ", "Category": "Pending", "PausesTimers": true, "Order": 35 }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var created = Assert.Single(statuses.Created);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal("Waiting on vendor", created.Name);
        Assert.Equal(TicketStatusCategory.Pending, created.Category);
        Assert.True(created.PausesTimers);
        Assert.Equal("Pending", json.GetProperty("record").GetProperty("category").GetString());
    }

    [Fact]
    public async Task SaveRecord_WithoutTheTicketSettingsPermission_SavesNothing()
    {
        // Arrange
        var statuses = new InMemoryRecordManager<TicketStatus>();
        using var services = BuildRecordTools(
            collection =>
            {
                collection.AddScoped<IDisplayDriver<TicketStatus>, TicketStatusDisplayDriver>();
                collection.AddSingleton<ICatalogManager<TicketStatus>>(statuses);
            },
            authorize: (permission, _) => permission.Name != TicketsPermissions.ManageTicketSettings.Name);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = TicketRecordKinds.StatusKind,
            ["values"] = JsonNode.Parse("""{ "Name": "Waiting" }"""),
        });

        // Assert
        Assert.DoesNotContain("\"saved\":true", result);
        Assert.Empty(statuses.Created);
    }

    [Fact]
    public async Task GetRecord_ShowsADesksSettings_AndOnlyTheNamesOfOtherFeaturesSettings()
    {
        // Arrange
        var desk = new TicketDesk
        {
            ItemId = "support",
            Name = "Support",
            DefaultTeamId = "team-1",
            PriorityMatrix = [new TicketPriorityRule { Impact = TicketLevel.High, Urgency = TicketLevel.High, PriorityId = "urgent" }],
        };
        desk.Put(new TicketDeskAISettings { ProfileId = "profile-1", ApplyPriority = true });
        desk.Put(new TicketDeskTimeSettings { StageId = "stage-1" });
        desk.Properties["TicketDeskQueueSettings"] = new JsonObject { ["QueueId"] = "CANARY-queue" };

        var desks = new InMemoryRecordManager<TicketDesk>();
        desks.Items.Add(desk);

        using var services = BuildRecordTools(collection => collection.AddSingleton<ICatalogManager<TicketDesk>>(desks));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = TicketRecordKinds.DeskKind,
            ["id"] = "support",
        });

        // Assert
        Assert.DoesNotContain("CANARY", result);

        var record = AgentToolTestContext.ReadJson(result).GetProperty("record");

        Assert.Equal("team-1", record.GetProperty("defaultTeamId").GetString());
        Assert.Equal("profile-1", record.GetProperty("ai").GetProperty("profileId").GetString());
        Assert.True(record.GetProperty("ai").GetProperty("applyPriority").GetBoolean());
        Assert.Equal("stage-1", record.GetProperty("time").GetProperty("stageId").GetString());
        Assert.Equal("urgent", Assert.Single(record.GetProperty("priorityMatrix").EnumerateArray()).GetProperty("priorityId").GetString());
        Assert.Equal("TicketDeskQueueSettings", Assert.Single(record.GetProperty("otherSettings").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task ADeskSave_ThatChangesItsAIProfile_IsRefused()
    {
        // Arrange
        var stored = Desk();
        stored.Put(new TicketDeskAISettings { ProfileId = "profile-1" });

        var candidate = Desk();
        candidate.Put(new TicketDeskAISettings { ProfileId = "profile-2" });

        using var services = BuildValidation(stored);

        // Act
        var result = await TicketRecordKinds.ValidateDeskAsync(services, candidate, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("AI settings", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ADeskSave_ThatKeepsItsAISettingsAndChangesOtherFields_IsAllowed()
    {
        // Arrange
        var stored = Desk();
        stored.Put(new TicketDeskAISettings { Disabled = true, ApplyPriority = true });

        var candidate = Desk();
        candidate.Description = "Printers and laptops";
        candidate.Put(new TicketDeskAISettings { Disabled = true });

        using var services = BuildValidation(stored);

        // Act
        var result = await TicketRecordKinds.ValidateDeskAsync(services, candidate, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.ErrorMessage)));
    }

    [Theory]
    [InlineData(0, 30, false)]
    [InlineData(60, 30, false)]
    [InlineData(30, 60, true)]
    [InlineData(30, 0, true)]
    public async Task ADeskSave_NeverErasesProtectedInformationSooner(int storedDays, int newDays, bool allowed)
    {
        // Arrange
        var stored = Desk();
        stored.DeleteProtectedInformationDays = storedDays;

        var candidate = Desk();
        candidate.DeleteProtectedInformationDays = newDays;

        using var services = BuildValidation(stored);

        // Act
        var result = await TicketRecordKinds.ValidateDeskAsync(services, candidate, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(allowed, result.Succeeded);
    }

    [Fact]
    public async Task ANewDesk_ThatErasesProtectedInformation_IsRefused()
    {
        // Arrange
        var candidate = Desk();
        candidate.ItemId = "new-desk";
        candidate.Name = "Billing";
        candidate.DeleteProtectedInformationDays = 30;

        using var services = BuildValidation(Desk());

        // Act
        var result = await TicketRecordKinds.ValidateDeskAsync(services, candidate, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(nameof(TicketDesk.DeleteProtectedInformationDays), Assert.Single(result.Errors.SelectMany(error => error.MemberNames)));
    }

    [Fact]
    public async Task ANewDesk_ThatKeepsProtectedInformation_IsAllowed()
    {
        // Arrange
        var candidate = Desk();
        candidate.ItemId = "new-desk";
        candidate.Name = "Billing";

        using var services = BuildValidation(Desk());

        // Act
        var result = await TicketRecordKinds.ValidateDeskAsync(services, candidate, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.ErrorMessage)));
    }

    [Fact]
    public async Task ADeskSave_RefusesIdsThatDoNotExist()
    {
        // Arrange
        var candidate = Desk();
        candidate.DefaultTeamId = "no-team";
        candidate.HoursScheduleId = "no-hours";
        candidate.ServiceLevelPolicyId = "work-order-policy";
        candidate.TicketTypes = ["Ticket", "Article"];

        using var services = BuildValidation(Desk());

        // Act
        var result = await TicketRecordKinds.ValidateDeskAsync(services, candidate, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [nameof(TicketDesk.DefaultTeamId), nameof(TicketDesk.HoursScheduleId), nameof(TicketDesk.ServiceLevelPolicyId), nameof(TicketDesk.TicketTypes)],
            result.Errors.SelectMany(error => error.MemberNames));
        Assert.Contains("Article", result.Errors[^1].ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADeskSave_AcceptsIdsThatExist_AndKeepsAnUnchangedIdThatNoLongerExists()
    {
        // Arrange
        var stored = Desk();
        stored.DefaultTeamId = "deleted-team";

        var candidate = Desk();
        candidate.DefaultTeamId = "deleted-team";
        candidate.HoursScheduleId = "office";
        candidate.ServiceLevelPolicyId = "ticket-policy";
        candidate.TicketTypes = ["Ticket"];

        using var services = BuildValidation(stored);

        // Act
        var result = await TicketRecordKinds.ValidateDeskAsync(services, candidate, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.ErrorMessage)));
    }

    [Fact]
    public async Task AViewSave_RefusesATeamOrDeskThatDoesNotExist()
    {
        // Arrange
        var views = new InMemoryRecordManager<TicketView>();
        var candidate = new TicketView
        {
            ItemId = "view-1",
            Name = "Breaching soon",
            SharedWithTeamId = "no-team",
            Query = new TicketQuery { DeskIds = ["support", "no-desk"] },
        };

        using var services = BuildValidation(Desk(), collection => collection.AddSingleton<ICatalogManager<TicketView>>(views));

        // Act
        var result = await TicketRecordKinds.ValidateViewAsync(services, candidate, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains("no-team", result.Errors[0].ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("no-desk", result.Errors[1].ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APriorityThatADesksMatrixNames_IsKept_AndAnUnusedOneIsNot()
    {
        // Arrange
        await using var kit = await TicketKit.CreateAsync(
            configureDesk: desk => desk.PriorityMatrix = [new TicketPriorityRule { Impact = TicketLevel.High, Urgency = TicketLevel.High, PriorityId = TicketKit.Urgent.ItemId }],
            configureServices: services => services.AddScoped<TicketSettingsUsage>());

        // Act
        var (urgent, unused) = await kit.ReadAsync(async services => (
            await TicketRecordKinds.GetPriorityUseAsync(services, TicketKit.Urgent, TestContext.Current.CancellationToken),
            await TicketRecordKinds.GetPriorityUseAsync(services, new TicketPriority { ItemId = "low", Name = "Low" }, TestContext.Current.CancellationToken)));

        // Assert
        Assert.Equal("Urgent is still used by the priority matrix of the desk Support, so it can't be deleted.", urgent);
        Assert.Null(unused);
    }

    [Fact]
    public async Task APriorityThatATicketCategoryOrATicketPolicyTargetNames_IsKept()
    {
        // Arrange
        var ticketType = new ContentTypeDefinition(
            TicketsConstants.DefaultContentType,
            TicketsConstants.DefaultContentType,
            [new ContentTypePartDefinition(TicketsConstants.PartName, new ContentPartDefinition(TicketsConstants.PartName, [], []), [])],
            []);
        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.ListTypeDefinitionsAsync()).ReturnsAsync(new[] { ticketType });

        var categories = new Mock<ITicketCategoryService>();
        categories.Setup(service => service.ListAsync(TicketsConstants.DefaultContentType, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TicketCategory { Id = "laptop", Name = "Laptop", PathText = "Hardware > Laptop", PriorityId = TicketKit.Urgent.ItemId },
                new TicketCategory { Id = "printer", Name = "Printer", PathText = "Hardware > Printer", PriorityId = TicketKit.Normal.ItemId },
            });

        await using var kit = await TicketKit.CreateAsync(configureServices: services =>
        {
            services.AddScoped<TicketSettingsUsage>();
            services.AddSingleton(definitions.Object);
            services.AddSingleton(categories.Object);
        });

        await kit.RunAsync(async services =>
        {
            var policies = services.GetRequiredService<IServiceLevelPolicyManager>();
            var policy = await policies.NewAsync(cancellationToken: TestContext.Current.CancellationToken);
            policy.Name = "Urgent support";
            policy.Scope = TicketsConstants.ServiceLevelScope;
            policy.Targets = [new ServiceLevelTarget { Metric = ServiceLevelMetrics.FirstResponse, Priority = TicketKit.Urgent.ItemId, Minutes = 30 }];
            await policies.CreateAsync(policy, TestContext.Current.CancellationToken);

            return policy.ItemId;
        });

        // Act
        var urgent = await kit.ReadAsync(async services => await TicketRecordKinds.GetPriorityUseAsync(services, TicketKit.Urgent, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("Urgent is still used by the ticket category Hardware > Laptop, the targets of the service level policy Urgent support, so it can't be deleted.", urgent);
    }

    private static TicketDesk Desk()
        => new()
        {
            ItemId = "support",
            Name = "Support",
            TicketTypes = ["Ticket"],
        };

    private static ServiceProvider BuildStartup()
    {
        var services = new ServiceCollection();

        new TicketsAIToolsStartup(new RecordTestLocalizer<TicketsAIToolsStartup>()).ConfigureServices(services);

        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildRecordTools(Action<IServiceCollection> configure, Func<global::OrchardCore.Security.Permissions.Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(authorize: authorize);

        services.AddLogging();
        services.AddLocalization();
        services.AddMvcCore().AddDataAnnotations();
        services.AddEditorDisplayManagement();
        services.AddScoped<AIRecordScopeChanges>();

        new TicketsAIToolsStartup(new RecordTestLocalizer<TicketsAIToolsStartup>()).ConfigureServices(services);

        configure(services);

        return services.BuildServiceProvider();
    }

    // The desk "support" as stored; a team, an hours schedule, a policy of each scope and the Ticket content type exist.
    private static ServiceProvider BuildValidation(TicketDesk stored, Action<IServiceCollection> configure = null)
    {
        var desks = new InMemoryRecordManager<TicketDesk>();
        desks.Items.Add(stored);

        var teams = new Mock<ITeamManager>();
        teams.Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => id == "team-1" ? new Team { ItemId = "team-1" } : null);

        var hours = new Mock<IOpenHoursService>();
        hours.Setup(service => service.ListSchedulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new HoursScheduleOption("office", "Office hours") });

        var policies = new Mock<IServiceLevelPolicyManager>();
        policies.Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => id switch
            {
                "ticket-policy" => new ServiceLevelPolicy { ItemId = id, Name = "Standard", Scope = TicketsConstants.ServiceLevelScope },
                "work-order-policy" => new ServiceLevelPolicy { ItemId = id, Name = "Field", Scope = "WorkOrders" },
                _ => null,
            });

        var ticketType = new ContentTypeDefinition(
            TicketsConstants.DefaultContentType,
            TicketsConstants.DefaultContentType,
            [new ContentTypePartDefinition(TicketsConstants.PartName, new ContentPartDefinition(TicketsConstants.PartName, [], []), [])],
            []);
        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.ListTypeDefinitionsAsync()).ReturnsAsync(new[] { ticketType });

        var deskCatalog = new Mock<ICatalog<TicketDesk>>();
        deskCatalog.Setup(catalog => catalog.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { stored });

        var services = new ServiceCollection();

        services.AddSingleton<ICatalogManager<TicketDesk>>(desks);
        services.AddSingleton(deskCatalog.Object);
        services.AddSingleton(teams.Object);
        services.AddSingleton(hours.Object);
        services.AddSingleton(policies.Object);
        services.AddSingleton(definitions.Object);
        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }
}
