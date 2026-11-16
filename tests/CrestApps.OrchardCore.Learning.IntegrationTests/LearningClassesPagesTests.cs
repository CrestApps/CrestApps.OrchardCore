using System.Net;
using CrestApps.OrchardCore.Learning.Classes;
using CrestApps.OrchardCore.Learning.Classes.Core.Services;
using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The admin pages of a class render their rows (the sessions list once failed on its row views) and are refused to a
/// learner.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningClassesPagesTests
{
    private readonly LearningSiteFixture _fixture;

    public LearningClassesPagesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TheFollowUpQueue_KeepsAndHandsOutWorkOnARunningSite()
    {
        // Arrange: the queue that retries class registration work lives in a collection of its own, whose document
        // table the store creates only for collections a module registers. Unregistered, every hourly run failed with
        // "no such table: LearningClassFollowUps_Document".
        var work = new ClassFollowUpWork
        {
            ItemId = Guid.NewGuid().ToString("n"),
            AppointmentId = "missing-" + Guid.NewGuid().ToString("n"),
            Kind = "attendance",
            Actor = "test",
            NextAttemptUtc = DateTime.UtcNow.AddMinutes(-1),
        };

        // Act
        await _fixture.InTenantAsync(async services =>
        {
            await services.GetRequiredService<ILeasedWorkQueue<ClassFollowUpWork>>().EnqueueAsync(work, TestContext.Current.CancellationToken);
            await services.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var claimed = await _fixture.InTenantAsync(async services =>
        {
            var queue = services.GetRequiredService<ILeasedWorkQueue<ClassFollowUpWork>>();
            var due = await queue.ClaimDueAsync(100, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
            var mine = due.SingleOrDefault(item => item.ItemId == work.ItemId);

            if (mine is not null)
            {
                await queue.CompleteAsync(mine, cancellationToken: TestContext.Current.CancellationToken);
            }

            return mine;
        });

        // Assert
        Assert.NotNull(claimed);
        Assert.Equal(work.AppointmentId, claimed.AppointmentId);
    }

    [Fact]
    public async Task TheSessionsAndRosterPages_RenderTheirRows_AndAreRefusedToALearner()
    {
        // Arrange: a class item with one session.
        var title = "Practical " + Guid.NewGuid().ToString("n")[..6];
        var (classItemId, sessionId) = await _fixture.InTenantAsync(async services =>
        {
            var type = new AppointmentType { Name = "Classroom " + title, DurationMinutes = 120, SlotIntervalMinutes = 60 };

            await services.GetRequiredService<AppointmentTypeStore>().CreateAsync(type);

            var contentManager = services.GetRequiredService<IContentManager>();
            var item = await contentManager.NewAsync(ClassesConstants.ContentTypes.Class);

            item.DisplayText = title;
            item.Apply(new ClassPart { AppointmentTypeId = type.ItemId, Capacity = 2 });
            await contentManager.CreateAsync(item, VersionOptions.Published);

            var start = DateTime.UtcNow.Date.AddDays(10).AddHours(14);
            var session = new SchedulingSession
            {
                AppointmentTypeId = type.ItemId,
                Title = title,
                Capacity = 2,
                Source = ClassesConstants.SessionSource,
                SourceId = item.ContentItemId,
                Meetings = [new SchedulingSessionMeeting { StartUtc = start, EndUtc = start.AddHours(2) }],
            };

            var errors = await services.GetRequiredService<ISchedulingSessionService>().SaveAsync(session);

            Assert.Empty(errors);

            return (item.ContentItemId, session.ItemId);
        });

        // Act
        using var admin = await _fixture.CreateClientAsync("admin");
        var sessions = await admin.GetAsync($"Admin/learning/classes/{classItemId}/sessions", TestContext.Current.CancellationToken);
        var sessionsPage = await sessions.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var roster = await admin.GetAsync($"Admin/learning/class-sessions/{sessionId}/roster", TestContext.Current.CancellationToken);

        using var learner = await _fixture.CreateClientAsync("learner1");
        var refused = await learner.GetAsync($"Admin/learning/classes/{classItemId}/sessions", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, sessions.StatusCode);
        Assert.Contains(title, sessionsPage);
        Assert.Contains("0 / 2", sessionsPage);
        Assert.Equal(HttpStatusCode.OK, roster.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, refused.StatusCode);
    }

    [Fact]
    public async Task TheSessionActions_RefuseAPostWithoutTheAntiforgeryToken()
    {
        // Arrange: review round 8, 6.9. An administrator may run each action, so only the token stops them.
        using var admin = await _fixture.CreateClientAsync("admin");
        using var empty = new FormUrlEncodedContent(new Dictionary<string, string>());

        // Act
        var create = await admin.PostAsync("Admin/learning/classes/some-class/sessions/new", empty, TestContext.Current.CancellationToken);
        var edit = await admin.PostAsync("Admin/learning/class-sessions/some-session/edit", empty, TestContext.Current.CancellationToken);
        var cancel = await admin.PostAsync("Admin/learning/class-sessions/some-session/cancel", empty, TestContext.Current.CancellationToken);
        var roster = await admin.PostAsync("Admin/learning/class-sessions/some-session/roster", empty, TestContext.Current.CancellationToken);

        // The same post with the token reaches the action, which does not find the session.
        var page = await admin.GetStringAsync("Admin/learning/enrollments/new", TestContext.Current.CancellationToken);
        using var withToken = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = LearningSiteFixture.Token(page) });
        var reached = await admin.PostAsync("Admin/learning/class-sessions/some-session/roster", withToken, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, roster.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reached.StatusCode);
    }
}
