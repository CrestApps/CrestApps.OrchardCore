using System.Security.Claims;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Chat.Controllers;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// The AI usage page end to end, short of rendering: the filters the owner chooses reach both halves of the report.
/// </summary>
public sealed class UsageAnalyticsControllerTests
{
    [Fact]
    public async Task TheReport_AppliesTheProfileFilterAndBothGroupings_ToCompletionsAndVoiceCalls()
    {
        // Arrange
        var harness = new ControllerHarness();

        // Act
        var result = await harness.Controller.IndexPost(new UsageAnalyticsIndexViewModel
        {
            ProfileId = "profile-1",
            GroupBy = AICompletionUsageGroupBy.Deployment,
            VoiceGroupBy = AIVoiceUsageGroupBy.Engine,
        });

        // Assert
        var model = Assert.IsType<UsageAnalyticsIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.ShowReport);

        // Completions: only profile-1's, grouped by deployment.
        var row = Assert.Single(model.Rows);
        Assert.Equal("chat-main", row.GroupLabel);
        Assert.Equal(30, model.TotalTokens);

        // Voice: only profile-1's call, grouped by engine, with its session's text tokens.
        Assert.Equal(1, model.VoiceTotals.Calls);
        Assert.Equal(30, model.VoiceTotals.TextTokens);
        Assert.Equal("Realtime", Assert.Single(model.VoiceRows).Label);

        // The filter can be changed again from the page it produced.
        Assert.Equal(["Sales follow-up", "Service reminders"], model.Profiles.Select(profile => profile.Text));
    }

    [Fact]
    public async Task ThePage_OffersEveryChatProfileToFilterBy()
    {
        // Arrange
        var harness = new ControllerHarness();

        // Act
        var result = await harness.Controller.Index();

        // Assert
        var model = Assert.IsType<UsageAnalyticsIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.ShowReport);
        Assert.Equal(["profile-2", "profile-1"], model.Profiles.Select(profile => profile.Value));
    }

    private sealed class ControllerHarness
    {
        public ControllerHarness()
        {
            var usageService = new Mock<IAICompletionUsageService>();
            usageService
                .Setup(service => service.GetAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new AICompletionUsageRecord { SessionId = "session-1", ProfileId = "profile-1", DeploymentName = "chat-main", TotalTokenCount = 30 },
                    new AICompletionUsageRecord { SessionId = "session-2", ProfileId = "profile-2", DeploymentName = "chat-small", TotalTokenCount = 70 },
                ]);

            var voiceStore = new Mock<IAIVoiceSessionSummaryStore>();
            voiceStore
                .Setup(store => store.GetAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new AIVoiceSessionSummaryIndex { ActivityId = "activity-1", AISessionId = "session-1", AIProfileId = "profile-1", Engine = nameof(AIVoiceSessionEngine.Realtime), Outcome = nameof(AIVoiceSessionOutcome.CompletedByAI), SessionDurationMs = 60_000 },
                    new AIVoiceSessionSummaryIndex { ActivityId = "activity-2", AISessionId = "session-2", AIProfileId = "profile-2", Engine = nameof(AIVoiceSessionEngine.TurnBased), Outcome = nameof(AIVoiceSessionOutcome.CompletedByAI), SessionDurationMs = 30_000 },
                ]);

            var profileStore = new Mock<IAIProfileStore>();
            profileStore
                .Setup(store => store.GetByTypeAsync(AIProfileType.Chat))
                .ReturnsAsync(
                [
                    new AIProfile { ItemId = "profile-1", Name = "service", DisplayText = "Service reminders" },
                    new AIProfile { ItemId = "profile-2", Name = "sales", DisplayText = "Sales follow-up" },
                ]);

            var authorizationService = new Mock<IAuthorizationService>();
            authorizationService
                .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(AuthorizationResult.Success());

            var options = new Mock<IOptionsMonitor<GeneralAIOptions>>();
            options.Setup(monitor => monitor.CurrentValue).Returns(new GeneralAIOptions { EnableAIUsageTracking = true });

            var clock = new Mock<IClock>();
            clock
                .Setup(value => value.ConvertToTimeZone(It.IsAny<DateTimeOffset>(), It.IsAny<ITimeZone>()))
                .Returns((DateTimeOffset value, ITimeZone _) => value);

            var localClock = new Mock<ILocalClock>();
            localClock.Setup(value => value.GetLocalTimeZoneAsync()).ReturnsAsync(Mock.Of<ITimeZone>());

            Controller = new UsageAnalyticsController(
                usageService.Object,
                voiceStore.Object,
                profileStore.Object,
                authorizationService.Object,
                localClock.Object,
                clock.Object,
                options.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test")),
                    },
                },
            };
        }

        public UsageAnalyticsController Controller { get; }
    }
}
