using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Resources.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Core;
using CrestApps.OrchardCore.Timeline.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Deployments;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Recipes;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;
using YesSql;
using ISession = YesSql.ISession;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Timeline;

/// <summary>
/// Moving timelines between sites and finding records: the recipe step imports through the service (checked, stamped,
/// nothing announced, an entry already stored left alone, a refused one reported); the deployment step exports every
/// entry of the kinds it names, oldest first; and the content item search matches names that start with the text.
/// </summary>
public sealed class TimelinePortabilityAndRecordSourceTests
{
    private static readonly RecordRef _order = new("Order", "o1");

    [Fact]
    public async Task TheRecipeStep_ImportsThroughTheService_AndReportsARefusedEntry()
    {
        // Arrange
        await using var harness = await TimelineAsync();
        await harness.RunAsync(services => services.GetRequiredService<ITimelineService>().ImportAsync(new TimelineEntry
        {
            ItemId = "4existingentry000000000000",
            Subject = _order,
            Kind = TimelineConstants.Kinds.Note,
            Body = "Already here.",
        }));

        var context = new RecipeExecutionContext
        {
            Name = TimelineConstants.RecipeStepName,
            Step = JsonNode.Parse("""
                {
                  "name": "TimelineEntries",
                  "Entries": [
                    { "ItemId": "4existingentry000000000000", "Subject": "Order:o1", "Kind": "Note", "Body": "Changed in the recipe." },
                    { "Subject": "Order:o1", "Kind": "Note", "Body": "From the recipe." },
                    { "Subject": "Order:o1", "Kind": "Note" }
                  ]
                }
                """).AsObject(),
        };

        // Act
        await harness.RunAsync(services => new TimelineEntriesRecipeStep(services.GetRequiredService<ITimelineService>()).ExecuteAsync(context));
        var page = await harness.RunAsync(services => services.GetRequiredService<ITimelineService>().ListAsync(new TimelineQuery { Subject = _order, IncludeInternal = true }), commit: false);

        // Assert
        Assert.Equal(2, page.Total);
        Assert.Contains(page.Entries, entry => entry.Body == "Already here.");
        Assert.All(page.Entries, entry => Assert.NotEqual(default, entry.CreatedUtc));
        Assert.Single(context.Errors);
    }

    [Fact]
    public async Task TheDeploymentStep_ExportsEveryEntryOfItsKinds_OldestFirst()
    {
        // Arrange
        await using var harness = await TimelineAsync();
        await harness.RunAsync(async services =>
        {
            var timeline = services.GetRequiredService<ITimelineService>();
            await timeline.ImportAsync(new TimelineEntry { Subject = _order, Kind = TimelineConstants.Kinds.Note, Body = "Second", CreatedUtc = CalendarTestHarness.Now });
            await timeline.ImportAsync(new TimelineEntry { Subject = _order, Kind = TimelineConstants.Kinds.Note, Body = "First", CreatedUtc = CalendarTestHarness.Now.AddDays(-1) });
            await timeline.ImportAsync(new TimelineEntry { Subject = new RecordRef("WorkOrder", "wo1"), Kind = TimelineConstants.Kinds.Note, Body = "Other kind" });
        });
        var result = new DeploymentPlanResult(new NullFileBuilder(), new RecipeDescriptor());

        // Act
        await harness.RunAsync(services => new TimelineEntriesDeploymentSource(services.GetRequiredService<ISession>())
            .ProcessDeploymentStepAsync(new TimelineEntriesDeploymentStep { SubjectKinds = "Order" }, result), commit: false);

        // Assert
        var step = Assert.IsType<JsonObject>(Assert.Single(result.Steps));
        var bodies = step[TimelineEntriesRecipeStep.EntriesName]!.AsArray().Select(entry => entry!["Body"]!.GetValue<string>());
        Assert.Equal(["First", "Second"], bodies);
    }

    [Fact]
    public async Task TheContentItemSearch_MatchesNamesThatStartWithTheText()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync(
            createSchema: schemaBuilder => schemaBuilder.CreateMapIndexTableAsync<ContentItemIndex>(table => table
                .Column<string>("ContentItemId", column => column.WithLength(26))
                .Column<string>("ContentItemVersionId", column => column.WithLength(26))
                .Column<bool>("Published")
                .Column<bool>("Latest")
                .Column<string>("ContentType", column => column.WithLength(255))
                .Column<DateTime>("ModifiedUtc")
                .Column<DateTime>("PublishedUtc")
                .Column<DateTime>("CreatedUtc")
                .Column<string>("Owner", column => column.WithLength(255))
                .Column<string>("Author", column => column.WithLength(255))
                .Column<string>("DisplayText", column => column.WithLength(255))),
            indexProviders: [new ContentItemIndexProvider()]);

        var items = new[]
        {
            new ContentItem { ContentItemId = "c1", ContentItemVersionId = "v1", ContentType = "Contact", DisplayText = "Abbot Kinney", Latest = true, Published = true },
            new ContentItem { ContentItemId = "c2", ContentItemVersionId = "v2", ContentType = "Contact", DisplayText = "Cabana Club", Latest = true, Published = true },
        };

        await harness.RunAsync(async services =>
        {
            foreach (var item in items)
            {
                await services.GetRequiredService<ISession>().SaveAsync(item);
            }
        });

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((IEnumerable<string> ids, VersionOptions _) => [.. items.Where(item => ids.Contains(item.ContentItemId))]);

        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        // Act
        var found = await harness.RunAsync(services => new ContentItemRecordSource(
                services.GetRequiredService<ISession>(),
                contentManager.Object,
                authorization.Object,
                Mock.Of<LinkGenerator>(),
                new HttpContextAccessor())
            .SearchAsync(new RecordSearch { Text = "Ab", Count = 10, User = CalendarTestHarness.User("agent") }), commit: false);

        // Assert
        Assert.Equal(["Abbot Kinney"], found.Select(summary => summary.Title));
        Assert.Equal(RecordKinds.ContentItem, ContentItemRecordSource.RecordKind);
        Assert.Equal(RecordKinds.ContentItem, TimelineConstants.ContentItemKind);
    }

    private static Task<CalendarTestHarness> TimelineAsync()
        => CalendarTestHarness.CreateAsync(
            services => services.AddTimelineCore(),
            TimelineIndexSchema.CreateAsync,
            [new TimelineEntryIndexProvider()],
            [TimelineConstants.CollectionName]);

    private sealed class NullFileBuilder : IFileBuilder
    {
        public Task SetFileAsync(string subpath, Stream stream) => Task.CompletedTask;
    }
}
