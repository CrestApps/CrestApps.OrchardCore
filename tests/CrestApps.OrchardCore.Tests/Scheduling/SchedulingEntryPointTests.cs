using System.Security.Claims;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Scheduling;
using CrestApps.OrchardCore.Scheduling.AI;
using CrestApps.OrchardCore.Scheduling.Api;
using CrestApps.OrchardCore.Scheduling.Core;
using CrestApps.OrchardCore.Scheduling.Core.Indexes;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Migrations;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Scheduling;

/// <summary>
/// The scheduling API asks for the same permissions as the appointment screens and checks its input; a booking or a change
/// sent twice takes effect once; and a manage code opens only its own appointment. The store is made by the module's own
/// migration.
/// </summary>
public sealed class SchedulingEntryPointTests
{
    // Tuesday 3 November 2026; the harness clock says it is 12:00 UTC on 2 November.
    private static readonly DateTime _day = new(2026, 11, 3);

    [Fact]
    public async Task Api_ReadingNeedsViewAppointments_AndChangingNeedsManageAppointments()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var type = await AddTypeAsync(harness, (await AddResourceAsync(harness)).ItemId);
        var booked = await BookAsync(harness, type, Utc(_day.AddHours(9)), "Ann", "ann@contoso.com");
        var nobody = CalendarTestHarness.User("nobody");
        var viewer = CalendarTestHarness.User("viewer", SchedulingPermissions.ViewAppointments.Name);
        var manager = CalendarTestHarness.User("manager", SchedulingPermissions.ManageAppointments.Name);

        // Act
        var nobodyLists = await CallAsync(harness, nobody, (services, context) => SchedulingApiEndpoints.ListTypesAsync(context, Authorization(services), services.GetRequiredService<AppointmentTypeStore>()));
        var nobodyGets = await CallAsync(harness, nobody, (services, context) => SchedulingApiEndpoints.GetAsync(booked.Appointment.ItemId, context, Authorization(services), Appointments(services)));
        var viewerGets = await CallAsync(harness, viewer, (services, context) => SchedulingApiEndpoints.GetAsync(booked.Appointment.ItemId, context, Authorization(services), Appointments(services)));
        var viewerBooks = await CallAsync(harness, viewer, (services, context) => SchedulingApiEndpoints.BookAsync(Request(type, Utc(_day.AddHours(10)), "Bob", "bob@contoso.com"), context, Authorization(services), Appointments(services)));
        var viewerCancels = await CallAsync(harness, viewer, (services, context) => SchedulingApiEndpoints.CancelAsync(booked.Appointment.ItemId, new AppointmentCancelInput(), context, Authorization(services), Appointments(services)));
        var stillBooked = await FindAsync(harness, booked.Appointment.ItemId);
        var managerBooks = await CallAsync(harness, manager, (services, context) => SchedulingApiEndpoints.BookAsync(Request(type, Utc(_day.AddHours(10)), "Bob", "bob@contoso.com"), context, Authorization(services), Appointments(services)));
        var managerCancels = await CallAsync(harness, manager, (services, context) => SchedulingApiEndpoints.CancelAsync(booked.Appointment.ItemId, null, context, Authorization(services), Appointments(services)));

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(nobodyLists));
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(nobodyGets));
        Assert.Equal(StatusCodes.Status200OK, StatusOf(viewerGets));
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(viewerBooks));
        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(viewerCancels));
        Assert.Equal(AppointmentStatus.Confirmed, stillBooked.Status);
        Assert.Equal(StatusCodes.Status201Created, StatusOf(managerBooks));
        Assert.Equal(StatusCodes.Status200OK, StatusOf(managerCancels));
        Assert.Equal(AppointmentStatus.Cancelled, (await FindAsync(harness, booked.Appointment.ItemId)).Status);
    }

    [Fact]
    public async Task Api_MissingOrBackwardsInput_IsAValidationProblem_NotAServerError()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var type = await AddTypeAsync(harness, (await AddResourceAsync(harness)).ItemId);
        var booked = await BookAsync(harness, type, Utc(_day.AddHours(9)), "Ann", "ann@contoso.com");
        var manager = CalendarTestHarness.User("manager", SchedulingPermissions.ManageAppointments.Name);
        var from = new DateTimeOffset(DateTime.SpecifyKind(Utc(_day), DateTimeKind.Utc));

        // Act
        var noRequest = await CallAsync(harness, manager, (services, context) => SchedulingApiEndpoints.BookAsync(null, context, Authorization(services), Appointments(services)));
        var noNewTime = await CallAsync(harness, manager, (services, context) => SchedulingApiEndpoints.RescheduleAsync(booked.Appointment.ItemId, null, context, Authorization(services), Appointments(services)));
        var backwards = await CallAsync(harness, manager, (services, context) => SchedulingApiEndpoints.ListSlotsAsync(type.ItemId, from, from.AddHours(-1), null, context, Authorization(services), services.GetRequiredService<AppointmentTypeStore>(), services.GetRequiredService<ISchedulingAvailabilityService>()));
        var taken = await CallAsync(harness, manager, (services, context) => SchedulingApiEndpoints.BookAsync(Request(type, Utc(_day.AddHours(9)), "Cid", "cid@contoso.com"), context, Authorization(services), Appointments(services)));
        var unknown = await CallAsync(harness, manager, (services, context) => SchedulingApiEndpoints.GetAsync("missing", context, Authorization(services), Appointments(services)));

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(noRequest));
        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(noNewTime));
        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(backwards));
        Assert.Equal(StatusCodes.Status409Conflict, StatusOf(taken));
        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(unknown));
        Assert.Equal(booked.Appointment.StartUtc, (await FindAsync(harness, booked.Appointment.ItemId)).StartUtc);
    }

    [Fact]
    public async Task ABookingSentTwice_TakesOnePlace_EvenInAGroupSession()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var type = await AddTypeAsync(harness, (await AddResourceAsync(harness)).ItemId, t => t.Capacity = 3);
        var start = Utc(_day.AddHours(10));

        // Act
        var first = await BookAsync(harness, type, start, "Ann", "ann@contoso.com");
        var retried = await BookAsync(harness, type, start, "Ann", "ANN@contoso.com");
        var someoneElse = await BookAsync(harness, type, start, "Bob", "bob@contoso.com");
        var lastPlace = await BookAsync(harness, type, start, "Cid", "cid@contoso.com");
        var full = await BookAsync(harness, type, start, "Dan", "dan@contoso.com");

        // Assert
        Assert.True(first.Succeeded, string.Join(" ", first.Errors));
        Assert.False(retried.Succeeded);
        Assert.True(retried.Conflict);
        Assert.True(someoneElse.Succeeded, string.Join(" ", someoneElse.Errors));

        // The retry took no place, so the third place is still there for someone else, and only then is the session full.
        Assert.True(lastPlace.Succeeded, string.Join(" ", lastPlace.Errors));
        Assert.False(full.Succeeded);
    }

    [Fact]
    public async Task AHoldSentTwice_HoldsOnce_AndConfirmingOrCompletingTwice_RaisesOneEvent()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var type = await AddTypeAsync(harness, (await AddResourceAsync(harness)).ItemId);
        var start = Utc(_day.AddHours(11));

        // Act
        var held = await harness.RunAsync(services => Appointments(services).BookAsync(Request(type, start, "Ann", "ann@contoso.com", hold: true)));
        var heldAgain = await harness.RunAsync(services => Appointments(services).BookAsync(Request(type, start, "Ann", "ann@contoso.com", hold: true)));
        var confirmed = await ChangeAsync(harness, held.Appointment.ItemId, (service, appointment) => service.ConfirmAsync(appointment));
        var confirmedAgain = await ChangeAsync(harness, held.Appointment.ItemId, (service, appointment) => service.ConfirmAsync(appointment));
        var completed = await ChangeAsync(harness, held.Appointment.ItemId, (service, appointment) => service.CompleteAsync(appointment, noShow: false));
        var completedAgain = await ChangeAsync(harness, held.Appointment.ItemId, (service, appointment) => service.CompleteAsync(appointment, noShow: false));
        var cancelled = await ChangeAsync(harness, held.Appointment.ItemId, (service, appointment) => service.CancelAsync(appointment));

        // Assert
        Assert.Equal(AppointmentStatus.Held, held.Appointment.Status);
        Assert.True(heldAgain.Conflict);
        Assert.Equal([AppointmentChangeKind.Confirmed], confirmed.Pending.Select(change => change.Kind));
        Assert.False(confirmedAgain.Result.Succeeded);
        Assert.Empty(confirmedAgain.Pending);
        Assert.Equal([AppointmentChangeKind.Completed], completed.Pending.Select(change => change.Kind));
        Assert.True(completedAgain.Result.Succeeded);
        Assert.Empty(completedAgain.Pending);
        Assert.False(cancelled.Result.Succeeded);
        Assert.Empty(cancelled.Pending);
    }

    [Fact]
    public void AManageCode_OpensOnlyItsOwnAppointment()
    {
        // Arrange
        var ann = new Appointment { ItemId = "ann", ManageToken = "0123456789abcdef0123456789abcdef" };
        var bob = new Appointment { ItemId = "bob", ManageToken = "fedcba9876543210fedcba9876543210" };

        // Act & Assert
        Assert.True(AppointmentManageToken.Matches(ann, "0123456789abcdef0123456789abcdef"));
        Assert.False(AppointmentManageToken.Matches(bob, "0123456789abcdef0123456789abcdef"));
        Assert.False(AppointmentManageToken.Matches(ann, "0123456789abcdef0123456789abcde"));
        Assert.False(AppointmentManageToken.Matches(ann, "0123456789ABCDEF0123456789ABCDEF"));
        Assert.False(AppointmentManageToken.Matches(ann, null));
        Assert.False(AppointmentManageToken.Matches(new Appointment { ItemId = "old" }, string.Empty));
        Assert.False(AppointmentManageToken.Matches(null, "0123456789abcdef0123456789abcdef"));
    }

    [Fact]
    public void TheAIsProof_MustBeTheAppointmentsOwnCodeEmailOrPhone()
    {
        // Arrange
        var ann = new Appointment { ItemId = "ann", ManageToken = "0123456789abcdef0123456789abcdef", CustomerEmail = "ann@contoso.com", CustomerPhone = "+1 (702) 555-0100" };
        var noPhone = new Appointment { ItemId = "walk-in", ManageToken = "fedcba9876543210fedcba9876543210", CustomerPhone = "n/a" };

        // Act & Assert
        Assert.True(ChangeAppointmentTool.Matches(ann, "0123456789abcdef0123456789abcdef"));
        Assert.True(ChangeAppointmentTool.Matches(ann, "ANN@contoso.com"));
        Assert.True(ChangeAppointmentTool.Matches(ann, "17025550100"));
        Assert.False(ChangeAppointmentTool.Matches(ann, "fedcba9876543210fedcba9876543210"));
        Assert.False(ChangeAppointmentTool.Matches(ann, "bob@contoso.com"));
        Assert.False(ChangeAppointmentTool.Matches(ann, null));

        // A saved number without digits isn't matched by proof without digits.
        Assert.False(ChangeAppointmentTool.Matches(noPhone, "unknown"));
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return CalendarTestHarness.CreateAsync(
            services => services.AddSchedulingCore(),
            async schemaBuilder => Assert.Equal(2, await new SchedulingMigrations { SchemaBuilder = schemaBuilder }.CreateAsync()),
            [new SchedulingResourceIndexProvider(), new AppointmentTypeIndexProvider(), new AppointmentIndexProvider(), new WaitlistEntryIndexProvider()],
            [SchedulingConstants.CollectionName]);
    }

    private static async Task<SchedulingResource> AddResourceAsync(CalendarTestHarness harness)
    {
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Studio calendar");
        var resource = new SchedulingResource
        {
            Name = "Studio",
            CalendarId = calendar.ItemId,
            TimeZoneId = "America/New_York",
        };

        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            resource.WorkingHours.Add(new SchedulingWorkingHours { Day = day, Start = "09:00", End = "17:00" });
        }

        await harness.RunAsync(services => services.GetRequiredService<SchedulingResourceStore>().CreateAsync(resource).AsTask());

        return resource;
    }

    private static async Task<AppointmentType> AddTypeAsync(CalendarTestHarness harness, string resourceId, Action<AppointmentType> configure = null)
    {
        var type = new AppointmentType
        {
            Name = "Consultation",
            DurationMinutes = 60,
            SlotIntervalMinutes = 60,
            MinimumNoticeMinutes = 0,
            HorizonDays = 30,
            ResourceIds = [resourceId],
        };

        configure?.Invoke(type);
        await harness.RunAsync(services => services.GetRequiredService<AppointmentTypeStore>().CreateAsync(type).AsTask());

        return type;
    }

    private static Task<AppointmentResult> BookAsync(CalendarTestHarness harness, AppointmentType type, DateTime startUtc, string name, string email)
    {
        return harness.RunAsync(services => Appointments(services).BookAsync(Request(type, startUtc, name, email)));
    }

    private static Task<(AppointmentResult Result, List<AppointmentChange> Pending)> ChangeAsync(CalendarTestHarness harness, string id, Func<IAppointmentService, Appointment, Task<AppointmentResult>> change)
    {
        return harness.RunAsync(async services =>
        {
            var service = Appointments(services);
            var result = await change(service, await service.FindByIdAsync(id));

            return (result, services.GetRequiredService<AppointmentChangeQueue>().Pending.ToList());
        });
    }

    private static Task<Appointment> FindAsync(CalendarTestHarness harness, string id)
    {
        return harness.RunAsync(services => Appointments(services).FindByIdAsync(id), commit: false);
    }

    private static Task<IResult> CallAsync(CalendarTestHarness harness, ClaimsPrincipal user, Func<IServiceProvider, HttpContext, Task<IResult>> call)
    {
        return harness.RunAsync(services => call(services, new DefaultHttpContext { User = user, RequestServices = services }));
    }

    private static AppointmentRequest Request(AppointmentType type, DateTime startUtc, string name, string email, bool hold = false)
    {
        return new AppointmentRequest
        {
            AppointmentTypeId = type.ItemId,
            StartUtc = startUtc,
            CustomerName = name,
            CustomerEmail = email,
            Source = "Test",
            Hold = hold,
        };
    }

    private static int StatusOf(IResult result)
    {
        return result switch
        {
            ForbidHttpResult => StatusCodes.Status403Forbidden,
            IStatusCodeHttpResult { StatusCode: { } code } => code,
            _ => throw new InvalidOperationException($"No status code on {result?.GetType().Name}."),
        };
    }

    private static IAuthorizationService Authorization(IServiceProvider services) => services.GetRequiredService<IAuthorizationService>();

    private static IAppointmentService Appointments(IServiceProvider services) => services.GetRequiredService<IAppointmentService>();

    private static DateTime Utc(DateTime local) => CalendarTime.ToUtc(local, "America/New_York");
}
