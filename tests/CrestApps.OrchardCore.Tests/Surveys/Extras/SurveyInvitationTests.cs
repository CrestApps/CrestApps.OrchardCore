using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Surveys.Extras;

public sealed class SurveyInvitationTests
{
    [Fact]
    public void Token_SameInvitation_IsStableAndDiffersBySurveySecret()
    {
        // Arrange
        var survey = new Survey { ItemId = "s1", AnonymitySecret = "one" };
        var other = new Survey { ItemId = "s1", AnonymitySecret = "two" };

        // Act
        var first = SurveyInvitationTokens.Create(survey, "i1");
        var again = SurveyInvitationTokens.Create(survey, "i1");
        var otherSecret = SurveyInvitationTokens.Create(other, "i1");

        // Assert
        Assert.Equal(first, again);
        Assert.NotEqual(first, otherSecret);
        Assert.Equal(64, SurveyInvitationTokens.Hash(first).Length);
    }

    [Theory]
    [InlineData(SurveyInvitationStatus.Sent, 0, 4, true)]
    [InlineData(SurveyInvitationStatus.Sent, 0, 2, false)]
    [InlineData(SurveyInvitationStatus.Opened, 2, 10, false)]
    [InlineData(SurveyInvitationStatus.Completed, 0, 10, false)]
    [InlineData(SurveyInvitationStatus.Pending, 0, 10, false)]
    public void IsReminderDue_FollowsTheCountAndInterval(SurveyInvitationStatus status, int sent, int daysSince, bool expected)
    {
        // Arrange
        var now = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
        var invitation = new SurveyInvitation { Status = status, RemindersSent = sent, SentUtc = now.AddDays(-daysSince) };
        var settings = new SurveyInvitationSettings { ReminderCount = 2, ReminderEveryDays = 3 };

        // Act
        var due = SurveyInvitationService.IsReminderDue(invitation, settings, now);

        // Assert
        Assert.Equal(expected, due);
    }

    [Fact]
    public async Task InvitationLink_StartsOneResponse_AndCompletesTheInvitation()
    {
        // Arrange
        var sender = new RecordingSender();
        await using var harness = await RuntimeTestHarness.CreateAsync(services =>
        {
            services.AddHttpContextAccessor();
            services.AddSurveysInvitations();
            services.AddSingleton<ISurveyInvitationSender>(sender);
        });

        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        // Act
        var invited = await harness.RunAsync(services => services.GetRequiredService<ISurveyInvitationService>().InviteAsync(new SurveyInvitationRequest
        {
            SurveyId = survey.ItemId,
            Email = "guest@example.com",
        }));

        var token = invited.Link.Substring(invited.Link.LastIndexOf('/') + 1);
        var started = await harness.StartAsync(survey, configure: request => request.InvitationToken = Uri.UnescapeDataString(token));
        await harness.PostAsync(started.Response, new SurveyRespondentIdentity { ResumeToken = started.ResumeToken }, SurveyPageDirection.Finish, ("q1", "hi"));
        var again = await harness.StartAsync(survey, configure: request => request.InvitationToken = Uri.UnescapeDataString(token));
        var invitation = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().FindByIdAsync(invited.Invitation.ItemId).AsTask());

        // Assert
        Assert.True(invited.Succeeded);
        Assert.Single(sender.Sent);
        Assert.StartsWith("/s/i/", invited.Link, StringComparison.Ordinal);
        Assert.True(started.Succeeded);
        Assert.Equal(invited.Invitation.ItemId, started.Response.InvitationId);
        Assert.Equal(SurveyInvitationStatus.Completed, invitation.Status);
        Assert.Equal(SurveyErrorCode.InvitationUsed, again.Error);
    }

    [Fact]
    public async Task Invite_SameRecipientTwice_RenewsTheOpenInvitation()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync(services =>
        {
            services.AddHttpContextAccessor();
            services.AddSurveysInvitations();
        });

        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        // Act
        var first = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, UserId = "u1" }));
        var second = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, UserId = "u1" }));

        // Assert
        Assert.Equal(first.Invitation.ItemId, second.Invitation.ItemId);
        Assert.Equal(first.Link, second.Link);
    }

    private sealed class RecordingSender : ISurveyInvitationSender
    {
        public List<string> Sent { get; } = [];

        public Task<bool> SendAsync(Survey survey, SurveyCollector collector, SurveyInvitation invitation, string link, bool isReminder, CancellationToken cancellationToken = default)
        {
            Sent.Add(link);

            return Task.FromResult(true);
        }
    }
}
