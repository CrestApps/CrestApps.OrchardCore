using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The business-hours calendar editor. The catalog admin controller shows the shape that <c>UpdateEditorAsync</c>
/// returns again when the post is not valid, so the driver's update has to hand back the same editor its edit builds.
/// </summary>
public sealed class BusinessHoursCalendarDisplayDriverTests
{
    [Fact]
    public async Task Update_ReturnsTheEditorAgain_WithWhatWasPosted_SoAPostWithAnErrorComesBackFilledIn()
    {
        // Arrange
        var driver = (IDisplayDriver<BusinessHoursCalendar>)CreateDriver();
        var calendar = new BusinessHoursCalendar { ItemId = "calendar-1" };
        var posted = new BusinessHoursCalendarViewModel
        {
            Name = "  Support hours ",
            TimeZoneId = "America/New_York",
            HolidaysText = "2026-12-25",
            Days = [new BusinessHoursDayViewModel { Day = DayOfWeek.Monday, IsOpen = true, OpenTime = "08:30", CloseTime = "16:00" }],
        };
        var context = PostedFormUpdateModel.CreateContext(posted, isNew: true);

        // Act
        var models = await DisplayResultModels.BuildAsync<BusinessHoursCalendarViewModel>(await driver.UpdateEditorAsync(calendar, context));

        // Assert - the general, weekly schedule and holidays cards, each filled in from the post.
        Assert.Equal(3, models.Count);
        Assert.All(models, model =>
        {
            Assert.Equal("calendar-1", model.Id);
            Assert.Equal("Support hours", model.Name);
            Assert.Equal("America/New_York", model.TimeZoneId);
            Assert.Equal("2026-12-25", model.HolidaysText);
            Assert.Equal("08:30", model.Days.Single(day => day.Day == DayOfWeek.Monday).OpenTime);
        });
    }

    private static BusinessHoursCalendarDisplayDriver CreateDriver()
    {
        var site = new SiteSettings { TimeZoneId = "Europe/London" };
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);

        var clock = new Mock<IClock>();
        clock.Setup(service => service.GetTimeZones()).Returns([]);

        return new BusinessHoursCalendarDisplayDriver(
            new PassThroughStringLocalizer<BusinessHoursCalendarDisplayDriver>(),
            clock.Object,
            siteService.Object);
    }
}
