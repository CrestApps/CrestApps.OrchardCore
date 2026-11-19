using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Handlers;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// A category moved under another parent takes its tickets along: publishing the taxonomy with a category's path
/// changed asks for the paged step that brings the tickets' paths up to date (a rename changes no path and asks for
/// nothing), and once it has run the list's "with sub-categories" filter on the new parent finds the tickets, on real
/// YesSql and SQLite.
/// </summary>
public sealed class TicketCategoryPathsTests
{
    [Fact]
    public void AMove_ChangesPaths_ButARenameDoesNot()
    {
        // Arrange: Laptop under Hardware; then under Devices; then Hardware renamed.
        var before = Taxonomy(Term("hardware", "Hardware", Term("laptop", "Laptop")), Term("devices", "Devices"));
        var moved = Taxonomy(Term("hardware", "Hardware"), Term("devices", "Devices", Term("laptop", "Laptop")));
        var renamed = Taxonomy(Term("hardware", "Computers", Term("laptop", "Laptop")), Term("devices", "Devices"));

        // Act and assert
        Assert.True(TicketCategoryTaxonomyHandler.PathsChanged(before, moved));
        Assert.False(TicketCategoryTaxonomyHandler.PathsChanged(before, renamed));
    }

    [Fact]
    public async Task AfterAMove_TheFilterOnTheNewParent_FindsTheTickets()
    {
        // Arrange: t1 was filed under Hardware > Laptop; Laptop now sits under Devices.
        var categories = new Mock<ITicketCategoryService>();
        categories.Setup(service => service.FindAsync(It.IsAny<string>(), "laptop", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketCategory { Id = "laptop", Name = "Laptop", Path = "/devices/laptop/" });
        await using var harness = await TicketReportDataSourceTests.CreateAsync(services =>
        {
            // The list reads only the index; the ticket service's other services aren't used.
            services.AddSingleton(categories.Object);
            services.AddSingleton(Mock.Of<CrestApps.OrchardCore.Timeline.Services.ITimelineService>());
            services.AddSingleton(Mock.Of<CrestApps.OrchardCore.ServiceLevels.Services.IServiceLevelTracker>());
            services.AddSingleton(Mock.Of<CrestApps.OrchardCore.Parties.Services.IPartyDirectory>());
            services.AddSingleton(Mock.Of<CrestApps.OrchardCore.Numbering.INumberSequence>());
        });
        await harness.RunAsync(async services =>
        {
            var ticket = new ContentItem { ContentItemId = "t1", ContentType = TicketsConstants.DefaultContentType, Latest = true, Published = true, DisplayText = "Laptop won't start", CreatedUtc = CalendarTestHarness.Now };
            ticket.Apply(new TicketPart { Number = "T-1", DeskId = "support", StatusId = "open", StatusCategory = TicketStatusCategory.Open, PriorityId = "normal", CategoryId = "laptop", CategoryPath = "/hardware/laptop/" });
            await services.GetRequiredService<ISession>().SaveAsync(ticket);

            return true;
        });
        var underDevicesBefore = await CountUnderAsync(harness, "devices");

        // Act
        var done = await harness.RunAsync(services => ActivatorUtilities.CreateInstance<TicketCategoryPathsStep>(services).RunAsync(new ResumableStepContext()));

        // Assert
        Assert.True(done);
        Assert.Equal(0, underDevicesBefore);
        Assert.Equal(1, await CountUnderAsync(harness, "devices"));
        Assert.Equal(0, await CountUnderAsync(harness, "hardware"));
    }

    // The list's "with sub-categories" filter: the ticket's path holds the category's id.
    private static Task<int> CountUnderAsync(CalendarTestHarness harness, string categoryId)
        => harness.RunAsync(async services =>
        {
            var list = await services.GetRequiredService<ITicketService>().ListAsync(new TicketScopedQuery
            {
                Query = new TicketQuery { CategoryId = categoryId, Take = 50 },
                AllDeskIds = ["support"],
            });

            return list.Total;
        }, commit: false);

    private static ContentItem Taxonomy(params JsonObject[] terms)
    {
        var taxonomy = new ContentItem { ContentItemId = "categories", ContentType = "Taxonomy" };
        ((JsonObject)taxonomy.Content)["TaxonomyPart"] = new JsonObject { ["Terms"] = new JsonArray(terms) };

        return taxonomy;
    }

    private static JsonObject Term(string id, string name, params JsonObject[] children)
    {
        var term = new JsonObject { ["ContentItemId"] = id, ["DisplayText"] = name, ["ContentType"] = TicketsConstants.CategoryTermType };

        if (children.Length > 0)
        {
            term["Terms"] = new JsonArray(children);
        }

        return term;
    }
}
