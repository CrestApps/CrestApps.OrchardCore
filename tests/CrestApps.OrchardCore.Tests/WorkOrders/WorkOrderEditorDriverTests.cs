using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Drivers;
using CrestApps.OrchardCore.WorkOrders.Integrations;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using CrestApps.OrchardCore.WorkOrders.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// The work order editor is built by display drivers: the built-in driver fills and reads the fields the form always
/// had, and a driver another module registers adds its own section, whose errors stop the save under its prefix.
/// </summary>
public sealed class WorkOrderEditorDriverTests
{
    private static readonly DateTime _now = new(2026, 11, 5, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Driver_FillsTheEditorFromTheWorkOrder_WithTwoBlankLines()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var typeId = await AddTypeAsync(harness);
        var workOrder = new WorkOrder
        {
            ItemId = "wo-1",
            Number = "WO-00001",
            TypeId = typeId,
            Summary = "No heat",
            Contact = new WorkOrderContact { Name = "Dana Reyes", Phone = "555-0100" },
            ServiceAddress = new Address { AddressLine1 = "1 Main St", City = "Springfield", Latitude = 40.1, Longitude = -75.2 },
            EarliestUtc = new DateTime(2026, 11, 6, 14, 0, 0, DateTimeKind.Utc),
            Lines = [new WorkOrderLine { Id = "line-1", Description = "Filter", QuantityEstimated = 2, UnitPrice = 15 }],
        };

        // Act
        var model = await harness.RunAsync(async services =>
        {
            var result = await ((IDisplayDriver<WorkOrder>)CreateDriver(services)).BuildEditorAsync(workOrder, PostedFormUpdateModel.CreateContext(null));

            return Assert.Single(await DisplayResultModels.BuildAsync<WorkOrderEditViewModel>(result));
        }, commit: false);

        // Assert
        Assert.Equal("wo-1", model.WorkOrderId);
        Assert.Equal("Boiler service", model.TypeName);
        Assert.Equal("No heat", model.Summary);
        Assert.Equal("Dana Reyes", model.ContactName);
        Assert.Equal("1 Main St", model.AddressLine1);
        Assert.Equal(40.1, model.Latitude);
        Assert.Equal(new DateTime(2026, 11, 6, 9, 0, 0), model.Earliest);
        Assert.Equal(["Filter", null, null], model.Lines.Select(line => line.Description));
        Assert.Equal(2, model.Lines[0].Quantity);
    }

    [Fact]
    public async Task Driver_AppliesThePostedFields_KeepsUsedLines_AndTakesAPinnedPlace()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var workOrder = new WorkOrder
        {
            TerritoryId = "territory-1",
            ServiceAddress = new Address { AddressLine1 = "1 Main St", City = "Springfield" },
            Lines =
            [
                new WorkOrderLine { Id = "used", Description = "Valve", QuantityEstimated = 1, QuantityUsed = 1 },
                new WorkOrderLine { Id = "planned", Description = "Labor", QuantityEstimated = 1 },
            ],
        };
        var posted = new WorkOrderEditViewModel
        {
            Summary = "  No heat ",
            Description = " ",
            EstimatedMinutes = -5,
            ContactName = " Dana Reyes ",
            AddressLine1 = "1 Main St",
            City = "Springfield",
            Latitude = 40.1234567,
            Longitude = -75.2,
            Earliest = new DateTime(2026, 11, 6, 9, 0, 0),
            AssetIds = ["asset-1", "asset-1", string.Empty],
            Lines =
            [
                new LineEditModel { Id = "planned", Description = " Labor ", Quantity = 2, UnitPrice = 80 },
                new LineEditModel { Description = "Filter", Quantity = 1, UnitPrice = 15 },
                new LineEditModel(),
            ],
        };
        var context = PostedFormUpdateModel.CreateContext(posted);

        // Act
        await harness.RunAsync(services => ((IDisplayDriver<WorkOrder>)CreateDriver(services)).UpdateEditorAsync(workOrder, context), commit: false);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal("No heat", workOrder.Summary);
        Assert.Null(workOrder.Description);
        Assert.Equal(0, workOrder.EstimatedMinutes);
        Assert.Equal("Dana Reyes", workOrder.Contact.Name);
        Assert.Equal(new DateTime(2026, 11, 6, 14, 0, 0), workOrder.EarliestUtc);
        Assert.Equal(["asset-1"], workOrder.AssetIds);

        // The cleared row that already used parts stays: what was used is a record.
        Assert.Equal(["Labor", "Filter", "Valve"], workOrder.Lines.Select(line => line.Description));
        Assert.Equal(2, workOrder.Lines[0].QuantityEstimated);

        // Placed by hand on the map: the pin is where the job is, and the territory is found again.
        Assert.Equal(40.123457, workOrder.ServiceAddress.Latitude);
        Assert.Equal("Pin", workOrder.ServiceAddress.GeocodeSource);
        Assert.Equal(_now, workOrder.ServiceAddress.GeocodedUtc);
        Assert.Null(workOrder.TerritoryId);
    }

    [Fact]
    public void TheBuiltInDriver_IsRegistered()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new global::CrestApps.OrchardCore.WorkOrders.Startup().ConfigureServices(services);

        // Assert
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IDisplayDriver<WorkOrder>) && descriptor.ImplementationType == typeof(WorkOrderDisplayDriver));
    }

    [Fact]
    public async Task Editor_ShowsTheSectionOfADriverAnotherModuleRegisters_AfterTheBuiltInFields()
    {
        // Arrange
        await using var harness = await CreateAsync(services => services
            .AddEditorDisplayManagement()
            .AddScoped<IDisplayDriver<WorkOrder>>(provider => CreateDriver(provider))
            .AddDisplayDriver<WorkOrder, ExtraSectionDisplayDriver<WorkOrder>>());

        // Act
        var shapes = await harness.RunAsync(async services =>
        {
            var editor = await services.GetRequiredService<IDisplayManager<WorkOrder>>().BuildEditorAsync(new WorkOrder { Summary = "No heat" }, new PostedFormUpdateModel(null), isNew: true);

            return EditorDisplayManagement.ZoneShapes(editor);
        }, commit: false);

        // Assert
        Assert.Equal(["WorkOrderFields_Edit", ExtraSectionDisplayDriver<WorkOrder>.ShapeType], shapes.Select(shape => shape.Metadata.Type));
        Assert.Equal("No heat", Assert.IsAssignableFrom<WorkOrderEditViewModel>(shapes[0]).Summary);
        Assert.Equal(nameof(WorkOrder), shapes[0].Metadata.Prefix);
        Assert.Equal(ExtraSectionDisplayDriver<WorkOrder>.Note, Assert.IsAssignableFrom<ExtraSectionViewModel>(shapes[1]).Note);
    }

    [Fact]
    public async Task Editor_ReportsTheErrorOfAnotherModulesSection_UnderItsPrefix()
    {
        // Arrange
        await using var harness = await CreateAsync(services => services
            .AddEditorDisplayManagement()
            .AddScoped<IDisplayDriver<WorkOrder>>(provider => CreateDriver(provider))
            .AddDisplayDriver<WorkOrder, RefusingSectionDisplayDriver<WorkOrder>>());
        var updater = new PostedFormUpdateModel(new WorkOrderEditViewModel { Summary = "No heat" });
        var workOrder = new WorkOrder();

        // Act
        await harness.RunAsync(services => services.GetRequiredService<IDisplayManager<WorkOrder>>().UpdateEditorAsync(workOrder, updater, isNew: true), commit: false);

        // Assert
        Assert.Equal("No heat", workOrder.Summary);
        Assert.Equal([RefusingSectionDisplayDriver<WorkOrder>.Error], updater.ModelState["WorkOrder.Note"].Errors.Select(error => error.ErrorMessage));
    }

    private static WorkOrderDisplayDriver CreateDriver(IServiceProvider services)
    {
        var clock = new Mock<IClock>();

        clock.SetupGet(value => value.UtcNow).Returns(_now);

        // The editor shows times five hours behind UTC.
        var localClock = new Mock<ILocalClock>();

        localClock.Setup(value => value.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync((DateTimeOffset utc) => new DateTimeOffset(DateTime.SpecifyKind(utc.UtcDateTime.AddHours(-5), DateTimeKind.Unspecified), TimeSpan.FromHours(-5)));
        localClock.Setup(value => value.ConvertToUtcAsync(It.IsAny<DateTime>())).ReturnsAsync((DateTime local) => local.AddHours(5));

        return new WorkOrderDisplayDriver(
            services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>(),
            new WorkOrderLookups([]),
            new WorkOrderCustomers(new PartyDirectory([]), services.GetRequiredService<ISession>(), []),
            localClock.Object,
            clock.Object,
            services);
    }

    private static Task<string> AddTypeAsync(CalendarTestHarness harness)
        => harness.RunAsync(async services =>
        {
            var types = services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>();
            var type = await types.NewAsync();

            type.Name = "Boiler service";
            await types.CreateAsync(type);

            return type.ItemId;
        });

    private static Task<CalendarTestHarness> CreateAsync(Action<IServiceCollection> configure = null)
        => CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddWorkOrdersCore();
                configure?.Invoke(services);
            },
            WorkOrdersIndexSchema.CreateAsync,
            [new WorkOrderIndexProvider(), new WorkOrderTypeIndexProvider(), new ChecklistTemplateIndexProvider(), new ServiceLevelPolicyIndexProvider(), new WorkOrderSubStatusIndexProvider()],
            [WorkOrdersConstants.CollectionName]);
}
