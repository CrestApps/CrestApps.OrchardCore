using System.Net;
using CrestApps.OrchardCore.Learning.Classes;
using CrestApps.OrchardCore.Learning.Classes.Models;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
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
}
