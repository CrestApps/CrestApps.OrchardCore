using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Security;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Agreements.Core.Indexes;
using CrestApps.OrchardCore.Agreements.Core.Services;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.AI;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Integrations;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// The work order and service agreement AI tools work only for a signed-in person with the admin page's permission, run
/// the pages' services and editor rules, and leave a record unchanged when they refuse.
/// </summary>
public sealed class WorkOrderAIToolTests
{
    private static readonly string _view = WorkOrdersPermissions.ViewWorkOrders.Name;
    private static readonly string _manage = WorkOrdersPermissions.ManageWorkOrders.Name;
    private static readonly string _settings = WorkOrdersPermissions.ManageWorkOrderSettings.Name;
    private static readonly string _agreements = AgreementsPermissions.ManageAgreements.Name;
    private static readonly string _viewAgreements = AgreementsPermissions.ViewAgreements.Name;
    private static readonly string _serviceLevels = ServiceLevelsPermissions.ManageServiceLevelPolicies.Name;

    public static TheoryData<string> ToolNames
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var name in WorkOrderAIToolRegistrationTests.NewTools.Select(tool => tool.Name))
            {
                data.Add(name);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesSomeoneWhoIsNotSignedIn_OrLacksThePagesPermission_AndChangesNothing(string toolName)
    {
        // Arrange
        await using var setup = await SetupAsync();
        var workOrder = await CreateWorkOrderAsync(setup);
        var permission = WorkOrderAIToolRegistrationTests.NewTools.Single(tool => tool.Name == toolName).Permission;
        var arguments = new Dictionary<string, object>
        {
            ["number"] = workOrder.Number,
            ["summary"] = "Changed",
            ["status"] = "OnHold",
            ["action"] = "Cancel",
            ["reason"] = "Changed",
            ["kind"] = "types",
            ["id"] = setup.TypeId,
            ["name"] = "Changed",
        };

        // Manage work order settings implies Manage work orders, which implies View work orders; agreements are the shared
        // Agreements feature's and stand apart.
        var below = permission == _agreements || permission == _viewAgreements
            ? _settings
            : permission == _settings ? _manage : permission == _manage ? _view : "AccessAdminPanel";

        // Act
        var anonymous = await InvokeAsync(setup, new ClaimsPrincipal(new ClaimsIdentity()), toolName, arguments);
        var anonymousWithPermission = await InvokeAsync(setup, new ClaimsPrincipal(new ClaimsIdentity([new Claim(global::OrchardCore.Security.Permissions.Permission.ClaimType, _settings), new Claim(global::OrchardCore.Security.Permissions.Permission.ClaimType, _agreements)])), toolName, arguments);
        var lacking = await InvokeAsync(setup, CalendarTestHarness.User("john", below), toolName, arguments);
        var stored = await FindAsync(setup, workOrder.ItemId);
        var type = await setup.Harness.RunAsync(services => services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>().FindByIdAsync(setup.TypeId).AsTask(), commit: false);

        // Assert
        Assert.Equal(WorkOrderToolBase.SignInRequired, anonymous);
        Assert.Equal(WorkOrderToolBase.SignInRequired, anonymousWithPermission);
        Assert.Equal($"The user isn't allowed to do this: it needs the '{permission}' permission.", lacking);
        Assert.Equal("No heat", stored.Summary);
        Assert.Equal(WorkOrderStatus.New, stored.Status);
        Assert.Equal("Boiler service", type.Name);
    }

    [Fact]
    public async Task Tools_ReadTheCallerFromTheUserAccessor_WhenThereIsNoRequest()
    {
        // Arrange
        await using var setup = await SetupAsync();
        var workOrder = await CreateWorkOrderAsync(setup);

        // Act
        var result = await setup.Harness.RunAsync(async services =>
        {
            services.GetRequiredService<TestUserAccessor>().User = CalendarTestHarness.User("viewer", _view);

            return await new ListWorkOrdersTool().InvokeAsync(new AIFunctionArguments { Services = services }, TestContext.Current.CancellationToken);
        });

        // Assert
        Assert.Equal(workOrder.Number, Parse(result).GetProperty("items")[0].GetProperty("number").GetString());
    }

    [Fact]
    public async Task ListWorkOrders_ListsTheOpenOnes_InTheLocalTimeZone_AndFiltersByPriorityAndSearch()
    {
        // Arrange
        await using var setup = await SetupAsync();
        var first = await CreateWorkOrderAsync(setup);
        var second = await CreateWorkOrderAsync(setup, "Leaking valve", WorkOrderPriority.Urgent);
        var viewer = CalendarTestHarness.User("viewer", _view);

        await InvokeAsync(setup, CalendarTestHarness.User("dispatcher", _manage), CloseWorkOrderTool.ToolName, new() { ["number"] = first.Number, ["action"] = "Cancel", ["reason"] = "Duplicate" });

        // Act
        var open = Parse(await InvokeAsync(setup, viewer, ListWorkOrdersTool.ToolName, []));
        var all = Parse(await InvokeAsync(setup, viewer, ListWorkOrdersTool.ToolName, new() { ["status"] = "all" }));
        var urgent = Parse(await InvokeAsync(setup, viewer, ListWorkOrdersTool.ToolName, new() { ["status"] = "all", ["priority"] = "urgent" }));
        var search = Parse(await InvokeAsync(setup, viewer, ListWorkOrdersTool.ToolName, new() { ["status"] = "all", ["search"] = "valve" }));
        var badStatus = await InvokeAsync(setup, viewer, ListWorkOrdersTool.ToolName, new() { ["status"] = "Pending" });

        // Assert
        Assert.Equal(1, open.GetProperty("total").GetInt32());
        Assert.Equal(second.Number, open.GetProperty("items")[0].GetProperty("number").GetString());
        Assert.Equal("America/New_York", open.GetProperty("timeZoneId").GetString());
        Assert.Equal("2026-11-02T07:00-05:00", open.GetProperty("items")[0].GetProperty("created").GetString());
        Assert.Equal("Boiler service", open.GetProperty("items")[0].GetProperty("type").GetString());
        Assert.Equal(2, all.GetProperty("total").GetInt32());
        Assert.Equal(second.Number, Assert.Single(urgent.GetProperty("items").EnumerateArray()).GetProperty("number").GetString());
        Assert.Equal(second.Number, Assert.Single(search.GetProperty("items").EnumerateArray()).GetProperty("number").GetString());
        Assert.StartsWith("The status must be", Assert.IsType<string>(badStatus), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateWorkOrder_AppliesTheEditorsRules_AddsANote_AndRefusesAnUnknownLineWithoutChangingAnything()
    {
        // Arrange
        await using var setup = await SetupAsync();
        var workOrder = await CreateWorkOrderAsync(setup);
        var dispatcher = CalendarTestHarness.User("dispatcher", _manage);
        var filter = workOrder.Lines.Single(line => line.Description == "Filter");

        // Act
        var updated = Parse(await InvokeAsync(setup, dispatcher, UpdateWorkOrderTool.ToolName, new()
        {
            ["number"] = workOrder.Number,
            ["summary"] = "No heat upstairs",
            ["priority"] = "High",
            ["serviceAddress"] = new Dictionary<string, object> { ["addressLine1"] = "12 Elm St", ["city"] = "Springfield" },
            ["earliest"] = "2026-11-04T09:00",
            ["lines"] = new object[]
            {
                new Dictionary<string, object> { ["id"] = filter.Id, ["quantity"] = 2 },
                new Dictionary<string, object> { ["kind"] = "Expense", ["description"] = "Parking", ["unitPrice"] = 8 },
            },
            ["note"] = "Customer called back.",
        }));
        var noteOnly = Parse(await InvokeAsync(setup, dispatcher, UpdateWorkOrderTool.ToolName, new() { ["number"] = workOrder.Number, ["note"] = "Gate code is 1234." }));
        var unknownLine = await InvokeAsync(setup, dispatcher, UpdateWorkOrderTool.ToolName, new()
        {
            ["number"] = workOrder.Number,
            ["summary"] = "Should not stay",
            ["lines"] = new object[] { new Dictionary<string, object> { ["id"] = "ghost", ["kind"] = "Labor", ["description"] = "X" } },
        });
        var badTime = await InvokeAsync(setup, dispatcher, UpdateWorkOrderTool.ToolName, new() { ["number"] = workOrder.Number, ["earliest"] = "Thursday morning" });
        var missing = await InvokeAsync(setup, dispatcher, UpdateWorkOrderTool.ToolName, new() { ["number"] = "WO-999999", ["summary"] = "X" });
        var stored = await FindAsync(setup, workOrder.ItemId);

        // Assert
        Assert.True(updated.GetProperty("updated").GetBoolean());
        Assert.True(updated.GetProperty("noteAdded").GetBoolean());
        Assert.True(noteOnly.GetProperty("noteAdded").GetBoolean());
        Assert.False(noteOnly.GetProperty("updated").GetBoolean());
        Assert.StartsWith("The work order has no line with the ID 'ghost'", Assert.IsType<string>(unknownLine), StringComparison.Ordinal);
        Assert.StartsWith("The earliest time must be written", Assert.IsType<string>(badTime), StringComparison.Ordinal);
        Assert.Equal(WorkOrderToolSupport.WorkOrderNotFound, missing);
        Assert.Equal("No heat upstairs", stored.Summary);
        Assert.Equal(WorkOrderPriority.High, stored.Priority);
        Assert.Equal("12 Elm St", stored.ServiceAddress.AddressLine1);
        Assert.Equal(new DateTime(2026, 11, 4, 14, 0, 0, DateTimeKind.Utc), stored.EarliestUtc);
        Assert.Equal(["Filter", "Parking"], stored.Lines.Select(line => line.Description));
        Assert.Equal(filter.Id, stored.Lines[0].Id);
        Assert.Equal(2m, stored.Lines[0].QuantityEstimated);

        // A line given by its id keeps what was not given: its kind, price and stock item.
        Assert.Equal(WorkOrderLineKind.Product, stored.Lines[0].Kind);
        Assert.Equal(15m, stored.Lines[0].UnitPrice);
        Assert.Equal("filter-item", stored.Lines[0].StockItemId);

        // The notes are internal entries on the work order's timeline.
        var notes = await setup.Harness.RunAsync(
            async services => (await services.GetRequiredService<ITimelineService>().ListAsync(new TimelineQuery { Subject = WorkOrderServiceLevels.SubjectOf(stored), IncludeInternal = true }, TestContext.Current.CancellationToken)).Entries,
            commit: false);

        Assert.Contains(notes, note => note.Body == "Customer called back." && note.AuthorName == "dispatcher");
        Assert.Contains(notes, note => note.Body == "Gate code is 1234.");
    }

    [Fact]
    public async Task SetAndCloseWorkOrder_FollowTheStatusRules_TakeASubStatusByName_AndNeedAReasonToCancel()
    {
        // Arrange
        await using var setup = await SetupAsync();
        var workOrder = await CreateWorkOrderAsync(setup);
        var dispatcher = CalendarTestHarness.User("dispatcher", _manage);
        var waiting = await setup.Harness.RunAsync(async services =>
        {
            var subStatuses = services.GetRequiredService<WorkOrderSettingManager<WorkOrderSubStatus>>();
            var subStatus = await subStatuses.NewAsync();

            subStatus.Name = "Waiting for parts";
            subStatus.ParentStatus = WorkOrderStatus.OnHold;
            await subStatuses.CreateAsync(subStatus);

            return subStatus.ItemId;
        });

        // Act
        var onHold = Parse(await InvokeAsync(setup, dispatcher, SetWorkOrderStatusTool.ToolName, new() { ["number"] = workOrder.Number, ["status"] = "OnHold", ["subStatus"] = "waiting for parts", ["note"] = "Compressor back-ordered" }));
        var unknownSubStatus = Parse(await InvokeAsync(setup, dispatcher, SetWorkOrderStatusTool.ToolName, new() { ["number"] = workOrder.Number, ["status"] = "OnHold", ["subStatus"] = "Lunch" }));
        var completeHere = await InvokeAsync(setup, dispatcher, SetWorkOrderStatusTool.ToolName, new() { ["number"] = workOrder.Number, ["status"] = "Completed" });
        var close = await InvokeAsync(setup, dispatcher, CloseWorkOrderTool.ToolName, new() { ["number"] = workOrder.Number, ["action"] = "Close" });
        var noReason = await InvokeAsync(setup, dispatcher, CloseWorkOrderTool.ToolName, new() { ["number"] = workOrder.Number, ["action"] = "Cancel" });
        var canceled = Parse(await InvokeAsync(setup, dispatcher, CloseWorkOrderTool.ToolName, new() { ["number"] = workOrder.Number, ["action"] = "Cancel", ["reason"] = "The customer sold the house." }));
        var stored = await FindAsync(setup, workOrder.ItemId);

        // Assert
        Assert.Equal("New", onHold.GetProperty("previousStatus").GetString());
        Assert.Equal("Waiting for parts", onHold.GetProperty("subStatus").GetString());
        Assert.False(onHold.GetProperty("slaPaused").GetBoolean());
        Assert.Equal(waiting, unknownSubStatus.GetProperty("subStatuses")[0].GetProperty("id").GetString());
        Assert.StartsWith("The status must be Ready", Assert.IsType<string>(completeHere), StringComparison.Ordinal);
        Assert.Equal("The work order was not closed: A work order can't move from On hold to Closed.", close);
        Assert.Equal("The work order was not closed: Say why the work order is canceled.", noReason);
        Assert.Equal("Canceled", canceled.GetProperty("status").GetString());
        Assert.Equal(WorkOrderStatus.Canceled, stored.Status);
        Assert.Equal("The customer sold the house.", stored.CancelReason);
    }

    [Fact]
    public async Task PartsChecklistsAndCompletion_UseTheService_AndReturnWhatToPickFromWhenAReferenceIsUnclear()
    {
        // Arrange
        await using var setup = await SetupAsync();
        var workOrder = await CreateWorkOrderAsync(setup);
        var technician = CalendarTestHarness.User("tech", _manage);

        await InvokeAsync(setup, technician, SetWorkOrderStatusTool.ToolName, new() { ["number"] = workOrder.Number, ["status"] = "InProgress" });

        // Act
        var unknownLine = Parse(await InvokeAsync(setup, technician, RecordWorkOrderPartUsedTool.ToolName, new() { ["number"] = workOrder.Number, ["line"] = "Gasket", ["quantity"] = 1 }));
        var labor = Parse(await InvokeAsync(setup, technician, RecordWorkOrderPartUsedTool.ToolName, new() { ["number"] = workOrder.Number, ["line"] = "labor", ["quantity"] = 1.5 }));
        var notYet = Parse(await InvokeAsync(setup, technician, CompleteWorkOrderTool.ToolName, new() { ["number"] = workOrder.Number }));
        var unknownChecklist = Parse(await InvokeAsync(setup, technician, AnswerWorkOrderChecklistTool.ToolName, new() { ["number"] = workOrder.Number, ["checklist"] = "Fire check", ["answers"] = new object[] { new Dictionary<string, object> { ["question"] = "x", ["value"] = "1" } } }));
        var outOfRange = Parse(await InvokeAsync(setup, technician, AnswerWorkOrderChecklistTool.ToolName, new() { ["number"] = workOrder.Number, ["checklist"] = "1", ["answers"] = new object[] { new Dictionary<string, object> { ["question"] = "Gas pressure (mbar)", ["value"] = 40 } } }));
        var answered = Parse(await InvokeAsync(setup, technician, AnswerWorkOrderChecklistTool.ToolName, new() { ["number"] = workOrder.Number, ["checklist"] = "Safety check", ["answers"] = new object[] { new Dictionary<string, object> { ["question"] = "pressure", ["value"] = 20 } } }));
        var done = Parse(await InvokeAsync(setup, technician, CompleteWorkOrderTool.ToolName, new() { ["number"] = workOrder.Number }));
        var stored = await FindAsync(setup, workOrder.ItemId);

        // Assert
        Assert.Equal(2, unknownLine.GetProperty("lines").GetArrayLength());
        Assert.Equal(1.5m, labor.GetProperty("line").GetProperty("quantityUsed").GetDecimal());
        Assert.False(labor.GetProperty("takenFromStock").GetBoolean());
        Assert.False(notYet.GetProperty("completed").GetBoolean());
        Assert.Equal("Answer \"Gas pressure (mbar)\" on Safety check.", notYet.GetProperty("missing")[0].GetString());
        Assert.Equal("pressure", unknownChecklist.GetProperty("checklists")[0].GetProperty("questions")[0].GetProperty("id").GetString());
        Assert.Equal("\"Gas pressure (mbar)\" must be from 15 to 25.", outOfRange.GetProperty("refused")[0].GetString());
        Assert.Equal(0, outOfRange.GetProperty("saved").GetArrayLength());
        Assert.Equal(0, answered.GetProperty("stillRequired").GetArrayLength());
        Assert.True(done.GetProperty("completed").GetBoolean());
        Assert.False(done.GetProperty("billingRequested").GetBoolean());
        Assert.Equal(WorkOrderStatus.Completed, stored.Status);
        Assert.Equal("20", stored.Checklists[0].Answers.Single().Value);
    }

    [Fact]
    public async Task WorkOrderSettings_AreListedReadSavedAndDeleted_WithTheEditorsRules()
    {
        // Arrange
        await using var setup = await SetupAsync();
        await CreateWorkOrderAsync(setup);
        var admin = CalendarTestHarness.User("admin", _settings, _serviceLevels);

        // Act
        var schema = Parse(await InvokeAsync(setup, admin, GetWorkOrderSettingTool.ToolName, new() { ["kind"] = "service-levels" }));
        var unknownChecklist = await InvokeAsync(setup, admin, SaveWorkOrderSettingTool.ToolName, new() { ["kind"] = "types", ["name"] = "AC tune-up", ["checklistTemplateIds"] = new[] { "ghost" } });
        var noPrice = await InvokeAsync(setup, admin, SaveWorkOrderSettingTool.ToolName, new() { ["kind"] = "types", ["name"] = "AC tune-up", ["billingMode"] = "FixedPrice" });
        var foreign = await InvokeAsync(setup, admin, SaveWorkOrderSettingTool.ToolName, new() { ["kind"] = "sub-statuses", ["name"] = "Waiting", ["crewSize"] = 2 });
        var created = Parse(await InvokeAsync(setup, admin, SaveWorkOrderSettingTool.ToolName, new()
        {
            ["kind"] = "types",
            ["name"] = "AC tune-up",
            ["defaultDurationMinutes"] = 90,
            ["crewSize"] = 1,
            ["checklistTemplateIds"] = new[] { setup.ChecklistId },
            ["billingMode"] = "FixedPrice",
            ["fixedPrice"] = 149,
            ["currency"] = "usd",
        }));
        var createdId = created.GetProperty("record").GetProperty("id").GetString();
        var listed = Parse(await InvokeAsync(setup, admin, ListWorkOrderSettingsTool.ToolName, new() { ["kind"] = "types", ["search"] = "tune" }));
        var read = Parse(await InvokeAsync(setup, admin, GetWorkOrderSettingTool.ToolName, new() { ["kind"] = "types", ["id"] = createdId }));
        var inUse = await InvokeAsync(setup, admin, DeleteWorkOrderSettingTool.ToolName, new() { ["kind"] = "types", ["id"] = setup.TypeId });
        var checklistInUse = await InvokeAsync(setup, admin, DeleteWorkOrderSettingTool.ToolName, new() { ["kind"] = "checklists", ["id"] = setup.ChecklistId });
        var deleted = Parse(await InvokeAsync(setup, admin, DeleteWorkOrderSettingTool.ToolName, new() { ["kind"] = "types", ["id"] = createdId }));
        var gone = await InvokeAsync(setup, admin, GetWorkOrderSettingTool.ToolName, new() { ["kind"] = "types", ["id"] = createdId });

        // Assert
        Assert.True(schema.GetProperty("isNew").GetBoolean());
        Assert.Equal(["OnHold"], schema.GetProperty("record").GetProperty("pauseStatuses").EnumerateArray().Select(status => status.GetString()));
        Assert.True(schema.GetProperty("schema").GetProperty("properties").TryGetProperty("targets", out _));
        Assert.False(schema.GetProperty("schema").GetProperty("properties").TryGetProperty("crewSize", out _));
        Assert.StartsWith("No checklist has the ID 'ghost'", Assert.IsType<string>(unknownChecklist), StringComparison.Ordinal);
        Assert.Equal("Nothing was saved: Enter the fixed price.", noPrice);
        Assert.StartsWith("A record of kind sub-statuses has no crewSize", Assert.IsType<string>(foreign), StringComparison.Ordinal);
        Assert.True(created.GetProperty("created").GetBoolean());
        Assert.Equal("USD", created.GetProperty("record").GetProperty("currency").GetString());
        Assert.Equal(createdId, Assert.Single(listed.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
        Assert.Equal(149m, read.GetProperty("record").GetProperty("fixedPrice").GetDecimal());
        Assert.Equal("Boiler service can't be deleted: work orders use it.", inUse);
        Assert.StartsWith("Safety check can't be deleted: work order types use it.", Assert.IsType<string>(checklistInUse), StringComparison.Ordinal);
        Assert.True(deleted.GetProperty("deleted").GetBoolean());
        Assert.StartsWith("No record of kind types has the ID", Assert.IsType<string>(gone), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServiceAgreements_AreSavedRunAndDeleted_AsTheirPagesDo()
    {
        // Arrange
        await using var setup = await SetupAsync();
        var manager = CalendarTestHarness.User("manager", _agreements);

        // Act
        var noPrice = await InvokeAsync(setup, manager, SaveServiceAgreementTool.ToolName, new() { ["name"] = "Filter care", ["pricing"] = "PerVisit" });
        var badRule = await InvokeAsync(setup, manager, SaveServiceAgreementTool.ToolName, new()
        {
            ["name"] = "Filter care",
            ["schedules"] = new object[] { new Dictionary<string, object> { ["workOrderTypeId"] = setup.TypeId, ["rule"] = "FREQ=SOMETIMES" } },
        });
        var draft = Parse(await InvokeAsync(setup, manager, SaveServiceAgreementTool.ToolName, new()
        {
            ["name"] = "Filter care",
            ["startDate"] = "2026-10-01",
            ["timeZoneId"] = "America/New_York",
            ["contactName"] = "Pat",
            ["pricing"] = "PerVisit",
            ["pricePerVisit"] = 189,
            ["schedules"] = new object[] { new Dictionary<string, object> { ["name"] = "Filter check", ["workOrderTypeId"] = setup.TypeId, ["rule"] = "FREQ=WEEKLY;BYDAY=MO", ["leadDays"] = 14 } },
        }));
        var number = draft.GetProperty("agreement").GetProperty("number").GetString();
        var notActive = await InvokeAsync(setup, manager, RunServiceAgreementTool.ToolName, new() { ["agreement"] = number });
        var activated = Parse(await InvokeAsync(setup, manager, SaveServiceAgreementTool.ToolName, new() { ["agreement"] = number, ["status"] = "Active" }));
        var run = Parse(await InvokeAsync(setup, manager, RunServiceAgreementTool.ToolName, new() { ["agreement"] = number }));
        var again = Parse(await InvokeAsync(setup, manager, RunServiceAgreementTool.ToolName, new() { ["agreement"] = number }));
        var noHours = await InvokeAsync(setup, manager, SaveServiceAgreementTool.ToolName, new() { ["agreement"] = number, ["hoursKind"] = "Block" });
        var withHours = Parse(await InvokeAsync(setup, manager, SaveServiceAgreementTool.ToolName, new() { ["agreement"] = number, ["hoursKind"] = "Block", ["hours"] = 20, ["roundingMinutes"] = 15 }));
        var read = Parse(await InvokeAsync(setup, manager, GetServiceAgreementTool.ToolName, new() { ["agreement"] = number }));
        var byShortNumber = Parse(await InvokeAsync(setup, manager, GetServiceAgreementTool.ToolName, new() { ["agreement"] = "1" }));
        var notDraft = await InvokeAsync(setup, manager, DeleteServiceAgreementTool.ToolName, new() { ["agreement"] = number });
        await InvokeAsync(setup, manager, SaveServiceAgreementTool.ToolName, new() { ["agreement"] = number, ["status"] = "Draft" });
        var deleted = Parse(await InvokeAsync(setup, manager, DeleteServiceAgreementTool.ToolName, new() { ["agreement"] = number }));
        var missing = await InvokeAsync(setup, manager, GetServiceAgreementTool.ToolName, new() { ["agreement"] = number });

        // Assert
        Assert.Equal("Nothing was saved: Enter the price of a visit.", noPrice);
        Assert.StartsWith("Nothing was saved: When a schedule repeats isn't a valid rule", Assert.IsType<string>(badRule), StringComparison.Ordinal);
        Assert.Equal("Draft", draft.GetProperty("agreement").GetProperty("status").GetString());
        Assert.Equal("SA-00001", number);
        Assert.StartsWith("SA-00001 is Draft", Assert.IsType<string>(notActive), StringComparison.Ordinal);
        Assert.Equal("Active", activated.GetProperty("agreement").GetProperty("status").GetString());
        Assert.Equal(3, run.GetProperty("made").GetInt32());
        Assert.Equal(0, again.GetProperty("made").GetInt32());
        Assert.StartsWith("Nothing was saved: Enter the hours the agreement covers", Assert.IsType<string>(noHours), StringComparison.Ordinal);
        Assert.Equal("Block", withHours.GetProperty("agreement").GetProperty("hoursKind").GetString());
        Assert.Equal("2026-10-01", read.GetProperty("startDate").GetString());
        Assert.Equal(20m, read.GetProperty("hours").GetDecimal());
        Assert.Equal(15, read.GetProperty("roundingMinutes").GetInt32());
        Assert.Equal(ServiceAgreementToolResults.UntrustedNote, read.GetProperty("note").GetString());
        Assert.Equal(number, byShortNumber.GetProperty("number").GetString());
        Assert.Equal(["2026-11-02", "2026-11-09", "2026-11-16"], read.GetProperty("schedules")[0].GetProperty("nextVisits").EnumerateArray().Select(day => day.GetString()));
        Assert.Equal(3, read.GetProperty("workOrders").GetArrayLength());
        Assert.Equal(189m, read.GetProperty("pricePerVisit").GetDecimal());
        Assert.StartsWith("Only a draft agreement can be deleted", Assert.IsType<string>(notDraft), StringComparison.Ordinal);
        Assert.True(deleted.GetProperty("deleted").GetBoolean());
        Assert.Equal(WorkOrderToolSupport.AgreementNotFound, missing);
    }

    private static async Task<Setup> SetupAsync()
    {
        var setup = new Setup();
        var timeZone = new Mock<ITimeZone>();
        var localClock = new Mock<ILocalClock>();

        // The user's time zone is five hours behind UTC, as New York is in November.
        timeZone.SetupGet(zone => zone.TimeZoneId).Returns("America/New_York");
        localClock.Setup(clock => clock.GetLocalTimeZoneAsync()).ReturnsAsync(timeZone.Object);
        localClock.Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync((DateTimeOffset utc) => new DateTimeOffset(DateTime.SpecifyKind(utc.UtcDateTime.AddHours(-5), DateTimeKind.Unspecified), TimeSpan.FromHours(-5)));
        localClock.Setup(clock => clock.ConvertToUtcAsync(It.IsAny<DateTime>())).ReturnsAsync((DateTime local) => DateTime.SpecifyKind(local.AddHours(5), DateTimeKind.Utc));

        setup.Harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddSingleton(localClock.Object);
                services.AddWorkOrdersWithServiceLevels();
                services.AddServiceAgreementsCore();
                services.AddScoped<ICatalogEntryHandler<Agreement>, AgreementHandler>();
                services.AddSingleton<INumberSequence, InMemoryNumberSequence>();
                services.AddSingleton(new WorkOrderLookups([]));
                services.AddScoped(provider => new WorkOrderCustomers(new PartyDirectory([]), provider.GetRequiredService<ISession>(), []));
                services.AddScoped<WorkOrderSettingDeletion>();
                services.AddSingleton<TestHttpContextAccessor>();
                services.AddSingleton<IHttpContextAccessor>(provider => provider.GetRequiredService<TestHttpContextAccessor>());
                services.AddScoped<TestUserAccessor>();
                services.AddScoped<IUserAccessor>(provider => provider.GetRequiredService<TestUserAccessor>());
            },
            async builder =>
            {
                await WorkOrderTestStores.CreateSchemaAsync(builder);
                await AgreementsIndexSchema.CreateAsync(builder);
            },
            [.. WorkOrderTestStores.IndexProviders, new AgreementIndexProvider()],
            [.. WorkOrderTestStores.Collections, AgreementsConstants.CollectionName]);

        (setup.TypeId, setup.ChecklistId) = await setup.Harness.RunAsync(async services =>
        {
            var checklists = services.GetRequiredService<WorkOrderSettingManager<ChecklistTemplate>>();
            var checklist = await checklists.NewAsync();

            checklist.Name = "Safety check";
            checklist.Questions = [new ChecklistQuestion { Id = "pressure", Text = "Gas pressure (mbar)", Kind = ChecklistQuestionKind.Number, Required = true, Min = 15, Max = 25 }];
            await checklists.CreateAsync(checklist);

            var types = services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>();
            var type = await types.NewAsync();

            type.Name = "Boiler service";
            type.DefaultDurationMinutes = 90;
            type.ChecklistTemplateIds = [checklist.ItemId];
            type.DefaultLines =
            [
                new WorkOrderLine { Kind = WorkOrderLineKind.Product, Description = "Filter", StockItemId = "filter-item", QuantityEstimated = 1, UnitPrice = 15 },
                new WorkOrderLine { Kind = WorkOrderLineKind.Labor, Description = "Labor", QuantityEstimated = 1.5m, UnitPrice = 80 },
            ];
            await types.CreateAsync(type);

            return (type.ItemId, checklist.ItemId);
        });

        return setup;
    }

    private static Task<WorkOrder> CreateWorkOrderAsync(Setup setup, string summary = "No heat", WorkOrderPriority priority = WorkOrderPriority.Normal)
        => setup.Harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<WorkOrderService>();
            var workOrder = await service.NewAsync(setup.TypeId);

            workOrder.Summary = summary;
            workOrder.Priority = priority;

            var result = await service.CreateAsync(workOrder, "dispatcher");

            Assert.True(result.Succeeded, string.Join(" ", result.Errors));

            return result.WorkOrder;
        });

    private static Task<WorkOrder> FindAsync(Setup setup, string id)
        => setup.Harness.RunAsync(services => services.GetRequiredService<WorkOrderStore>().FindByIdAsync(id).AsTask(), commit: false);

    private static Task<object> InvokeAsync(Setup setup, ClaimsPrincipal user, string toolName, Dictionary<string, object> arguments)
    {
        return setup.Harness.RunAsync(async services =>
        {
            services.GetRequiredService<TestHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = user, RequestServices = services };

            return await WorkOrderAIToolRegistrationTests.Create(toolName).InvokeAsync(new AIFunctionArguments(arguments) { Services = services }, TestContext.Current.CancellationToken);
        });
    }

    private static JsonElement Parse(object result)
    {
        var text = Assert.IsType<string>(result);

        Assert.StartsWith("{", text, StringComparison.Ordinal);

        return JsonElement.Parse(text);
    }

    private sealed class Setup : IAsyncDisposable
    {
        public CalendarTestHarness Harness { get; set; }

        public string TypeId { get; set; }

        public string ChecklistId { get; set; }

        public ValueTask DisposeAsync() => Harness?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext HttpContext { get; set; }
    }

    private sealed class TestUserAccessor : IUserAccessor
    {
        public ClaimsPrincipal User { get; set; }
    }
}
