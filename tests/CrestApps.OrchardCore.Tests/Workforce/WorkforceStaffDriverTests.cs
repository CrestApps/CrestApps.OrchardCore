using CrestApps.Core;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Workforce.Drivers;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Workforce adds its scheduling card to the staff editor and keeps its values in the staff member's properties: the
/// hours and availability survive a save and a reload, changes made to the availability list in place are kept, and a
/// line of availability that can't be read is reported under the card's prefix and shown again as typed.
/// </summary>
public sealed class WorkforceStaffDriverTests
{
    [Fact]
    public async Task SchedulingCard_AppliesTheHoursAndAvailability_AndTheyAreKeptOnTheStaffRecord()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var staff = await WorkforceTests.AddWorkerAsync(harness, "Lee Park");
        var context = PostedFormUpdateModel.CreateContext(new WorkforceStaffEditViewModel
        {
            MinHoursPerWeek = 10,
            MaxHoursPerWeek = 30,
            MaxHoursPerDay = 8,
            Availability = "Monday 09:00-17:00",
        });

        // Act
        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IStaffMemberManager>();
            var stored = await manager.FindByIdAsync(staff.ItemId);

            await ((IDisplayDriver<StaffMember>)Driver(services)).UpdateEditorAsync(stored, context);
            await manager.UpdateAsync(stored);
        });
        var reloaded = await harness.RunAsync(services => services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(staff.ItemId).AsTask(), commit: false);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal(10m, reloaded.MinHoursPerWeek);
        Assert.Equal(30m, reloaded.MaxHoursPerWeek);
        Assert.Equal(8m, reloaded.MaxHoursPerDay);
        Assert.Equal(DayOfWeek.Monday, Assert.Single(reloaded.Availability).Day);
        Assert.True(reloaded.TryGet<WorkforceStaffSettings>(out var settings));
        Assert.Equal(30m, settings.MaxHoursPerWeek);
    }

    [Fact]
    public async Task AvailabilityChangedInPlace_IsKeptWhenTheStaffMemberIsSaved()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();

        // Act
        var staff = await WorkforceTests.AddWorkerAsync(harness, "Lee Park", member => member.Availability.Add(new AvailabilityWindow { Day = DayOfWeek.Friday, Preference = AvailabilityPreference.Unavailable }));
        var reloaded = await harness.RunAsync(services => services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(staff.ItemId).AsTask(), commit: false);

        // Assert
        Assert.Equal(DayOfWeek.Friday, Assert.Single(reloaded.Availability).Day);
    }

    [Fact]
    public async Task SchedulingCard_ReportsALineItCantRead_UnderItsPrefix_AndShowsWhatWasTyped()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var context = PostedFormUpdateModel.CreateContext(new WorkforceStaffEditViewModel { Availability = "Someday whenever" }, isNew: true);

        // Act
        var model = await harness.RunAsync(async services =>
        {
            var result = await ((IDisplayDriver<StaffMember>)Driver(services)).UpdateEditorAsync(new StaffMember(), context);

            return Assert.Single(await DisplayResultModels.BuildAsync<WorkforceStaffEditViewModel>(result));
        }, commit: false);

        // Assert
        Assert.StartsWith("This line can't be read:", Assert.Single(context.Updater.ModelState["StaffMember.Availability"].Errors).ErrorMessage);
        Assert.Equal("Someday whenever", model.Availability);
    }

    [Fact]
    public async Task FewestHoursAboveTheMost_IsRefused()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();

        // Act
        var errors = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IStaffMemberManager>();
            var staff = await manager.NewAsync();

            staff.DisplayName = "Lee Park";
            staff.MinHoursPerWeek = 40;
            staff.MaxHoursPerWeek = 20;

            return (await manager.ValidateAsync(staff)).Errors.Select(error => error.ErrorMessage).ToArray();
        }, commit: false);

        // Assert
        Assert.Contains("The fewest hours a week are more than the most.", errors);
    }

    private static WorkforceStaffDisplayDriver Driver(IServiceProvider services)
        => new(services.GetRequiredService<IStringLocalizer<WorkforceStaffDisplayDriver>>());
}
