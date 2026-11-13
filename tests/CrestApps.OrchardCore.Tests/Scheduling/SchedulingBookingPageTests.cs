using System.Globalization;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Scheduling.Controllers;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
using OrchardCore.Modules;
using static CrestApps.OrchardCore.Tests.Scheduling.SchedulingTestSupport;

namespace CrestApps.OrchardCore.Tests.Scheduling;

/// <summary>
/// SEC-P9, still open after the first round, through the public booking page: the result of sending the confirmation
/// e-mail was ignored, so on a failing provider the time stayed held with nothing sent; and a network's booking attempt
/// was spent before the form and the reCAPTCHA were checked, so failed posts used up its bookings for the hour.
/// </summary>
public sealed class SchedulingBookingPageTests
{
    [Fact]
    public async Task ABookingWhoseConfirmationEmailCantBeSent_ReleasesTheTime_AndSaysSo()
    {
        // Arrange
        var email = new Mock<IEmailService>();

        email.Setup(service => service.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failed(new LocalizedString("Down", "The provider is down.")));

        await using var harness = await CreateAsync(services => services.AddSingleton(email.Object));
        var type = await AddSelfServiceTypeAsync(harness);
        var slot = (await FindAsync(harness, type, Day, Day.AddDays(1)))[0];

        // Act
        var result = await harness.RunAsync(services => Page(services, new Mock<IAttemptLimiter>(), allowed: true).BookPost(type.Slug, Booking(slot.StartUtc)));
        var slots = await FindAsync(harness, type, Day, Day.AddDays(1));

        // Assert
        var view = Assert.IsType<ViewResult>(result);

        Assert.NotEqual("CheckEmail", view.ViewName);
        Assert.NotEmpty(Assert.IsType<NewAppointmentViewModel>(view.Model).Errors);
        Assert.Contains(slots, candidate => candidate.StartUtc == slot.StartUtc);
    }

    [Fact]
    public async Task APostThatFailsTheForm_DoesntSpendTheNetworksBookingAttempt()
    {
        // Arrange
        await using var harness = await CreateAsync(services => services.AddSingleton(Mock.Of<IEmailService>()));
        var type = await AddSelfServiceTypeAsync(harness);
        var limiter = new Mock<IAttemptLimiter>();
        var noEmail = Booking(Day.AddHours(11));

        noEmail.CustomerEmail = null;

        // Act
        var result = await harness.RunAsync(services => Page(services, limiter, allowed: true).BookPost(type.Slug, noEmail));

        // Assert
        Assert.NotEmpty(Assert.IsType<NewAppointmentViewModel>(Assert.IsType<ViewResult>(result).Model).Errors);
        limiter.Verify(value => value.AcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task<AppointmentType> AddSelfServiceTypeAsync(CalendarTestHarness harness)
    {
        var resource = await AddResourceAsync(harness, "Dr. Page");

        return await AddTypeAsync(harness, [resource.ItemId], type =>
        {
            type.Slug = "consultation";
            type.AllowSelfService = true;
            type.RequireEmailConfirmation = true;
        });
    }

    private static NewAppointmentViewModel Booking(DateTime startUtc)
        => new()
        {
            Start = startUtc.ToString("O", CultureInfo.InvariantCulture),
            CustomerName = "John Smith",
            CustomerEmail = "john@contoso.com",
        };

    private static BookingPagesController Page(IServiceProvider services, Mock<IAttemptLimiter> limiter, bool allowed)
    {
        limiter.Setup(value => value.AcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttemptLimitResult(allowed, allowed ? 1 : 0, TimeSpan.Zero));

        var controller = ActivatorUtilities.CreateInstance<BookingPagesController>(
            services,
            ActivatorUtilities.CreateInstance<SchedulingPageService>(services),
            limiter.Object,
            new SignedTokenService(new EphemeralDataProtectionProvider(), services.GetRequiredService<IClock>()));
        var url = new Mock<IUrlHelper>();

        url.Setup(value => value.RouteUrl(It.IsAny<UrlRouteContext>())).Returns("https://shop.example/book/confirm");
        controller.Url = url.Object;
        var httpContext = new DefaultHttpContext { RequestServices = services };

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        return controller;
    }
}
