using CrestApps.Core;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Extras;

/// <summary>
/// The hourly invitation reminder task: a list whose save fails never takes the other lists' updates with it (R4-H1).
/// </summary>
public sealed class SurveyInvitationReminderTaskTests
{
    [Fact]
    public async Task OneListsSaveFails_TheOtherListsRemindersAreStillRecorded()
    {
        // Arrange: two surveys with an invitation list each; the reminder of one list's invitation fails at save time.
        var sender = new RecordingSender();
        var poisonedEmail = "poisoned@example.com";
        var failure = new SaveFailureIndexProvider<SurveyInvitation>(SurveysConstants.CollectionName, invitation =>
            invitation.Email == poisonedEmail && invitation.RemindersSent > 0 ? new InvalidOperationException("index provider failed") : null);
        await using var harness = await CreateAsync(sender, failure);
        var poisoned = await InviteWithRemindersAsync(harness, poisonedEmail);
        var healthy = await InviteWithRemindersAsync(harness, "healthy@example.com");

        // Act
        harness.Clock.UtcNow = RuntimeTestHarness.Now.AddDays(2);
        await harness.RunInShellAsync(async services =>
        {
            await new SurveyInvitationReminderBackgroundTask().DoWorkAsync(services, TestContext.Current.CancellationToken);

            return true;
        });

        // Assert
        var healthyNow = await FindAsync(harness, healthy.Invitation.ItemId);
        var poisonedNow = await FindAsync(harness, poisoned.Invitation.ItemId);

        Assert.Equal(1, healthyNow.RemindersSent);
        Assert.Equal(harness.Clock.UtcNow, healthyNow.LastReminderUtc);
        Assert.Equal(0, poisonedNow.RemindersSent);
        Assert.Equal(4, sender.Sent.Count);
    }

    private static async Task<RuntimeTestHarness> CreateAsync(RecordingSender sender, SaveFailureIndexProvider<SurveyInvitation> failure)
    {
        var site = new SiteSettings();
        site.Put(new SurveySettings { InvitationEmailsPerDay = 100 });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);

        var harness = await RuntimeTestHarness.CreateAsync(services =>
        {
            services.AddHttpContextAccessor();
            services.AddSurveysInvitations();
            services.AddSingleton<ISurveyInvitationSender>(sender);
            services.AddSingleton(siteService.Object);
            SaveFailureIndexProvider<SurveyInvitation>.Register(services, failure);
        });

        var store = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<IStore>()));
        await SaveFailureIndexProvider<SurveyInvitation>.CreateTableAsync(store, SurveysConstants.CollectionName);

        return harness;
    }

    // Publishes a survey, invites an address, and turns on daily reminders for the survey's invitation list.
    private static async Task<SurveyInvitationResult> InviteWithRemindersAsync(RuntimeTestHarness harness, string email)
    {
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));
        var result = await harness.RunAsync(services => services.GetRequiredService<ISurveyInvitationService>().InviteAsync(new SurveyInvitationRequest
        {
            SurveyId = survey.ItemId,
            Email = email,
        }));

        await harness.RunAsync(async services =>
        {
            var collectors = services.GetRequiredService<SurveyCollectorStore>();
            var collector = await collectors.FindByIdAsync(result.Invitation.CollectorId);

            collector.Put(new SurveyInvitationSettings { ReminderCount = 2, ReminderEveryDays = 1 });
            await collectors.UpdateAsync(collector);

            return true;
        });

        return result;
    }

    private static Task<SurveyInvitation> FindAsync(RuntimeTestHarness harness, string invitationId)
        => harness.RunAsync(services => services.GetRequiredService<SurveyInvitationStore>().FindByIdAsync(invitationId).AsTask());

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
