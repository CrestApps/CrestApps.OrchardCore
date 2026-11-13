using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Records;

/// <summary>
/// Work orders are a record kind: a user who may see work orders finds them by number or summary, newest first, read
/// in the database, and any record that links to a work order shows its number, summary and status.
/// </summary>
public sealed class WorkOrderRecordSourceTests
{
    [Fact]
    public async Task ASearch_FindsWorkOrdersByNumberOrSummary_NewestFirst_ForAUserWhoMaySeeWorkOrders()
    {
        // Arrange
        await using var harness = await CreateAsync();

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<WorkOrderStore>();

            await store.CreateAsync(NewWorkOrder("w1", "WO-000001", "Boiler service", WorkOrderStatus.InProgress, 0), TestContext.Current.CancellationToken);
            await store.CreateAsync(NewWorkOrder("w2", "WO-000002", "Replace the pump", WorkOrderStatus.New, 1), TestContext.Current.CancellationToken);
            await store.CreateAsync(NewWorkOrder("w3", "WO-000003", "Boiler inspection", WorkOrderStatus.OnHold, 2), TestContext.Current.CancellationToken);
        });

        var viewer = CalendarTestHarness.User("viewer", WorkOrdersPermissions.ViewWorkOrders.Name);
        var stranger = CalendarTestHarness.User("stranger");

        // Act
        var result = await harness.RunAsync(async services =>
        {
            var directory = services.GetRequiredService<IRecordDirectory>();

            return (
                ByNumber: await directory.SearchAsync(WorkOrdersConstants.RecordKind, new RecordSearch { Text = "WO-000002", User = viewer }, TestContext.Current.CancellationToken),
                BySummary: await directory.SearchAsync(WorkOrdersConstants.RecordKind, new RecordSearch { Text = "Boiler", User = viewer }, TestContext.Current.CancellationToken),
                Newest: await directory.SearchAsync(WorkOrdersConstants.RecordKind, new RecordSearch { User = viewer, Count = 2 }, TestContext.Current.CancellationToken),
                Forbidden: await directory.SearchAsync(WorkOrdersConstants.RecordKind, new RecordSearch { Text = "Boiler", User = stranger }, TestContext.Current.CancellationToken),
                Summary: await directory.GetSummaryAsync(new RecordRef(WorkOrdersConstants.RecordKind, "w3"), TestContext.Current.CancellationToken),
                Missing: await directory.GetSummaryAsync(new RecordRef(WorkOrdersConstants.RecordKind, "missing"), TestContext.Current.CancellationToken));
        }, commit: false);

        // Assert
        var found = Assert.Single(result.ByNumber);

        Assert.Equal("w2", found.Id);
        Assert.Equal("WO-000002 Replace the pump", found.Title);
        Assert.Equal("New", found.Description);
        Assert.Equal("/Admin/work-orders/view/w2", found.AdminUrl);
        Assert.Equal(["w3", "w1"], result.BySummary.Select(record => record.Id));
        Assert.Equal(["w3", "w2"], result.Newest.Select(record => record.Id));
        Assert.Empty(result.Forbidden);
        Assert.Equal("WO-000003 Boiler inspection", result.Summary.Title);
        Assert.Equal("On hold", result.Summary.Description);
        Assert.Null(result.Missing);
    }

    private static WorkOrder NewWorkOrder(string id, string number, string summary, WorkOrderStatus status, int createdAfterMinutes)
        => new()
        {
            ItemId = id,
            Number = number,
            Summary = summary,
            Status = status,
            CreatedUtc = CalendarTestHarness.Now.AddMinutes(createdAfterMinutes),
        };

    private static Task<CalendarTestHarness> CreateAsync()
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>();

        httpContextAccessor.SetupGet(accessor => accessor.HttpContext).Returns(new DefaultHttpContext());

        // Only work orders are saved, so only their store and index are registered.
        return CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddScoped<WorkOrderStore>();
                services.AddSingleton<LinkGenerator>(new TemplateLinkGenerator((_, values) => $"/Admin/work-orders/view/{values["id"]}"));
                services.AddSingleton(httpContextAccessor.Object);
                services.AddRecordSource<WorkOrderRecordSource>();
            },
            WorkOrdersIndexSchema.CreateAsync,
            [new WorkOrderIndexProvider()],
            [WorkOrdersConstants.CollectionName]);
    }
}
