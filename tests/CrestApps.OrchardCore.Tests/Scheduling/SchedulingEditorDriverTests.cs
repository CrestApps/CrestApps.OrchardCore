using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Drivers;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Scheduling;

/// <summary>
/// The scheduling resource and appointment type editors are built by display drivers: the built-in drivers fill and
/// read the same fields the forms always had, with the same checks reported under the driver's prefix, and a driver
/// another module registers adds its own section to the same editor.
/// </summary>
public sealed class SchedulingEditorDriverTests
{
    [Fact]
    public async Task ResourceDriver_FillsTheEditorFromTheResource()
    {
        // Arrange
        var resource = new SchedulingResource
        {
            ItemId = "resource-1",
            Name = "Dr. Lee",
            Kind = SchedulingResourceKind.Staff,
            UserId = "user-1",
            CalendarId = "calendar-1",
            WorkingHours = [new SchedulingWorkingHours { Day = DayOfWeek.Monday, Start = "09:00", End = "12:00" }],
        };

        // Act
        var result = CreateResourceDriver().Edit(resource, PostedFormUpdateModel.CreateContext(new SchedulingResourceViewModel()));
        var model = Assert.Single(await DisplayResultModels.BuildAsync<SchedulingResourceViewModel>(result));

        // Assert
        Assert.Equal("Dr. Lee", model.Name);
        Assert.Equal(nameof(SchedulingResourceKind.Staff), model.Kind);
        Assert.Equal("America/New_York", model.TimeZoneId);
        Assert.Contains("Monday", model.WorkingHours);
        Assert.Equal(["calendar-1"], model.CalendarOptions.Where(option => option.Selected).Select(option => option.Value));
        Assert.Contains("user-1", model.InitialUserJson);
        Assert.Equal(4, model.KindOptions.Count);
    }

    [Fact]
    public async Task ResourceDriver_AppliesThePostedFields()
    {
        // Arrange
        var resource = new SchedulingResource();
        var posted = new SchedulingResourceViewModel
        {
            Name = "  Room 2 ",
            Kind = nameof(SchedulingResourceKind.Room),
            UserId = "user-1",
            TimeZoneId = "Europe/Paris",
            AvailabilityMode = nameof(SchedulingAvailabilityMode.Shifts),
            ShiftCalendarIds = ["shifts-1", "shifts-1", " "],
            WorkingHours = "Tuesday 08:00-12:00",
            Enabled = false,
        };
        var context = PostedFormUpdateModel.CreateContext(posted, isNew: true);

        // Act
        await UpdateAsync(CreateResourceDriver(), resource, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal("Room 2", resource.Name);
        Assert.Equal(SchedulingResourceKind.Room, resource.Kind);
        Assert.Null(resource.UserId);
        Assert.Equal("Europe/Paris", resource.TimeZoneId);
        Assert.Equal(SchedulingAvailabilityMode.Shifts, resource.AvailabilityMode);
        Assert.Equal(["shifts-1"], resource.ShiftCalendarIds);
        Assert.Equal(DayOfWeek.Tuesday, Assert.Single(resource.WorkingHours).Day);
        Assert.False(resource.Enabled);
    }

    [Fact]
    public async Task ResourceDriver_RefusesAMissingNameAndAnUnreadableLine_AndKeepsWhatWasTyped()
    {
        // Arrange
        var posted = new SchedulingResourceViewModel
        {
            Name = " ",
            WorkingHours = "Someday whenever",
            TimeZoneId = "Mars/Olympus",
        };
        var context = PostedFormUpdateModel.CreateContext(posted);

        // Act
        var result = await UpdateAsync(CreateResourceDriver(), new SchedulingResource(), context);
        var model = Assert.Single(await DisplayResultModels.BuildAsync<SchedulingResourceViewModel>(result));

        // Assert
        Assert.Equal(["The name is required."], context.Updater.ModelState[$"{nameof(SchedulingResource)}.{nameof(SchedulingResourceViewModel.Name)}"].Errors.Select(error => error.ErrorMessage));
        Assert.StartsWith("This line can't be read:", Assert.Single(context.Updater.ModelState[$"{nameof(SchedulingResource)}.{nameof(SchedulingResourceViewModel.WorkingHours)}"].Errors).ErrorMessage);
        Assert.Equal(["'Mars/Olympus' is not a time zone."], context.Updater.ModelState[$"{nameof(SchedulingResource)}.{nameof(SchedulingResourceViewModel.TimeZoneId)}"].Errors.Select(error => error.ErrorMessage));
        Assert.Equal("Someday whenever", model.WorkingHours);
    }

    [Fact]
    public async Task AppointmentTypeDriver_FillsAndAppliesTheFields_WithItsResourceGroups()
    {
        // Arrange
        await using var harness = await SchedulingTestSupport.CreateAsync();
        var staff = await SchedulingTestSupport.AddResourceAsync(harness, "Dr. Lee");
        var room = await SchedulingTestSupport.AddResourceAsync(harness, "Room 1");
        var type = new AppointmentType();
        var posted = new AppointmentTypeViewModel
        {
            Name = " Checkup ",
            Slug = "Annual Checkup!",
            DurationMinutes = 45,
            SlotIntervalMinutes = 15,
            HorizonDays = 5000,
            Capacity = 0,
            Requirements =
            [
                new AppointmentResourceRequirementViewModel { Name = "Staff", ResourceIds = [staff.ItemId], Assignment = nameof(AppointmentAssignment.RoundRobin), Weights = new() { [staff.ItemId] = 3 } },
                new AppointmentResourceRequirementViewModel { Name = "Room", ResourceIds = [room.ItemId], Assignment = nameof(AppointmentAssignment.AnyAvailable) },
                new AppointmentResourceRequirementViewModel(),
            ],
        };
        var context = PostedFormUpdateModel.CreateContext(posted, isNew: true);

        // Act
        var model = await harness.RunAsync(async services =>
        {
            var driver = CreateTypeDriver(services);

            await UpdateAsync(driver, type, context);

            return Assert.Single(await DisplayResultModels.BuildAsync<AppointmentTypeViewModel>(driver.Edit(type, context)));
        }, commit: false);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal("Checkup", type.Name);
        Assert.Equal("annual-checkup", type.Slug);
        Assert.Equal(730, type.HorizonDays);
        Assert.Equal(1, type.Capacity);
        Assert.Equal(["Staff", "Room"], type.ResourceRequirements.Select(requirement => requirement.Name));
        Assert.Equal(3, type.ResourceRequirements[0].GetWeight(staff.ItemId));
        Assert.Equal([staff.ItemId, room.ItemId], type.ResourceIds);
        Assert.Equal(2, model.VisibleRequirements);
        Assert.Equal(5, model.Requirements.Count);
        Assert.Equal(2, model.ResourceOptions.Count);
    }

    [Fact]
    public async Task AppointmentTypeDriver_RefusesAnAddressAnotherTypeUses_AndAResourceInTwoGroups()
    {
        // Arrange
        await using var harness = await SchedulingTestSupport.CreateAsync();
        var staff = await SchedulingTestSupport.AddResourceAsync(harness, "Dr. Lee");

        await harness.RunAsync(services => services.GetRequiredService<AppointmentTypeStore>().CreateAsync(new AppointmentType { ItemId = "type-1", Name = "Existing", Slug = "checkup" }).AsTask());

        var posted = new AppointmentTypeViewModel
        {
            Name = "Checkup",
            Slug = "checkup",
            DurationMinutes = 3,
            Requirements =
            [
                new AppointmentResourceRequirementViewModel { ResourceIds = [staff.ItemId] },
                new AppointmentResourceRequirementViewModel { ResourceIds = [staff.ItemId] },
            ],
        };
        var context = PostedFormUpdateModel.CreateContext(posted, isNew: true);

        // Act
        await harness.RunAsync(services => UpdateAsync(CreateTypeDriver(services), new AppointmentType(), context), commit: false);

        // Assert
        Assert.Equal(["Another appointment type uses this address."], context.Updater.ModelState[$"{nameof(AppointmentType)}.{nameof(AppointmentTypeViewModel.Slug)}"].Errors.Select(error => error.ErrorMessage));
        Assert.Equal(["A resource can be in only one group."], context.Updater.ModelState[$"{nameof(AppointmentType)}.{nameof(AppointmentTypeViewModel.Requirements)}"].Errors.Select(error => error.ErrorMessage));
        Assert.Equal(["The length and the time between start times must be at least 5 minutes."], context.Updater.ModelState[$"{nameof(AppointmentType)}.{nameof(AppointmentTypeViewModel.DurationMinutes)}"].Errors.Select(error => error.ErrorMessage));
    }

    [Fact]
    public void TheBuiltInDrivers_AreRegisteredForBothEditors()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new global::CrestApps.OrchardCore.Scheduling.Startup().ConfigureServices(services);

        // Assert
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IDisplayDriver<SchedulingResource>) && descriptor.ImplementationType == typeof(SchedulingResourceFieldsDisplayDriver));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IDisplayDriver<AppointmentType>) && descriptor.ImplementationType == typeof(AppointmentTypeFieldsDisplayDriver));
    }

    [Fact]
    public async Task ResourceEditor_ShowsTheSectionOfADriverAnotherModuleRegisters_AfterTheBuiltInFields()
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddEditorDisplayManagement()
            .AddScoped<IDisplayDriver<SchedulingResource>>(_ => CreateResourceDriver())
            .AddDisplayDriver<SchedulingResource, ExtraSectionDisplayDriver<SchedulingResource>>()
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var displayManager = scope.ServiceProvider.GetRequiredService<IDisplayManager<SchedulingResource>>();

        // Act
        var editor = await displayManager.BuildEditorAsync(new SchedulingResource { Name = "Room 1" }, new PostedFormUpdateModel(null), isNew: false);
        var shapes = EditorDisplayManagement.ZoneShapes(editor);

        // Assert
        Assert.Equal(["SchedulingResourceFields_Edit", ExtraSectionDisplayDriver<SchedulingResource>.ShapeType], shapes.Select(shape => shape.Metadata.Type));
        Assert.Equal("Room 1", Assert.IsAssignableFrom<SchedulingResourceViewModel>(shapes[0]).Name);
        Assert.Equal(nameof(SchedulingResource), shapes[0].Metadata.Prefix);
        Assert.Equal(ExtraSectionDisplayDriver<SchedulingResource>.Note, Assert.IsAssignableFrom<ExtraSectionViewModel>(shapes[1]).Note);
    }

    [Fact]
    public async Task ResourceEditor_ReportsTheErrorsOfEverySection_UnderTheirPrefix()
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddEditorDisplayManagement()
            .AddScoped<IDisplayDriver<SchedulingResource>>(_ => CreateResourceDriver())
            .AddDisplayDriver<SchedulingResource, RefusingSectionDisplayDriver<SchedulingResource>>()
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var displayManager = scope.ServiceProvider.GetRequiredService<IDisplayManager<SchedulingResource>>();
        var updater = new PostedFormUpdateModel(new SchedulingResourceViewModel { Name = " " });

        // Act
        var editor = await displayManager.UpdateEditorAsync(new SchedulingResource(), updater, isNew: true);

        // Assert
        Assert.Equal(["The name is required."], updater.ModelState["SchedulingResource.Name"].Errors.Select(error => error.ErrorMessage));
        Assert.Equal([RefusingSectionDisplayDriver<SchedulingResource>.Error], updater.ModelState["SchedulingResource.Note"].Errors.Select(error => error.ErrorMessage));
        Assert.Equal(2, EditorDisplayManagement.ZoneShapes(editor).Count);
    }

    [Fact]
    public async Task AppointmentTypeEditor_ShowsTheSectionOfADriverAnotherModuleRegisters()
    {
        // Arrange
        await using var harness = await SchedulingTestSupport.CreateAsync(services => services
            .AddEditorDisplayManagement()
            .AddScoped<IDisplayDriver<AppointmentType>>(provider => CreateTypeDriver(provider))
            .AddDisplayDriver<AppointmentType, ExtraSectionDisplayDriver<AppointmentType>>());

        // Act
        var shapes = await harness.RunAsync(async services =>
        {
            var editor = await services.GetRequiredService<IDisplayManager<AppointmentType>>().BuildEditorAsync(new AppointmentType { Name = "Checkup" }, new PostedFormUpdateModel(null), isNew: true);

            return EditorDisplayManagement.ZoneShapes(editor);
        }, commit: false);

        // Assert
        Assert.Equal(["AppointmentTypeFields_Edit", ExtraSectionDisplayDriver<AppointmentType>.ShapeType], shapes.Select(shape => shape.Metadata.Type));
        Assert.Equal("Checkup", Assert.IsAssignableFrom<AppointmentTypeViewModel>(shapes[0]).Name);
    }

    private static SchedulingResourceFieldsDisplayDriver CreateResourceDriver()
    {
        var calendars = new Mock<ICalendarService>();

        calendars.Setup(service => service.ListAsync(It.IsAny<CalendarListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Calendar { ItemId = "calendar-1", Name = "Dr. Lee", Type = "General" }, new Calendar { ItemId = "calendar-2", Name = "Room 1", Type = "General" }]);

        var site = new Mock<ISite>();

        site.SetupGet(value => value.TimeZoneId).Returns("America/New_York");

        var siteService = new Mock<ISiteService>();

        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new SchedulingResourceFieldsDisplayDriver(
            calendars.Object,
            Mock.Of<ITimeZoneSelectListProvider>(),
            siteService.Object,
            RecipeRoundTrip.Localizer<SchedulingResourceFieldsDisplayDriver>());
    }

    /// <summary>
    /// Updates the editor through the driver interface, as the display manager does, so the driver's prefix is set.
    /// </summary>
    private static Task<IDisplayResult> UpdateAsync<TModel>(IDisplayDriver<TModel> driver, TModel model, UpdateEditorContext context)
        where TModel : class
        => driver.UpdateEditorAsync(model, context);

    private static AppointmentTypeFieldsDisplayDriver CreateTypeDriver(IServiceProvider services)
        => new(
            services.GetRequiredService<AppointmentTypeStore>(),
            services.GetRequiredService<SchedulingResourceStore>(),
            RecipeRoundTrip.Localizer<AppointmentTypeFieldsDisplayDriver>());
}
