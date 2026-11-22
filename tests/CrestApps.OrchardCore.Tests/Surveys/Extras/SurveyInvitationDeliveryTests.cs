using System.Security.Cryptography;
using CrestApps.Core;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Outreach.Core.Services;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Invitations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Extras;

/// <summary>
/// Delivery of survey invitations: the daily e-mail limit (R3-M6) and an unreadable opt-out list (R3-M4).
/// </summary>
public sealed class SurveyInvitationDeliveryTests
{
    // 18:00 on 19 November in New York; the site's day there ends at 05:00 UTC on 20 November.
    private static readonly DateTime _evening = new(2026, 11, 19, 23, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task OverTheDailyLimit_AnInvitationIsHeld_AndTheReminderTaskSendsItOnTheSitesNextDay()
    {
        // Arrange: one e-mail a day, counted in the site's time zone.
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 1);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        // Act: the first goes out, the second is held, and a third is only created, never sent.
        var first = await InviteAsync(harness, survey, "first@example.com");
        var held = await InviteAsync(harness, survey, "held@example.com");
        var created = await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().CreateAsync(new SurveyInvitationRequest { SurveyId = survey.ItemId, Email = "created@example.com" }));
        var collectorId = first.Invitation.CollectorId;

        // A new UTC day, but still the same day in New York: nothing more goes out.
        harness.Clock.UtcNow = _evening.AddHours(2);
        var sameSiteDay = await RemindAsync(harness, collectorId);

        // Just after midnight in New York: the held invitation goes out; the created one stays as it is.
        harness.Clock.UtcNow = _evening.AddHours(6.5);
        var nextSiteDay = await RemindAsync(harness, collectorId);

        // Assert
        Assert.Equal(0, sameSiteDay);
        Assert.Equal(1, nextSiteDay);
        Assert.Equal([first.Link, held.Link], sender.Sent);

        var heldNow = await FindAsync(harness, held.Invitation.ItemId);
        var createdNow = await FindAsync(harness, created.Invitation.ItemId);
        Assert.Equal(SurveyInvitationStatus.Sent, heldNow.Status);
        Assert.False(heldNow.TryGet<SurveyInvitationHold>(out var hold) && hold is not null);
        Assert.Equal(SurveyInvitationStatus.Pending, createdNow.Status);
    }

    [Fact]
    public async Task TheDailyLimit_CountsEmails_NotInvitations()
    {
        // Arrange: two e-mails a day.
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(sender, emailsPerDay: 2);
        harness.Clock.UtcNow = _evening;
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        // Act: one invitation and its manual reminder are two e-mails, so the next invitation waits.
        var first = await InviteAsync(harness, survey, "first@example.com");
        harness.Clock.UtcNow = _evening.AddMinutes(10);
        await harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().RemindAsync(first.Invitation.CollectorId, force: true));
        var second = await InviteAsync(harness, survey, "second@example.com");

        // Assert
        Assert.Equal(2, sender.Sent.Count);
        Assert.Equal(SurveyInvitationStatus.Pending, (await FindAsync(harness, second.Invitation.ItemId)).Status);
    }

    [Fact]
    public async Task AnUnreadableOptOutList_HoldsInvitationsUnchanged_AndTheConsentSourceFailsInsteadOfOptingEveryoneOut()
    {
        // Arrange: an address unsubscribed, then the list's Data Protection key is lost.
        var sender = new RecordingSender();
        var protection = new BreakableDataProtection();
        await using var harness = await CreateAsync(sender, emailsPerDay: 100, protection);
        var survey = await harness.PublishAsync(RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("q1"))));

        await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<IConsentService>().RecordAsync(ConsentChannels.Email, "gone@example.com", new ConsentChange
            {
                OptedOut = true,
                Source = SurveyInvitationConsentSource.SourceName,
            });

            return true;
        });

        protection.Broken = true;

        // Act
        var stays = await InviteAsync(harness, survey, "stays@example.com");
        var gone = await InviteAsync(harness, survey, "gone@example.com");
        var consent = await Record.ExceptionAsync(() => harness.RunAsync(services => services.GetRequiredService<IConsentService>().GetAsync(ConsentChannels.Email, "anyone@example.com")));
        var whileBroken = (Stays: await FindAsync(harness, stays.Invitation.ItemId), Gone: await FindAsync(harness, gone.Invitation.ItemId));

        // The keys come back: the next reminder run sends the held invitation and only now marks the unsubscribed one.
        protection.Broken = false;
        await RemindAsync(harness, stays.Invitation.CollectorId);

        // Assert
        Assert.IsType<SurveyInvitationOptOutUnavailableException>(consent);
        Assert.Equal(SurveyInvitationStatus.Pending, whileBroken.Stays.Status);
        Assert.Equal(SurveyInvitationStatus.Pending, whileBroken.Gone.Status);
        Assert.True(whileBroken.Stays.TryGet<SurveyInvitationHold>(out var hold) && hold is not null);
        Assert.Equal([stays.Link], sender.Sent);
        Assert.Equal(SurveyInvitationStatus.Sent, (await FindAsync(harness, stays.Invitation.ItemId)).Status);
        Assert.Equal(SurveyInvitationStatus.OptedOut, (await FindAsync(harness, gone.Invitation.ItemId)).Status);
    }

    [Fact]
    public async Task TwoFirstUnsubscribesAtOnce_EachKeepTheirAddress()
    {
        // Arrange: two sessions both find no list and each insert one.
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var (store, protection) = await harness.RunAsync(services => Task.FromResult((services.GetRequiredService<IStore>(), services.GetRequiredService<IDataProtectionProvider>())));

        await using (var one = store.CreateSession())
        await using (var two = store.CreateSession())
        {
            var first = new SurveyInvitationOptOutStore(one, protection, NullLogger<SurveyInvitationOptOutStore>.Instance);
            var second = new SurveyInvitationOptOutStore(two, protection, NullLogger<SurveyInvitationOptOutStore>.Instance);

            Assert.Equal(SurveyInvitationOptOutState.NotOptedOut, await first.GetStateAsync("a@example.com", TestContext.Current.CancellationToken));
            Assert.Equal(SurveyInvitationOptOutState.NotOptedOut, await second.GetStateAsync("b@example.com", TestContext.Current.CancellationToken));

            await first.SetAsync("a@example.com", optedOut: true, TestContext.Current.CancellationToken);
            await second.SetAsync("b@example.com", optedOut: true, TestContext.Current.CancellationToken);
            await one.SaveChangesAsync(TestContext.Current.CancellationToken);
            await two.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await using var read = store.CreateSession();
        var reader = new SurveyInvitationOptOutStore(read, protection, NullLogger<SurveyInvitationOptOutStore>.Instance);
        var a = await reader.GetStateAsync("a@example.com", TestContext.Current.CancellationToken);
        var b = await reader.GetStateAsync("b@example.com", TestContext.Current.CancellationToken);
        await reader.SetAsync("b@example.com", optedOut: false, TestContext.Current.CancellationToken);
        await read.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var again = store.CreateSession();
        var bAfterOptIn = await new SurveyInvitationOptOutStore(again, protection, NullLogger<SurveyInvitationOptOutStore>.Instance).GetStateAsync("b@example.com", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, (await again.Query<SurveyInvitationOptOutDocument>().ListAsync(TestContext.Current.CancellationToken)).Count);
        Assert.Equal(SurveyInvitationOptOutState.OptedOut, a);
        Assert.Equal(SurveyInvitationOptOutState.OptedOut, b);
        Assert.Equal(SurveyInvitationOptOutState.NotOptedOut, bAfterOptIn);
    }

    [Theory]
    [InlineData("2026-11-19T23:00:00Z", "America/New_York", "2026-11-19T05:00:00Z")]
    [InlineData("2026-11-20T05:30:00Z", "America/New_York", "2026-11-20T05:00:00Z")]
    [InlineData("2026-11-19T23:00:00Z", "", "2026-11-19T00:00:00Z")]
    public void DayStartUtc_IsMidnightInTheSitesTimeZone(string utcNow, string timeZoneId, string expected)
        => Assert.Equal(
            DateTime.Parse(expected, null, System.Globalization.DateTimeStyles.AdjustToUniversal),
            SurveyInvitationService.DayStartUtc(DateTime.Parse(utcNow, null, System.Globalization.DateTimeStyles.AdjustToUniversal), timeZoneId));

    private static async Task<RuntimeTestHarness> CreateAsync(RecordingSender sender, int emailsPerDay, BreakableDataProtection protection = null)
    {
        var site = new SiteSettings { TimeZoneId = "America/New_York" };
        site.Put(new SurveySettings { InvitationEmailsPerDay = emailsPerDay });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);

        return await RuntimeTestHarness.CreateAsync(services =>
        {
            services.AddHttpContextAccessor();
            services.AddSurveysInvitations();
            services.AddSingleton<ISurveyInvitationSender>(sender);
            services.AddSurveyInvitationRecipientPolicy<OutreachSurveyInvitationRecipientPolicy>();
            services.AddOutreachRecipientPolicy<ConsentOutreachRecipientPolicy>();
            services.AddScoped<SurveyInvitationOptOutStore>();
            services.AddConsentSource<SurveyInvitationConsentSource>();
            services.AddSingleton(siteService.Object);

            if (protection is not null)
            {
                services.AddSingleton<IDataProtectionProvider>(protection);
            }
        });
    }

    private static Task<SurveyInvitationResult> InviteAsync(RuntimeTestHarness harness, Survey survey, string email)
        => harness.RunAsync(services => services.GetRequiredService<ISurveyInvitationService>().InviteAsync(new SurveyInvitationRequest
        {
            SurveyId = survey.ItemId,
            Email = email,
        }));

    private static Task<int> RemindAsync(RuntimeTestHarness harness, string collectorId)
        => harness.RunAsync(services => services.GetRequiredService<SurveyInvitationService>().RemindAsync(collectorId, force: false));

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

    /// <summary>
    /// Data Protection whose opt-out list protector can lose its key, as when the keys are missing on a node.
    /// </summary>
    private sealed class BreakableDataProtection : IDataProtectionProvider
    {
        private readonly EphemeralDataProtectionProvider _inner = new();

        public bool Broken { get; set; }

        public IDataProtector CreateProtector(string purpose)
        {
            var protector = _inner.CreateProtector(purpose);

            return string.Equals(purpose, SurveyInvitationOptOutStore.Purpose, StringComparison.Ordinal) ? new Protector(protector, this) : protector;
        }

        private sealed class Protector : IDataProtector
        {
            private readonly IDataProtector _inner;
            private readonly BreakableDataProtection _owner;

            public Protector(IDataProtector inner, BreakableDataProtection owner)
            {
                _inner = inner;
                _owner = owner;
            }

            public IDataProtector CreateProtector(string purpose)
                => _inner.CreateProtector(purpose);

            public byte[] Protect(byte[] plaintext)
                => _inner.Protect(plaintext);

            public byte[] Unprotect(byte[] protectedData)
                => _owner.Broken ? throw new CryptographicException("The key ring has no key for this payload.") : _inner.Unprotect(protectedData);
        }
    }
}
