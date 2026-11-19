using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Reports.DataSources;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DataSources;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The ticket category everywhere the intake plan named it, on real YesSql and SQLite: the daily backlog breaks the open
/// tickets down by category; the <c>TicketCategories</c> data set lists each category once with its place in the tree
/// and its routing, only to a reader with a ticket scope; and workflow events narrowed to a category start for its
/// tickets and those of the categories below it, and to a ticket type for that type only.
/// </summary>
public sealed class TicketCategoryReportingTests
{
    private static readonly TicketCategory _hardware = new() { Id = "hardware", Name = "Hardware", Level = 0, PathText = "Hardware", Path = "/hardware/", HasChildren = true, DeskId = "support" };
    private static readonly TicketCategory _laptop = new() { Id = "laptop", Name = "Laptop", ParentId = "hardware", Level = 1, PathText = "Hardware > Laptop", Path = "/hardware/laptop/", DeskId = "support", PriorityId = "normal" };

    [Fact]
    public async Task TheBacklog_BreaksTheOpenTicketsDownByCategory()
    {
        // Arrange: two open laptop tickets and one without a category, all on Support.
        await using var harness = await CreateAsync();
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            await session.SaveAsync(Ticket("t1", "laptop"));
            await session.SaveAsync(Ticket("t2", "laptop"));
            await session.SaveAsync(Ticket("t3", null));

            return true;
        });

        // Act
        var day = await harness.RunAsync(services => services.GetRequiredService<TicketBacklogRecorder>().TakeAsync("support", CalendarTestHarness.Now, TestContext.Current.CancellationToken), commit: false);

        // Assert
        Assert.Equal(2, day.Groups.Single(group => group.CategoryId == "laptop").Count);
        Assert.Equal(1, day.Groups.Single(group => group.CategoryId is null).Count);
    }

    [Fact]
    public async Task TheCategoriesDataSet_ListsEachCategoryOnce_OnlyToAReaderWithATicketScope()
    {
        // Arrange: two ticket types share the taxonomy.
        await using var harness = await CreateAsync();

        // Act
        var ana = await ReadAsync(harness, TicketReportDataSourceTests.User("ana"));
        var stranger = await ReadAsync(harness, TicketReportDataSourceTests.User("stranger"));

        // Assert
        Assert.Equal(["hardware", "laptop"], ana.Select(row => row["CategoryId"]));
        var laptop = ana[1];
        Assert.Equal(("Hardware > Laptop", "hardware", "Hardware", 1, "Support", "Normal"), ((string)laptop["PathText"], (string)laptop["ParentId"], (string)laptop["Parent"], Convert.ToInt32(laptop["Level"], System.Globalization.CultureInfo.InvariantCulture), (string)laptop["Desk"], (string)laptop["Priority"]));
        Assert.Empty(stranger);
    }

    [Fact]
    public void AnEventNarrowedToACategory_StartsForItsSubCategoriesToo_AndToATypeForThatTypeOnly()
    {
        // Arrange
        var underHardware = new Dictionary<string, object> { [TicketWorkflowInput.CategoryPath] = "/hardware/laptop/", [TicketWorkflowInput.ContentType] = "Ticket" };
        var noCategory = new Dictionary<string, object> { [TicketWorkflowInput.ContentType] = "Incident" };

        // Act and assert
        Assert.True(TicketEventActivity.InCategories(["hardware"], underHardware));
        Assert.True(TicketEventActivity.InCategories(["laptop"], underHardware));
        Assert.False(TicketEventActivity.InCategories(["software"], underHardware));
        Assert.False(TicketEventActivity.InCategories(["hardware"], noCategory));
        Assert.True(TicketEventActivity.InCategories([], noCategory));
    }

    private static Task<CalendarTestHarness> CreateAsync()
        => TicketReportDataSourceTests.CreateAsync(services =>
        {
            var categories = new Mock<ITicketCategoryService>();
            categories.Setup(service => service.ListAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync([_hardware, _laptop]);
            services.AddSingleton(categories.Object);

            var definitions = new Mock<IContentDefinitionManager>();
            definitions.Setup(manager => manager.ListTypeDefinitionsAsync()).ReturnsAsync([TicketType("Ticket"), TicketType("Incident")]);
            services.AddSingleton(definitions.Object);
        });

    private static async Task<List<Dictionary<string, object>>> ReadAsync(CalendarTestHarness harness, ClaimsPrincipal user)
    {
        await using var scope = harness.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<TicketReportDataSource>();
        var table = await ReportDesignerTestServices.ReadAsync(source, new DataSourceQuery
        {
            DataSet = TicketReportDataSets.TicketCategories,
            Fields = new HashSet<string>(StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext { User = user },
        }, TestContext.Current.CancellationToken);

        return [.. table.Rows.Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))];
    }

    private static ContentTypeDefinition TicketType(string name)
        => new(name, name, [new ContentTypePartDefinition(TicketsConstants.PartName, new ContentPartDefinition(TicketsConstants.PartName), new JsonObject())], new JsonObject());

    private static ContentItem Ticket(string id, string categoryId)
    {
        var ticket = new ContentItem { ContentItemId = id, ContentType = TicketsConstants.DefaultContentType, Latest = true, Published = true, DisplayText = id, CreatedUtc = CalendarTestHarness.Now.AddHours(-2) };
        ticket.Apply(new TicketPart { Number = id, DeskId = "support", StatusId = "open", StatusCategory = TicketStatusCategory.Open, PriorityId = "normal", CategoryId = categoryId, CategoryPath = categoryId is null ? null : "/hardware/" + categoryId + "/" });

        return ticket;
    }
}
