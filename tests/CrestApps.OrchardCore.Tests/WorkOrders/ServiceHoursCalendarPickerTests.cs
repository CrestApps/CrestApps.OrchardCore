using CrestApps.OrchardCore.BusinessHours.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Controllers;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// The service level policy editor offers the hours calendars the service hours providers list, so WorkOrders picks a
/// Contact Center business-hours calendar without using Contact Center types.
/// </summary>
public sealed class ServiceHoursCalendarPickerTests
{
    [Fact]
    public async Task TheBusinessHoursProvider_ListsTheEnabledCalendarsByName()
    {
        // Arrange
        var manager = new Mock<IBusinessHoursCalendarManager>();
        manager.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new BusinessHoursCalendar { ItemId = "c2", Name = "Weekends" },
            new BusinessHoursCalendar { ItemId = "c1", Name = "Support desk" },
            new BusinessHoursCalendar { ItemId = "c3", Name = "Retired", Enabled = false },
        ]);

        IServiceHoursProvider provider = new OpenHoursServiceHoursProvider(new DefaultOpenHoursService([new BusinessHoursCalendarScheduleSource(manager.Object)]));

        // Act
        var calendars = await provider.ListCalendarsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([new ServiceHoursCalendar("c1", "Support desk"), new ServiceHoursCalendar("c2", "Weekends")], calendars);
    }

    [Fact]
    public async Task ThePicker_ListsEveryProvidersCalendarsOnce_AndKeepsASelectedCalendarNoLongerListed()
    {
        // Arrange
        var first = new Mock<IServiceHoursProvider>();
        first.Setup(value => value.ListCalendarsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new ServiceHoursCalendar("c1", "Support desk")]);

        var second = new Mock<IServiceHoursProvider>();
        second.Setup(value => value.ListCalendarsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new ServiceHoursCalendar("c1", "Duplicate"), new ServiceHoursCalendar("c0", "After hours")]);

        // Act
        var calendars = await ServiceHoursCalendars.ListAsync([first.Object, second.Object], TestContext.Current.CancellationToken);
        var options = WorkOrderSettingsController.HoursCalendarOptions(calendars, "gone");

        // Assert
        Assert.Equal(["After hours", "Support desk"], calendars.Select(calendar => calendar.Name));
        Assert.Equal(["c0", "c1", "gone"], options.Select(option => option.Value));
        Assert.True(options.Single(option => option.Value == "gone").Selected);
    }
}
