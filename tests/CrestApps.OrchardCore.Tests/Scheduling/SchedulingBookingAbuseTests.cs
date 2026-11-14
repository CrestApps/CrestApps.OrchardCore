using System.Net;
using System.Reflection;
using System.Threading.RateLimiting;
using CrestApps.OrchardCore.Scheduling;
using CrestApps.OrchardCore.Scheduling.Controllers;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.RateLimits;
using static CrestApps.OrchardCore.Tests.Scheduling.SchedulingTestSupport;

namespace CrestApps.OrchardCore.Tests.Scheduling;

/// <summary>
/// SEC-P9: the public booking page took any number of bookings from anyone, with only an antiforgery token, and the
/// per-customer cap keyed on an unverified e-mail address, so a script could book every open slot with throwaway
/// addresses. The page is throttled per network (always, and with Orchard Core's Rate Limits by a built-in group limit),
/// renders reCAPTCHA when that feature is on, and an appointment type can require the customer to confirm the booking
/// from their e-mail: until then the time is only held, and the hold expires.
/// </summary>
public sealed class SchedulingBookingAbuseTests
{
    private static readonly DateTime _day = Day;

    [Fact]
    public async Task ASelfServiceBooking_OnATypeThatNeedsEmailConfirmation_IsOnlyHeld_UntilConfirmed()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var resource = await AddResourceAsync(harness, "Dr. Confirm");
        var type = await AddTypeAsync(harness, [resource.ItemId], type =>
        {
            type.AllowSelfService = true;
            type.RequireEmailConfirmation = true;
        });

        // Act
        var booked = await BookAsync(harness, HeldRequest(type));
        var confirmed = await harness.RunAsync(async services =>
        {
            var appointment = await services.GetRequiredService<AppointmentStore>().FindByIdAsync(booked.Appointment.ItemId);

            return await services.GetRequiredService<IAppointmentService>().ConfirmContactAsync(appointment, "john@contoso.com");
        });

        // Assert
        Assert.True(booked.Succeeded, string.Join(" ", booked.Errors));
        Assert.Equal(AppointmentStatus.Held, booked.Appointment.Status);
        Assert.NotNull(booked.Appointment.HoldExpiresUtc);
        Assert.True(confirmed.Succeeded, string.Join(" ", confirmed.Errors));
        Assert.Equal(AppointmentStatus.Confirmed, confirmed.Appointment.Status);
        Assert.Null(confirmed.Appointment.HoldExpiresUtc);
    }

    [Fact]
    public async Task AConfirmedContact_StillWaitsForStaff_WhenTheTypeNeedsConfirmation()
    {
        await using var harness = await CreateAsync();
        var resource = await AddResourceAsync(harness, "Dr. Review");
        var type = await AddTypeAsync(harness, [resource.ItemId], type =>
        {
            type.AllowSelfService = true;
            type.RequireEmailConfirmation = true;
            type.RequiresConfirmation = true;
        });

        var booked = await BookAsync(harness, HeldRequest(type));
        var confirmed = await harness.RunAsync(async services =>
        {
            var appointment = await services.GetRequiredService<AppointmentStore>().FindByIdAsync(booked.Appointment.ItemId);

            return await services.GetRequiredService<IAppointmentService>().ConfirmContactAsync(appointment, "john@contoso.com");
        });

        Assert.Equal(AppointmentStatus.Requested, confirmed.Appointment.Status);
    }

    [Fact]
    public async Task AnUnconfirmedHold_Expires_AndFreesTheSlot()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var resource = await AddResourceAsync(harness, "Dr. Expire");
        var type = await AddTypeAsync(harness, [resource.ItemId], type =>
        {
            type.AllowSelfService = true;
            type.RequireEmailConfirmation = true;
        });
        var booked = await BookAsync(harness, HeldRequest(type));

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<AppointmentStore>();
            var appointment = await store.FindByIdAsync(booked.Appointment.ItemId);

            appointment.HoldExpiresUtc = CalendarTestHarness.Now.AddMinutes(-1);
            await store.UpdateAsync(appointment);
        });

        // Act
        var expired = await harness.RunAsync(services => services.GetRequiredService<IAppointmentService>().ExpireHoldsAsync());
        var slots = await FindAsync(harness, type, _day.AddHours(9), _day.AddHours(17));

        // Assert
        Assert.Equal(1, expired);
        Assert.Contains("Tue 11:00", Local(slots));
    }

    // What the public booking page asks for on a type that needs e-mail confirmation.
    private static global::CrestApps.OrchardCore.Scheduling.Models.AppointmentRequest HeldRequest(AppointmentType type)
    {
        var request = Request(type, _day.AddHours(11), selfService: true);

        request.Hold = type.RequireEmailConfirmation;

        return request;
    }

    [Fact]
    public void ABookingPostedTooOftenFromOneNetwork_GetsTooManyRequests_ByTheRateLimitsFeature()
    {
        // Arrange
        var services = new ServiceCollection();
        new SelfServiceRateLimitsStartup().ConfigureServices(services);
        var limit = Assert.Single(
            services.BuildServiceProvider().GetRequiredService<IOptions<RateLimitsOptions>>().Value.GroupRateLimits,
            candidate => candidate.GroupName == SchedulingConstants.RateLimitGroups.Booking);
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.9");
        var partition = limit.Partitioner(httpContext);
        using var limiter = partition.Factory(partition.PartitionKey);

        // Act
        var allowed = Enumerable.Range(0, SelfServiceRateLimitsStartup.BookingsPerWindow).Select(_ =>
        {
            using var lease = limiter.AttemptAcquire();

            return lease.IsAcquired;
        }).ToList();
        using var refused = limiter.AttemptAcquire();

        // Assert
        Assert.All(allowed, Assert.True);
        Assert.False(refused.IsAcquired);
    }

    [Fact]
    public void ThePublicBookingPost_IsInTheBookingGroup_AndHasANetworkAttemptPolicy()
    {
        // The attempt policy holds whether or not Rate Limits is on.
        var services = new ServiceCollection();
        new SelfServiceStartup().ConfigureServices(services);
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<AttemptLimitOptions>>().Value;
        var group = typeof(BookingPagesController).GetMethod(nameof(BookingPagesController.BookPost))?.GetCustomAttribute<RateLimitGroupAttribute>();

        Assert.True(options.Policies.TryGetValue(SchedulingConstants.AttemptPolicies.BookingNetwork, out var policy));
        Assert.True(policy.PermitLimit > 0);
        Assert.NotNull(group);
    }
}
