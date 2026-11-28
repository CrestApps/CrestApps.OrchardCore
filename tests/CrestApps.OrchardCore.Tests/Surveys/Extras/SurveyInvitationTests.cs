using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Outreach.Core.Services;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Invitations;
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
        // Act
        var first = SurveyInvitationTokens.Create("one", "i1");
        var again = SurveyInvitationTokens.Create("one", "i1");
        var otherSecret = SurveyInvitationTokens.Create("two", "i1");

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

    [Fact]
    public async Task Invite_OptedOutAddress_IsNotSentAndIsMarkedOptedOut()
    {
        // Arrange: the Invitations feature's chain, Outreach's consent policy over the survey invitations source.
        var sender = new RecordingSender();
        await using var harness = await RuntimeTestHarness.CreateAsync(services =>
        {
            services.AddHttpContextAccessor();
            services.AddSurveysInvitations();
            services.AddSingleton<ISurveyInvitationSender>(sender);
            services.AddSurveyInvitationRecipientPolicy<OutreachSurveyInvitationRecipientPolicy>();
            services.AddOutreachRecipientPolicy<ConsentOutreachRecipientPolicy>();
            services.AddScoped<SurveyInvitationOptOutStore>();
            services.AddConsentSource<SurveyInvitationConsentSource>();
        });

        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<IConsentService>().RecordAsync(ConsentChannels.Email, "Gone@Example.com ", new ConsentChange
            {
                OptedOut = true,
                Source = SurveyInvitationConsentSource.SourceName,
            });

            return true;
        });

        // Act
        var optedOut = await harness.RunAsync(services => services.GetRequiredService<ISurveyInvitationService>().InviteAsync(new SurveyInvitationRequest
        {
            SurveyId = survey.ItemId,
            Email = "gone@example.com",
        }));

        var welcome = await harness.RunAsync(services => services.GetRequiredService<ISurveyInvitationService>().InviteAsync(new SurveyInvitationRequest
        {
            SurveyId = survey.ItemId,
            Email = "stays@example.com",
        }));

        var stored = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().FindByIdAsync(optedOut.Invitation.ItemId).AsTask());

        // Assert
        Assert.Single(sender.Sent);
        Assert.Equal(welcome.Link, sender.Sent[0]);
        Assert.Equal(SurveyInvitationStatus.OptedOut, stored.Status);
    }

    [Fact]
    public void UnsubscribeLink_FollowsTheInvitationLink()
        => Assert.Equal("https://example.com/s/i/abc/unsubscribe", SurveyInvitationSender.UnsubscribeLink("https://example.com/s/i/abc"));

    private sealed class RecordingSender : ISurveyInvitationSender
    {
        public List<string> Sent { get; } = [];

        public bool CanSendEmail => true;

        public Task<SurveyInvitationDelivery> SendAsync(Survey survey, SurveyCollector collector, SurveyInvitation invitation, string link, bool isReminder, CancellationToken cancellationToken = default)
        {
            Sent.Add(link);

            return Task.FromResult(SurveyInvitationDelivery.Delivered);
        }
    }
}
