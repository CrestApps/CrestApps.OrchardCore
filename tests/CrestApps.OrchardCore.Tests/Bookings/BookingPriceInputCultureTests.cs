using System.Reflection;
using System.Security.Claims;
using CrestApps.OrchardCore.Bookings.Controllers;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Bookings.ViewModels;
using CrestApps.OrchardCore.Tests.Core.ModelBinding;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Primitives;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using BookingsStartup = CrestApps.OrchardCore.Bookings.Startup;

namespace CrestApps.OrchardCore.Tests.Bookings;

/// <summary>
/// Found with G11 to G13 (fourth fix verification): Bookings read its own price text with <c>NumberStyles.Number</c> and
/// the invariant culture, where the comma is a group separator, so a German admin's "140,50" was saved as 14050. A date's
/// price is posted here through MVC's own binder under the request culture, into the controller, as a site calls it.
/// </summary>
public sealed class BookingPriceInputCultureTests
{
    public static TheoryData<string, string> Posts => new()
    {
        { "en-US", "140.50" },
        { "de-DE", "140.50" },
        { "de-DE", "140,50" },
        { "fr-FR", "140,50" },
        { "nl-NL", "140.50" },
    };

    [Theory]
    [MemberData(nameof(Posts))]
    public async Task UpdateDate_SavesThePriceTheFormSent(string culture, string posted)
    {
        var slot = new BookingSlot { ItemId = "slot-1", ContentItemId = "product-1", Capacity = 4 };
        var slots = new Mock<IBookingSlotStore>();
        slots.Setup(store => store.FindByIdAsync("slot-1", It.IsAny<CancellationToken>())).ReturnsAsync(slot);

        var notifier = new Mock<INotifier>();
        var controller = CreateController(slots.Object, notifier.Object);
        var method = GetPostAction(nameof(BookingsAdminController.UpdateDate));

        var bound = await FormPostBinder.BindAsync(new BookingsStartup(), method, culture, new Dictionary<string, StringValues>
        {
            ["itemId"] = "slot-1",
            ["capacity"] = "4",
            ["price"] = posted,
        }, controller: controller);

        await (Task<IActionResult>)method.Invoke(controller, bound.Arguments);

        Assert.Equal(140.50m, slot.Price);
        slots.Verify(store => store.UpdateAsync(slot, It.IsAny<CancellationToken>()), Times.Once());
    }

    // A value that is not a number is refused, and the date keeps its price.
    [Theory]
    [InlineData("de-DE", "1.250,00")]
    [InlineData("en-US", "1,250.00")]
    public async Task UpdateDate_WithAPriceThatIsNotANumber_KeepsThePrice(string culture, string posted)
    {
        var slot = new BookingSlot { ItemId = "slot-1", ContentItemId = "product-1", Capacity = 4, Price = 99m };
        var slots = new Mock<IBookingSlotStore>();
        slots.Setup(store => store.FindByIdAsync("slot-1", It.IsAny<CancellationToken>())).ReturnsAsync(slot);

        var notifier = new Mock<INotifier>();
        var controller = CreateController(slots.Object, notifier.Object);
        var method = GetPostAction(nameof(BookingsAdminController.UpdateDate));

        var bound = await FormPostBinder.BindAsync(new BookingsStartup(), method, culture, new Dictionary<string, StringValues>
        {
            ["itemId"] = "slot-1",
            ["capacity"] = "4",
            ["price"] = posted,
        }, controller: controller);

        await (Task<IActionResult>)method.Invoke(controller, bound.Arguments);

        Assert.Equal(99m, slot.Price);
        slots.Verify(store => store.UpdateAsync(It.IsAny<BookingSlot>(), It.IsAny<CancellationToken>()), Times.Never());
        Assert.Single(notifier.Invocations, invocation => invocation.Arguments.Count > 0 && Equals(invocation.Arguments[0], NotifyType.Error));
    }

    [Theory]
    [MemberData(nameof(Posts))]
    public async Task CancelAndNewDate_BindTheAmountTheFormSent(string culture, string posted)
    {
        var cancel = await FormPostBinder.BindAsync(new BookingsStartup(), GetPostAction(nameof(BookingsAdminController.Cancel)), culture, new Dictionary<string, StringValues> { ["refund"] = posted });
        var dates = await FormPostBinder.BindAsync(new BookingsStartup(), GetPostAction(nameof(BookingsAdminController.DatesPost)), culture, new Dictionary<string, StringValues> { ["NewPrice"] = posted });

        Assert.Equal(140.50m, cancel.Arguments[1]);
        Assert.Equal(140.50m, Assert.IsType<BookingDatesViewModel>(dates.Arguments[1]).NewPrice);
    }

    // A refund that can't be read is refused before the booking is canceled, instead of falling back to the policy's.
    [Fact]
    public async Task Cancel_WithARefundThatIsNotANumber_IsRefused()
    {
        var bookings = new Mock<IBookingStore>();
        bookings.Setup(store => store.FindByIdAsync("booking-1", It.IsAny<CancellationToken>())).ReturnsAsync(new Booking { ItemId = "booking-1" });

        var notifier = new Mock<INotifier>();
        var controller = CreateController(Mock.Of<IBookingSlotStore>(), notifier.Object, bookings.Object);
        var method = GetPostAction(nameof(BookingsAdminController.Cancel));

        var bound = await FormPostBinder.BindAsync(new BookingsStartup(), method, "de-DE", new Dictionary<string, StringValues>
        {
            ["itemId"] = "booking-1",
            ["refund"] = "1.250,00",
        }, controller: controller);

        var result = await (Task<IActionResult>)method.Invoke(controller, bound.Arguments);

        Assert.Equal(nameof(BookingsAdminController.Detail), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Single(notifier.Invocations, invocation => invocation.Arguments.Count > 0 && Equals(invocation.Arguments[0], NotifyType.Error));
    }

    // The product's booking settings bind through the same binder, the occupancy and cancellation rows included.
    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public async Task BookingPartEditor_BindsTheNumbersTheFormSent(string culture)
    {
        var bound = await FormPostBinder.BindAsync(new BookingsStartup(), typeof(BookingPartEditorController).GetMethod(nameof(BookingPartEditorController.Update)), culture, new Dictionary<string, StringValues>
        {
            ["WeekendPrice"] = "140.50",
            ["DepositPercent"] = "12.5",
            ["Occupancies[0].Label"] = "Single",
            ["Occupancies[0].PriceAdjustment"] = "-15.25",
            ["CancellationTiers[0].DaysBefore"] = "30",
            ["CancellationTiers[0].FeePercent"] = "50.5",
        });

        var model = Assert.IsType<BookingPartViewModel>(bound.Arguments[0]);

        Assert.Equal(140.50m, model.WeekendPrice);
        Assert.Equal(12.5m, model.DepositPercent);
        Assert.Equal(-15.25m, model.Occupancies[0].PriceAdjustment);
        Assert.Equal(50.5m, model.CancellationTiers[0].FeePercent);
    }

    public sealed class BookingPartEditorController : Controller
    {
        [HttpPost]
        public IActionResult Update(BookingPartViewModel model) => Ok();
    }

    private static BookingsAdminController CreateController(IBookingSlotStore slots, INotifier notifier, IBookingStore bookings = null)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var htmlLocalizer = new Mock<IHtmlLocalizer<BookingsAdminController>>();
        htmlLocalizer.Setup(value => value[It.IsAny<string>()]).Returns((string name) => new LocalizedHtmlString(name, name));
        htmlLocalizer.Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] _) => new LocalizedHtmlString(name, name));

        return new BookingsAdminController(
            bookings ?? Mock.Of<IBookingStore>(),
            slots,
            Mock.Of<IBookingBalanceStore>(),
            service: null,
            capacity: null,
            charger: null,
            Mock.Of<IContentManager>(),
            authorization.Object,
            notifier,
            Mock.Of<IShapeFactory>(),
            Mock.Of<IClock>(),
            htmlLocalizer.Object,
            new PassThroughStringLocalizer<BookingsAdminController>());
    }

    private static MethodInfo GetPostAction(string name)
        => typeof(BookingsAdminController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(candidate => candidate.Name == name && candidate.GetCustomAttributes<HttpPostAttribute>().Any());
}
