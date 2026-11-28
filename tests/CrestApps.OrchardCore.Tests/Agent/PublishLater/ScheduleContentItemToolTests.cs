using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.PublishLater;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Json;
using OrchardCore.Modules;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Agent.PublishLater;

/// <summary>
/// The schedule tool sets the date of the Publish Later or Archive Later part from a local time, on a draft for a
/// publish and on the published item for an archive, and removes the date to cancel.
/// </summary>
public sealed class ScheduleContentItemToolTests
{
    private static readonly DateTime _now = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task InvokeAsync_ToPublishADraftLater_StoresTheUtcTimeAndSavesTheDraft()
    {
        // Arrange
        var harness = new Harness(published: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ScheduleContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "item-1",
            ["action"] = "publish",
            ["when"] = "2026-12-24T08:00",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("scheduled").GetBoolean());
        Assert.Equal("publish", json.GetProperty("action").GetString());
        Assert.Equal("2026-12-24T08:00:00+00:00", json.GetProperty("scheduledFor").GetString());
        Assert.Equal("UTC", json.GetProperty("timeZone").GetString());
        Assert.Equal(new DateTime(2026, 12, 24, 8, 0, 0, DateTimeKind.Utc), harness.Content["PublishLaterPart"]["ScheduledPublishUtc"].GetValue<DateTime>().ToUniversalTime());
        Assert.Equal("About us", harness.Content["TitlePart"]["Title"].GetValue<string>());
        Assert.Equal("owner-1", harness.ContentItem.Owner);
        harness.ContentManager.Verify(manager => manager.SaveDraftAsync(harness.ContentItem), Times.Once);
        harness.ContentManager.Verify(manager => manager.PublishAsync(It.IsAny<ContentItem>()), Times.Never);

        var check = Assert.Single(harness.Services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal("PublishContent", check.Permission);
        Assert.Same(harness.ContentItem, check.Resource);
    }

    [Fact]
    public async Task InvokeAsync_ToArchiveAPublishedItemLater_PublishesTheItemWithTheSchedule()
    {
        // Arrange
        var harness = new Harness(published: true);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ScheduleContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "item-1",
            ["action"] = "archive",
            ["when"] = "2026-12-31 23:59",
        });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("scheduled").GetBoolean());
        Assert.Equal(new DateTime(2026, 12, 31, 23, 59, 0, DateTimeKind.Utc), harness.Content["ArchiveLaterPart"]["ScheduledArchiveUtc"].GetValue<DateTime>().ToUniversalTime());
        harness.ContentManager.Verify(manager => manager.PublishAsync(harness.ContentItem), Times.Once);
        harness.ContentManager.Verify(manager => manager.SaveDraftAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_ToCancel_RemovesTheScheduledTime()
    {
        // Arrange
        var harness = new Harness(published: false, scheduledPublishUtc: "2026-12-24T08:00:00Z");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ScheduleContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "item-1",
            ["action"] = "publish",
            ["cancel"] = true,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.False(json.GetProperty("scheduled").GetBoolean());
        Assert.True(json.GetProperty("cancelled").GetBoolean());
        Assert.Null(harness.Content["PublishLaterPart"]["ScheduledPublishUtc"]);
        harness.ContentManager.Verify(manager => manager.SaveDraftAsync(harness.ContentItem), Times.Once);
    }

    [Theory]
    [InlineData(false, "publish", "2026-11-16T11:00", "is in the past")]
    [InlineData(false, "publish", "2026-12-24T08:00:00Z", "without a time zone")]
    [InlineData(true, "publish", "2026-12-24T08:00", "Publishing is scheduled on a draft")]
    [InlineData(false, "archive", "2026-12-24T08:00", "Archiving is scheduled on a published item")]
    [InlineData(false, "delete", "2026-12-24T08:00", "action must be publish or archive")]
    public async Task InvokeAsync_WhenTheScheduleCannotBeSet_SaysWhyAndSavesNothing(bool published, string action, string when, string expected)
    {
        // Arrange
        var harness = new Harness(published);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ScheduleContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "item-1",
            ["action"] = action,
            ["when"] = when,
        });

        // Assert
        Assert.StartsWith("Nothing was scheduled", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        harness.VerifyNothingSaved();
    }

    [Fact]
    public async Task InvokeAsync_WhenTheTypeHasNoSchedulingPart_SaysWhichPartIsMissing()
    {
        // Arrange
        var harness = new Harness(published: false, withParts: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ScheduleContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "item-1",
            ["action"] = "publish",
            ["when"] = "2026-12-24T08:00",
        });

        // Assert
        Assert.Contains("does not have the Publish Later part", result, StringComparison.Ordinal);
        harness.VerifyNothingSaved();
    }

    [Fact]
    public async Task InvokeAsync_WhenTheItemIsNotValid_CancelsTheChange()
    {
        // Arrange
        var harness = new Harness(published: false);
        var failure = new ContentValidateResult();
        failure.Fail(new ValidationResult("The title is required."));
        harness.ContentManager.Setup(manager => manager.ValidateAsync(harness.ContentItem)).ReturnsAsync(failure);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ScheduleContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "item-1",
            ["action"] = "publish",
            ["when"] = "2026-12-24T08:00",
        });

        // Assert
        Assert.StartsWith("Nothing was scheduled", result, StringComparison.Ordinal);
        Assert.Contains("The title is required.", result, StringComparison.Ordinal);
        harness.Session.Verify(session => session.CancelAsync(), Times.Once);
        harness.ContentManager.Verify(manager => manager.SaveDraftAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    private sealed class Harness
    {
        public Harness(bool published, string scheduledPublishUtc = null, bool withParts = true)
        {
            var typeBuilder = new ContentTypeDefinitionBuilder().WithName("Page").WithPart("TitlePart");

            if (withParts)
            {
                typeBuilder.WithPart("PublishLaterPart").WithPart("ArchiveLaterPart");
            }

            var typeDefinition = typeBuilder.Build();
            var scheduled = scheduledPublishUtc is null
                ? "null"
                : $"\"{scheduledPublishUtc}\"";

            ContentItem = JsonSerializer.Deserialize<ContentItem>(
                $$"""
                {
                  "ContentItemId": "item-1",
                  "ContentItemVersionId": "version-1",
                  "ContentType": "Page",
                  "DisplayText": "About",
                  "Latest": true,
                  "Published": {{(published ? "true" : "false")}},
                  "Owner": "owner-1",
                  "TitlePart": {
                    "Title": "About us"
                  },
                  "PublishLaterPart": {
                    "ScheduledPublishUtc": {{scheduled}}
                  }
                }
                """,
                new DocumentJsonSerializerOptions().SerializerOptions);

            ContentManager.Setup(manager => manager.GetAsync("item-1", It.IsAny<VersionOptions>())).ReturnsAsync(ContentItem);
            ContentManager.Setup(manager => manager.ValidateAsync(It.IsAny<ContentItem>())).ReturnsAsync(new ContentValidateResult());

            var contentDefinitionManager = new Mock<IContentDefinitionManager>();
            contentDefinitionManager.Setup(manager => manager.GetTypeDefinitionAsync("Page")).ReturnsAsync(typeDefinition);

            var clock = new Mock<IClock>();
            clock.Setup(value => value.UtcNow).Returns(_now);

            var services = AgentToolTestContext.CreateServices();

            services.AddSingleton(ContentManager.Object);
            services.AddSingleton(contentDefinitionManager.Object);
            services.AddSingleton(clock.Object);
            services.AddSingleton(Session.Object);

            Services = services.BuildServiceProvider();
        }

        public ContentItem ContentItem { get; }

        public Mock<IContentManager> ContentManager { get; } = new();

        public Mock<YSession> Session { get; } = new();

        public ServiceProvider Services { get; }

        public JsonObject Content
            => (JsonObject)ContentItem.Content;

        public void VerifyNothingSaved()
        {
            ContentManager.Verify(manager => manager.GetAsync(It.IsAny<string>(), VersionOptions.DraftRequired), Times.Never);
            ContentManager.Verify(manager => manager.UpdateAsync(It.IsAny<ContentItem>()), Times.Never);
            ContentManager.Verify(manager => manager.SaveDraftAsync(It.IsAny<ContentItem>()), Times.Never);
            ContentManager.Verify(manager => manager.PublishAsync(It.IsAny<ContentItem>()), Times.Never);
        }
    }
}
