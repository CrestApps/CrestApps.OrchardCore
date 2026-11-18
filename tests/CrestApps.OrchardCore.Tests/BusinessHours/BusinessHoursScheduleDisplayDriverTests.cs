using CrestApps.OrchardCore.BusinessHours.Core.Models;
using CrestApps.OrchardCore.BusinessHours.Drivers;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.BusinessHours;

/// <summary>
/// The business hours schedule editor. The catalog admin controller shows the shape that <c>UpdateEditorAsync</c>
/// returns again when the post is not valid, so the driver's update has to hand back the same editor its edit builds.
/// </summary>
public sealed class BusinessHoursScheduleDisplayDriverTests
{
    [Fact]
    public async Task Update_ReturnsTheEditorAgain_WithWhatWasPosted_SoAPostWithAnErrorComesBackFilledIn()
    {
        // Arrange
        var driver = (IDisplayDriver<BusinessHoursSchedule>)CreateDriver();
        var schedule = new BusinessHoursSchedule { ItemId = "schedule-1" };
        var posted = new BusinessHoursScheduleViewModel
        {
            Name = "  Support hours ",
            TimeZoneId = "America/New_York",
            SpecialDays = "2026-12-25",
            Days = [new WeeklyHoursRow { Day = DayOfWeek.Monday, Open = true, From = "08:30", To = "16:00" }],
        };
        var context = PostedFormUpdateModel.CreateContext(posted, isNew: true);

        // Act
        var models = await DisplayResultModels.BuildAsync<BusinessHoursScheduleViewModel>(await driver.UpdateEditorAsync(schedule, context));

        // Assert - the general, weekly hours and special days cards, each filled in from the post.
        Assert.Equal(3, models.Count);
        Assert.All(models, model =>
        {
            Assert.Equal("Support hours", model.Name);
            Assert.Equal("America/New_York", model.TimeZoneId);
            Assert.Contains("2026-12-25", model.SpecialDays, StringComparison.Ordinal);
            Assert.Equal("08:30", model.Days.Single(day => day.Day == DayOfWeek.Monday).From);
        });
    }

    private static BusinessHoursScheduleDisplayDriver CreateDriver()
    {
        var timeZones = new Mock<ITimeZoneSelectListProvider>();
        timeZones.Setup(provider => provider.GetTimeZoneSelectListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        return new BusinessHoursScheduleDisplayDriver(
            timeZones.Object,
            new PassThroughStringLocalizer<BusinessHoursScheduleDisplayDriver>());
    }
}
