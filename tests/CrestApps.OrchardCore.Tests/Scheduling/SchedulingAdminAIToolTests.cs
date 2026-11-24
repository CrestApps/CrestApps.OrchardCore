using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.AI.Agent.RecordTools;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Scheduling;
using CrestApps.OrchardCore.Scheduling.AI;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Drivers;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Tests.Agent.Records;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using static CrestApps.OrchardCore.Tests.Scheduling.SchedulingTestSupport;

namespace CrestApps.OrchardCore.Tests.Scheduling;

/// <summary>
/// The scheduling administration AI tools work only for a signed-in person with the admin page's permission, run the pages'
/// services and editor rules, show times in the appointment's or the site's time zone, and never return a manage token.
/// The scheduling resources are saved, listed and deleted through the generic record tools and the resource editor's
/// driver.
/// </summary>
public sealed class SchedulingAdminAIToolTests
{
    private static readonly string _view = SchedulingPermissions.ViewAppointments.Name;
    private static readonly string _manageAppointments = SchedulingPermissions.ManageAppointments.Name;
    private static readonly string _manageScheduling = SchedulingPermissions.ManageScheduling.Name;

    public static TheoryData<string> ToolNames
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var name in SchedulingAdminAIToolRegistrationTests.NewTools.Select(tool => tool.Name))
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
        await using var harness = await CreateAsync();
        var resource = await AddResourceAsync(harness, "Dr. Lee");
        var type = await AddTypeAsync(harness, [resource.ItemId]);
        var permission = SchedulingAdminAIToolRegistrationTests.NewTools.Single(tool => tool.Name == toolName).Permission;
        var arguments = new Dictionary<string, object>
        {
            ["appointmentTypeId"] = type.ItemId,
            ["resourceId"] = resource.ItemId,
            ["name"] = "Changed",
            ["fromDate"] = "2026-11-03",
            ["toDate"] = "2026-11-04",
            ["customerName"] = "Eve",
        };

        // A permission the pages do not ask for: Manage appointments only, against a tool that needs Manage scheduling, and so on.
        var below = permission == _manageScheduling ? _manageAppointments : permission == _manageAppointments ? _view : "AccessAdminPanel";

        // Act
        var anonymous = await InvokeAsync(harness, new ClaimsPrincipal(new ClaimsIdentity()), toolName, arguments);
        var anonymousWithPermission = await InvokeAsync(harness, new ClaimsPrincipal(new ClaimsIdentity([new Claim(global::OrchardCore.Security.Permissions.Permission.ClaimType, _manageScheduling)])), toolName, arguments);
        var lacking = await InvokeAsync(harness, CalendarTestHarness.User("john", below), toolName, arguments);
        var unchangedType = await FindAsync<AppointmentTypeStore, AppointmentType>(harness, type.ItemId);
        var unchangedResource = await FindAsync<SchedulingResourceStore, SchedulingResource>(harness, resource.ItemId);
        var waitlist = await harness.RunAsync(services => services.GetRequiredService<WaitlistStore>().PageAsync(0, 10), commit: false);

        // Assert
        Assert.Equal(SchedulingAdminToolBase.SignInRequired, anonymous);
        Assert.Equal(SchedulingAdminToolBase.SignInRequired, anonymousWithPermission);
        Assert.Equal($"The user isn't allowed to do this: it needs the '{permission}' permission.", lacking);
        Assert.Equal("Consultation", unchangedType.Name);
        Assert.Equal("Dr. Lee", unchangedResource.Name);
        Assert.Equal(0, waitlist.Count);
    }

    [Fact]
    public async Task Tools_ReadTheCallerFromTheUserAccessor_WhenThereIsNoRequest()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var resource = await AddResourceAsync(harness, "Dr. Lee");
        var type = await AddTypeAsync(harness, [resource.ItemId], t => t.Name = "Cleaning");

        // Act
        var result = await harness.RunAsync(async services =>
        {
            services.GetRequiredService<TestUserAccessor>().User = CalendarTestHarness.User("admin", _manageScheduling);

            return await new GetAppointmentTypeTool().InvokeAsync(new AIFunctionArguments(new Dictionary<string, object> { ["appointmentTypeId"] = type.ItemId }) { Services = services }, TestContext.Current.CancellationToken);
        });

        // Assert
        Assert.Equal("Cleaning", Parse(result).GetProperty("name").GetString());
    }

    [Fact]
    public async Task ListAndGetAppointment_ShowTheAppointmentInTheSitesTimeZone_AndNeverTheManageToken()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var resource = await AddResourceAsync(harness, "Dr. Lee");
        var type = await AddTypeAsync(harness, [resource.ItemId], t => t.RequiresConfirmation = true);
        var maria = await BookAsync(harness, Request(type, Day.AddHours(10), "Maria Lopez", "maria@contoso.com"));
        var john = await BookAsync(harness, Request(type, Day.AddHours(14), "John Reed", "john@contoso.com"));
        var viewer = CalendarTestHarness.User("viewer", _view);

        // Act
        var all = Parse(await InvokeAsync(harness, viewer, ListAppointmentsTool.ToolName, new() { ["status"] = "Requested", ["fromDate"] = "2026-11-03" }));
        var search = Parse(await InvokeAsync(harness, viewer, ListAppointmentsTool.ToolName, new() { ["search"] = "Maria" }));
        var later = Parse(await InvokeAsync(harness, viewer, ListAppointmentsTool.ToolName, new() { ["fromDate"] = "2026-11-04" }));
        var badStatus = await InvokeAsync(harness, viewer, ListAppointmentsTool.ToolName, new() { ["status"] = "Pending" });
        var badDate = await InvokeAsync(harness, viewer, ListAppointmentsTool.ToolName, new() { ["fromDate"] = "3 November" });
        var detail = await InvokeAsync(harness, viewer, GetAppointmentTool.ToolName, new() { ["appointmentId"] = maria.Appointment.ItemId });
        var missing = await InvokeAsync(harness, viewer, GetAppointmentTool.ToolName, new() { ["appointmentId"] = "missing" });
        var detailJson = Parse(detail);

        // Assert
        Assert.True(maria.Succeeded && john.Succeeded);
        Assert.Equal(2, all.GetProperty("total").GetInt32());
        Assert.False(all.GetProperty("truncated").GetBoolean());
        Assert.Equal(maria.Appointment.ItemId, all.GetProperty("items")[0].GetProperty("id").GetString());
        Assert.Equal("2026-11-03T10:00-05:00", all.GetProperty("items")[0].GetProperty("start").GetString());
        Assert.Equal("America/New_York", all.GetProperty("items")[0].GetProperty("timeZoneId").GetString());
        Assert.Equal("Consultation", all.GetProperty("items")[0].GetProperty("appointmentType").GetString());
        Assert.Equal("Maria Lopez", Assert.Single(search.GetProperty("items").EnumerateArray()).GetProperty("customerName").GetString());
        Assert.Equal(0, later.GetProperty("total").GetInt32());
        Assert.StartsWith("The status must be", Assert.IsType<string>(badStatus), StringComparison.Ordinal);
        Assert.StartsWith("The fromDate must be", Assert.IsType<string>(badDate), StringComparison.Ordinal);
        Assert.Equal("Requested", detailJson.GetProperty("status").GetString());
        Assert.Equal("maria@contoso.com", detailJson.GetProperty("customer").GetProperty("email").GetString());
        Assert.Equal("Dr. Lee", detailJson.GetProperty("resources")[0].GetProperty("name").GetString());
        Assert.Equal(maria.Appointment.CalendarEventId, detailJson.GetProperty("calendarEventId").GetString());
        Assert.False(string.IsNullOrEmpty(maria.Appointment.ManageToken));
        Assert.DoesNotContain(maria.Appointment.ManageToken, detail.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("manage", detail.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SchedulingToolResults.AppointmentNotFound, missing);
    }

    [Fact]
    public async Task SetAppointmentStatus_ConfirmsAndCompletes_ThroughTheAppointmentService_AndRelaysItsRefusals()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var resource = await AddResourceAsync(harness, "Dr. Lee");
        var type = await AddTypeAsync(harness, [resource.ItemId], t => t.RequiresConfirmation = true);
        var booked = await BookAsync(harness, Request(type, Day.AddHours(10)));
        var manager = CalendarTestHarness.User("frontdesk", _manageAppointments);
        var id = booked.Appointment.ItemId;

        // Act
        var cancel = await InvokeAsync(harness, manager, SetAppointmentStatusTool.ToolName, new() { ["appointmentId"] = id, ["status"] = "Cancelled" });
        var confirmed = Parse(await InvokeAsync(harness, manager, SetAppointmentStatusTool.ToolName, new() { ["appointmentId"] = id, ["status"] = "Confirmed" }));
        var again = await InvokeAsync(harness, manager, SetAppointmentStatusTool.ToolName, new() { ["appointmentId"] = id, ["status"] = "Confirmed" });
        var completed = Parse(await InvokeAsync(harness, manager, SetAppointmentStatusTool.ToolName, new() { ["appointmentId"] = id, ["status"] = "NoShow" }));
        var missing = await InvokeAsync(harness, manager, SetAppointmentStatusTool.ToolName, new() { ["appointmentId"] = "missing", ["status"] = "Confirmed" });
        var stored = await harness.RunAsync(services => services.GetRequiredService<IAppointmentService>().FindByIdAsync(id), commit: false);

        // Assert
        Assert.StartsWith("The status must be Confirmed, Completed or NoShow.", Assert.IsType<string>(cancel), StringComparison.Ordinal);
        Assert.Equal("Requested", confirmed.GetProperty("previousStatus").GetString());
        Assert.Equal("Confirmed", confirmed.GetProperty("status").GetString());
        Assert.Equal("The appointment was not changed: Only a held or requested appointment can be confirmed.", again);
        Assert.Equal("NoShow", completed.GetProperty("status").GetString());
        Assert.Equal(SchedulingToolResults.AppointmentNotFound, missing);
        Assert.Equal(AppointmentStatus.NoShow, stored.Status);
    }

    [Fact]
    public async Task ChangeAppointment_FindsAStaffCallerThroughTheUserAccessor_WhenTheRequestHasNoUser()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var resource = await AddResourceAsync(harness, "Dr. Lee");
        var type = await AddTypeAsync(harness, [resource.ItemId]);
        var booked = await BookAsync(harness, Request(type, Day.AddHours(10)));

        // Act: no proof, so only a caller who may manage appointments can cancel, and inside the chat hub that caller is
        // known only through the user accessor.
        var result = await harness.RunAsync(async services =>
        {
            services.GetRequiredService<TestHttpContextAccessor>().HttpContext = null;
            services.GetRequiredService<TestUserAccessor>().User = CalendarTestHarness.User("frontdesk", _manageAppointments);

            return await new ChangeAppointmentTool().InvokeAsync(new AIFunctionArguments(new Dictionary<string, object> { ["appointmentId"] = booked.Appointment.ItemId, ["action"] = "cancel" }) { Services = services }, TestContext.Current.CancellationToken);
        });
        var stored = await harness.RunAsync(services => services.GetRequiredService<IAppointmentService>().FindByIdAsync(booked.Appointment.ItemId), commit: false);

        // Assert
        Assert.Equal("Done.", result);
        Assert.Equal(AppointmentStatus.Cancelled, stored.Status);
    }

    [Fact]
    public async Task SaveAppointmentType_CreatesATypeWithItsGroups_AndUpdatesOnlyTheFieldsGiven()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var lee = await AddResourceAsync(harness, "Dr. Lee");
        var shah = await AddResourceAsync(harness, "Dr. Shah");
        var room = await AddResourceAsync(harness, "Room 2", r => r.Kind = SchedulingResourceKind.Room);
        var admin = CalendarTestHarness.User("admin", _manageScheduling);

        // Act
        var created = Parse(await InvokeAsync(harness, admin, SaveAppointmentTypeTool.ToolName, new()
        {
            ["name"] = "Initial consultation",
            ["slug"] = "Initial Consultation",
            ["durationMinutes"] = 45,
            ["slotIntervalMinutes"] = 15,
            ["bufferAfterMinutes"] = 15,
            ["allowSelfService"] = true,
            ["resourceGroups"] = JsonSerializer.SerializeToElement(new object[]
            {
                new { name = "Doctor", assignment = "RoundRobin", resourceIds = new[] { lee.ItemId, shah.ItemId }, weights = new[] { new { resourceId = lee.ItemId, weight = 3 } } },
                new { name = "Room", resourceIds = new[] { room.ItemId } },
            }),
        })).GetProperty("appointmentType");
        var id = created.GetProperty("id").GetString();
        var updated = Parse(await InvokeAsync(harness, admin, SaveAppointmentTypeTool.ToolName, new() { ["appointmentTypeId"] = id, ["bufferBeforeMinutes"] = 5 })).GetProperty("appointmentType");
        var read = Parse(await InvokeAsync(harness, admin, GetAppointmentTypeTool.ToolName, new() { ["appointmentTypeId"] = id }));
        var stored = await FindAsync<AppointmentTypeStore, AppointmentType>(harness, id);

        // Assert
        Assert.Equal("initial-consultation", created.GetProperty("slug").GetString());
        Assert.Equal(45, created.GetProperty("durationMinutes").GetInt32());
        Assert.Equal(2, created.GetProperty("resourceGroups").GetArrayLength());
        Assert.Equal("RoundRobin", created.GetProperty("resourceGroups")[0].GetProperty("assignment").GetString());
        Assert.Equal(3, created.GetProperty("resourceGroups")[0].GetProperty("resources")[0].GetProperty("weight").GetInt32());
        Assert.Equal("Dr. Lee", created.GetProperty("resourceGroups")[0].GetProperty("resources")[0].GetProperty("name").GetString());
        Assert.Equal(5, updated.GetProperty("bufferBeforeMinutes").GetInt32());
        Assert.Equal(15, updated.GetProperty("bufferAfterMinutes").GetInt32());
        Assert.True(updated.GetProperty("allowSelfService").GetBoolean());
        Assert.True(updated.GetProperty("enabled").GetBoolean());
        Assert.Equal(2, read.GetProperty("resourceGroups").GetArrayLength());
        Assert.Equal("Initial consultation", stored.Name);
        Assert.Equal(5, stored.BufferBeforeMinutes);
        Assert.Equal(new[] { lee.ItemId, shah.ItemId, room.ItemId }, stored.ResourceIds);
    }

    [Fact]
    public async Task SaveAppointmentType_RefusesWhatTheEditorRefuses_AndUnknownResources_WithoutChangingTheType()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var lee = await AddResourceAsync(harness, "Dr. Lee");
        await AddTypeAsync(harness, [lee.ItemId], t => t.Slug = "cleaning");
        var type = await AddTypeAsync(harness, [lee.ItemId], t => t.Name = "Checkup");
        var admin = CalendarTestHarness.User("admin", _manageScheduling);

        // Act
        var taken = await InvokeAsync(harness, admin, SaveAppointmentTypeTool.ToolName, new() { ["appointmentTypeId"] = type.ItemId, ["slug"] = "cleaning", ["name"] = "Changed" });
        var twoGroups = await InvokeAsync(harness, admin, SaveAppointmentTypeTool.ToolName, new()
        {
            ["appointmentTypeId"] = type.ItemId,
            ["resourceGroups"] = JsonSerializer.SerializeToElement(new object[] { new { resourceIds = new[] { lee.ItemId } }, new { resourceIds = new[] { lee.ItemId } } }),
        });
        var unknown = await InvokeAsync(harness, admin, SaveAppointmentTypeTool.ToolName, new()
        {
            ["appointmentTypeId"] = type.ItemId,
            ["resourceGroups"] = JsonSerializer.SerializeToElement(new object[] { new { resourceIds = new[] { "ghost" } } }),
        });
        var tooShort = await InvokeAsync(harness, admin, SaveAppointmentTypeTool.ToolName, new() { ["appointmentTypeId"] = type.ItemId, ["durationMinutes"] = 3 });
        var noName = await InvokeAsync(harness, admin, SaveAppointmentTypeTool.ToolName, new() { ["durationMinutes"] = 30 });
        var missing = await InvokeAsync(harness, admin, SaveAppointmentTypeTool.ToolName, new() { ["appointmentTypeId"] = "missing", ["name"] = "X" });
        var stored = await FindAsync<AppointmentTypeStore, AppointmentType>(harness, type.ItemId);
        var all = await harness.RunAsync(services => services.GetRequiredService<AppointmentTypeStore>().ListAsync(), commit: false);

        // Assert
        Assert.Equal("Nothing was saved: Another appointment type uses this address.", taken);
        Assert.Equal("Nothing was saved: A resource can be in only one group.", twoGroups);
        Assert.StartsWith("Nothing was saved: There is no scheduling resource with the ID ghost.", Assert.IsType<string>(unknown), StringComparison.Ordinal);
        Assert.Equal("Nothing was saved: The length and the time between start times must be at least 5 minutes.", tooShort);
        Assert.Equal("Nothing was saved: The name is required.", noName);
        Assert.Equal(SchedulingToolResults.AppointmentTypeNotFound, missing);
        Assert.Equal("Checkup", stored.Name);
        Assert.Null(stored.Slug);
        Assert.Equal(60, stored.DurationMinutes);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task DeleteAppointmentType_DeletesThroughTheStore()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var lee = await AddResourceAsync(harness, "Dr. Lee");
        var type = await AddTypeAsync(harness, [lee.ItemId]);
        var admin = CalendarTestHarness.User("admin", _manageScheduling);

        // Act
        var deletedType = Parse(await InvokeAsync(harness, admin, DeleteAppointmentTypeTool.ToolName, new() { ["appointmentTypeId"] = type.ItemId }));
        var typeAgain = await InvokeAsync(harness, admin, DeleteAppointmentTypeTool.ToolName, new() { ["appointmentTypeId"] = type.ItemId });

        // Assert
        Assert.True(deletedType.GetProperty("deleted").GetBoolean());
        Assert.Null(await FindAsync<AppointmentTypeStore, AppointmentType>(harness, type.ItemId));
        Assert.Equal(SchedulingToolResults.AppointmentTypeNotFound, typeAgain);
    }

    [Fact]
    public async Task SaveRecord_CreatesASchedulingResource_ThroughTheResourceEditor_WithItsAppointmentsCalendar()
    {
        // Arrange
        await using var harness = await CreateWithEditorAsync();
        var admin = CalendarTestHarness.User("admin", _manageScheduling);

        // Act
        var json = Parse(await InvokeToolAsync(harness, admin, services => new SaveRecordTool(RecordToolHarness.Kinds(services)), new()
        {
            ["kind"] = SchedulingResourceRecordKind.Name,
            ["values"] = JsonNode.Parse("""{ "Name": "Dr. Lee", "Kind": "Staff", "UserId": "lee", "WorkingHours": "Monday 08:00-16:00\nTuesday 08:00-16:00", "DateOverrides": "2026-12-24 closed" }"""),
        }));
        var id = json.GetProperty("id").GetString();
        var stored = await FindAsync<SchedulingResourceStore, SchedulingResource>(harness, id);
        var calendar = await harness.RunAsync(services => services.GetRequiredService<ICalendarService>().FindByIdAsync(stored.CalendarId).AsTask(), commit: false);

        // Assert
        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal("lee", stored.UserId);
        Assert.Equal("America/New_York", stored.TimeZoneId);
        Assert.Equal(2, stored.WorkingHours.Count);
        Assert.Equal("08:00", stored.WorkingHours[0].Start);
        Assert.True(Assert.Single(stored.DateOverrides).Closed);
        Assert.Equal(SchedulingConstants.CalendarType, calendar.Type);
        Assert.Equal(id, calendar.SourceId);
        Assert.Equal($"Monday 08:00-16:00{Environment.NewLine}Tuesday 08:00-16:00", json.GetProperty("record").GetProperty("workingHours").GetString());
    }

    [Fact]
    public async Task SaveRecord_RefusesWhatTheResourceEditorRefuses_WithoutChangingTheResource()
    {
        // Arrange
        await using var harness = await CreateWithEditorAsync();
        var lee = await AddResourceAsync(harness, "Dr. Lee");
        var admin = CalendarTestHarness.User("admin", _manageScheduling);

        // Act
        var zone = Parse(await InvokeToolAsync(harness, admin, services => new SaveRecordTool(RecordToolHarness.Kinds(services)), new()
        {
            ["kind"] = SchedulingResourceRecordKind.Name,
            ["id"] = lee.ItemId,
            ["values"] = JsonNode.Parse("""{ "TimeZoneId": "Mars/Olympus", "Name": "Changed" }"""),
        }));
        var backwards = Parse(await InvokeToolAsync(harness, admin, services => new SaveRecordTool(RecordToolHarness.Kinds(services)), new()
        {
            ["kind"] = SchedulingResourceRecordKind.Name,
            ["id"] = lee.ItemId,
            ["values"] = JsonNode.Parse("""{ "WorkingHours": "Monday 17:00-09:00" }"""),
        }));
        var refused = await InvokeToolAsync(harness, CalendarTestHarness.User("frontdesk", _manageAppointments), services => new SaveRecordTool(RecordToolHarness.Kinds(services)), new()
        {
            ["kind"] = SchedulingResourceRecordKind.Name,
            ["id"] = lee.ItemId,
            ["values"] = JsonNode.Parse("""{ "Name": "Changed" }"""),
        });
        var stored = await FindAsync<SchedulingResourceStore, SchedulingResource>(harness, lee.ItemId);

        // Assert
        Assert.False(zone.GetProperty("saved").GetBoolean());
        Assert.Contains(zone.GetProperty("errors").EnumerateArray(), error => error.GetProperty("message").GetString() == "'Mars/Olympus' is not a time zone.");
        Assert.False(backwards.GetProperty("saved").GetBoolean());
        Assert.Contains(backwards.GetProperty("errors").EnumerateArray(), error => error.GetProperty("message").GetString() == "This line can't be read: Monday 17:00-09:00");
        Assert.DoesNotContain("\"saved\":true", Assert.IsType<string>(refused), StringComparison.Ordinal);
        Assert.Equal("Dr. Lee", stored.Name);
        Assert.Equal("America/New_York", stored.TimeZoneId);
        Assert.Equal(10, stored.WorkingHours.Count);
    }

    [Fact]
    public async Task ListAndDeleteRecord_ListTheResourcesWithTheirHours_AndDeleteOne_KeepingItsCalendar()
    {
        // Arrange
        await using var harness = await CreateWithEditorAsync();
        var lee = await AddResourceAsync(harness, "Dr. Lee");
        await AddResourceAsync(harness, "Room 2", r => r.Kind = SchedulingResourceKind.Room);
        var admin = CalendarTestHarness.User("admin", _manageScheduling);

        // Act
        var listed = Parse(await InvokeToolAsync(harness, admin, services => new ListRecordsTool(RecordToolHarness.Kinds(services)), new() { ["kind"] = SchedulingResourceRecordKind.Name, ["query"] = "room" }));
        var deleted = Parse(await InvokeToolAsync(harness, admin, services => new DeleteRecordTool(RecordToolHarness.Kinds(services)), new() { ["kind"] = SchedulingResourceRecordKind.Name, ["ids"] = new[] { lee.ItemId } }));
        var gone = await FindAsync<SchedulingResourceStore, SchedulingResource>(harness, lee.ItemId);
        var calendar = await harness.RunAsync(services => services.GetRequiredService<ICalendarService>().FindByIdAsync(lee.CalendarId).AsTask(), commit: false);

        // Assert
        var room = Assert.Single(listed.GetProperty("items").EnumerateArray());

        Assert.Equal("Room 2", room.GetProperty("name").GetString());
        Assert.Equal("Room", room.GetProperty("kind").GetString());
        Assert.StartsWith("Monday 09:00-12:00, 13:00-17:00", room.GetProperty("workingHours").GetString(), StringComparison.Ordinal);
        Assert.Equal(lee.ItemId, Assert.Single(deleted.GetProperty("deleted").EnumerateArray()).GetProperty("id").GetString());
        Assert.Null(gone);
        Assert.NotNull(calendar);
    }

    [Fact]
    public async Task Waitlist_AddsListsAndRemovesEntries_ForWholeDaysInTheSitesTimeZone()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var lee = await AddResourceAsync(harness, "Dr. Lee");
        var type = await AddTypeAsync(harness, [lee.ItemId], t => t.Name = "Cleaning");
        var manager = CalendarTestHarness.User("frontdesk", _manageAppointments);

        // Act
        var added = Parse(await InvokeAsync(harness, manager, AddToAppointmentWaitlistTool.ToolName, new()
        {
            ["appointmentTypeId"] = type.ItemId,
            ["fromDate"] = "2026-11-09",
            ["toDate"] = "2026-11-13",
            ["customerName"] = "John Reed",
            ["customerPhone"] = "+1 702 555 0100",
        })).GetProperty("entry");
        var backwards = await InvokeAsync(harness, manager, AddToAppointmentWaitlistTool.ToolName, new() { ["appointmentTypeId"] = type.ItemId, ["fromDate"] = "2026-11-13", ["toDate"] = "2026-11-09", ["customerName"] = "Eve" });
        var unknownType = await InvokeAsync(harness, manager, AddToAppointmentWaitlistTool.ToolName, new() { ["appointmentTypeId"] = "ghost", ["fromDate"] = "2026-11-09", ["toDate"] = "2026-11-13", ["customerName"] = "Eve" });
        var listed = Parse(await InvokeAsync(harness, manager, ListAppointmentWaitlistTool.ToolName, []));
        var removed = Parse(await InvokeAsync(harness, manager, RemoveFromAppointmentWaitlistTool.ToolName, new() { ["waitlistEntryId"] = added.GetProperty("id").GetString() }));
        var missing = await InvokeAsync(harness, manager, RemoveFromAppointmentWaitlistTool.ToolName, new() { ["waitlistEntryId"] = "missing" });
        var stored = await FindAsync<WaitlistStore, WaitlistEntry>(harness, added.GetProperty("id").GetString());

        // Assert
        Assert.Equal("2026-11-09", added.GetProperty("fromDay").GetString());
        Assert.Equal("2026-11-13", added.GetProperty("toDay").GetString());
        Assert.Equal("Cleaning", added.GetProperty("appointmentType").GetString());
        Assert.StartsWith("Nothing was added:", Assert.IsType<string>(backwards), StringComparison.Ordinal);
        Assert.Equal(SchedulingToolResults.AppointmentTypeNotFound, unknownType);
        Assert.Equal(1, listed.GetProperty("total").GetInt32());
        Assert.Equal("Waiting", listed.GetProperty("items")[0].GetProperty("status").GetString());
        Assert.Equal("+1 702 555 0100", listed.GetProperty("items")[0].GetProperty("customerPhone").GetString());
        Assert.Equal("Removed", removed.GetProperty("status").GetString());
        Assert.Equal(SchedulingToolResults.WaitlistEntryNotFound, missing);
        Assert.Equal(WaitlistStatus.Removed, stored.Status);
        Assert.Equal(CalendarTime.ToUtc(new DateTime(2026, 11, 9), "America/New_York"), stored.FromUtc);
        Assert.Equal(CalendarTime.ToUtc(new DateTime(2026, 11, 14), "America/New_York"), stored.ToUtc);
    }

    private static Task<CalendarTestHarness> CreateAsync(Action<IServiceCollection> configure = null)
    {
        return SchedulingTestSupport.CreateAsync(services =>
        {
            services.AddScoped<SchedulingResourceCalendars>();
            services.AddCalendarType(SchedulingConstants.CalendarType, type => type.ManagedByCode());
            services.AddSingleton<TestHttpContextAccessor>();
            services.AddSingleton<IHttpContextAccessor>(provider => provider.GetRequiredService<TestHttpContextAccessor>());
            services.AddScoped<TestUserAccessor>();
            services.AddScoped<IUserAccessor>(provider => provider.GetRequiredService<TestUserAccessor>());
            configure?.Invoke(services);
        });
    }

    // The resource editor as the Scheduling Resources page builds it (the display manager, MVC binding and the editor's
    // driver), and the record kinds the admin startup registers.
    private static Task<CalendarTestHarness> CreateWithEditorAsync()
    {
        return CreateAsync(services =>
        {
            var timeZones = new Mock<ITimeZoneSelectListProvider>();

            timeZones.Setup(provider => provider.GetTimeZoneSelectListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new KeyValuePair<string, string>("America/New_York", "New York"), new KeyValuePair<string, string>("UTC", "UTC") });

            services.AddLocalization();
            services.AddMvcCore().AddDataAnnotations();
            services.AddEditorDisplayManagement();
            services.AddScoped<AIRecordScopeChanges>();
            services.AddScoped<IDisplayDriver<SchedulingResource>, SchedulingResourceFieldsDisplayDriver>();
            services.AddSingleton(timeZones.Object);

            new SchedulingAdminAIToolsStartup(new RecordTestLocalizer<SchedulingAdminAIToolsStartup>()).ConfigureServices(services);
        });
    }

    private static Task<object> InvokeAsync(CalendarTestHarness harness, ClaimsPrincipal user, string toolName, Dictionary<string, object> arguments)
    {
        return harness.RunAsync(async services =>
        {
            services.GetRequiredService<TestHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = user, RequestServices = services };

            return await SchedulingAdminAIToolRegistrationTests.Create(toolName).InvokeAsync(new AIFunctionArguments(arguments) { Services = services }, TestContext.Current.CancellationToken);
        });
    }

    private static Task<object> InvokeToolAsync(CalendarTestHarness harness, ClaimsPrincipal user, Func<IServiceProvider, AIFunction> createTool, Dictionary<string, object> arguments)
    {
        return harness.RunAsync(async services =>
        {
            services.GetRequiredService<TestHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = user, RequestServices = services };

            return await createTool(services).InvokeAsync(new AIFunctionArguments(arguments) { Services = services }, TestContext.Current.CancellationToken);
        });
    }

    private static Task<TModel> FindAsync<TStore, TModel>(CalendarTestHarness harness, string id)
        where TStore : CrestApps.Core.Services.ICatalog<TModel>
        where TModel : CrestApps.Core.Models.CatalogItem
    {
        return harness.RunAsync(services => services.GetRequiredService<TStore>().FindByIdAsync(id).AsTask(), commit: false);
    }

    private static JsonElement Parse(object result)
    {
        var text = Assert.IsType<string>(result);

        Assert.StartsWith("{", text, StringComparison.Ordinal);

        return JsonElement.Parse(text);
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
