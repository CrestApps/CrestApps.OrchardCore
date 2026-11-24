using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using YesSqlSession = global::YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Timeline.AI;

/// <summary>
/// The services the Timeline AI tools run on in tests: a timeline in memory, an access service whose answers a test
/// sets, the record kinds Content, Order, Ticket and WorkOrder, a record directory, a people directory, a local clock
/// that keeps UTC and a session whose commits are counted.
/// </summary>
internal sealed class TimelineAIToolTestKit
{
    public static readonly DateTime Now = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    public static readonly PartyRef Ana = new(PartyKinds.User, "u-ana");

    public InMemoryTimelineService Timeline { get; } = new();

    public FakeTimelineAccess Access { get; } = new();

    public Mock<IRecordDirectory> Directory { get; } = new();

    public Mock<YesSqlSession> Session { get; } = new();

    public ITimelineService TimelineService { get; set; }

    public ClaimsPrincipal User { get; set; } = SignedIn();

    public TimelineAIToolTestKit()
    {
        TimelineService = Timeline;
        Directory.Setup(directory => directory.GetSummaryAsync(It.IsAny<RecordRef>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecordRef record, CancellationToken _) => new RecordSummary { Id = record.Id, Title = "Acme Corp", AdminUrl = "/Admin/acme" });
        Session.Setup(session => session.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    public static ClaimsPrincipal SignedIn()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Ana.Id), new Claim(ClaimTypes.Name, "ana")], "test"));

    public static ClaimsPrincipal Anonymous()
        => new(new ClaimsIdentity());

    public async Task<string> InvokeAsync(AIFunction tool, Dictionary<string, object> arguments)
    {
        var services = new ServiceCollection();

        services.AddOptions();
        services.AddSingleton<IUserAccessor>(new TestUserAccessor { User = User });
        services.AddSingleton(TimelineService);
        services.AddSingleton<ITimelineAccessService>(Access);
        services.AddSingleton(Directory.Object);
        services.AddSingleton(Session.Object);
        services.AddSingleton(WorkforceTimeOffAdminAIToolTests.LocalClock(() => Now));

        foreach (var kind in new[] { "Content", "Order", "Ticket", "WorkOrder" })
        {
            services.AddSingleton(Mock.Of<ITimelineAccessProvider>(provider => provider.SubjectKind == kind));
        }

        var people = new Mock<IPartyDirectory>();

        people.Setup(directory => directory.GetNamesAsync(It.IsAny<IEnumerable<PartyRef>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<PartyRef, string> { [Ana] = "Ana Lopez" });
        services.AddSingleton(people.Object);

        var attachments = new Mock<IAttachmentService>();

        attachments.Setup(service => service.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) => ids.Select(id => new Attachment { ItemId = id, FileName = id + ".pdf" }).ToList());
        services.AddSingleton(attachments.Object);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var result = await tool.InvokeAsync(
            new AIFunctionArguments(arguments.ToDictionary(pair => pair.Key, pair => (object)JsonSerializer.SerializeToElement(pair.Value))) { Services = scope.ServiceProvider },
            TestContext.Current.CancellationToken);

        return result?.ToString();
    }

    private sealed class TestUserAccessor : IUserAccessor
    {
        public ClaimsPrincipal User { get; set; }
    }
}

/// <summary>
/// An access service whose answers a test sets: by default the user reads and writes every timeline.
/// </summary>
internal sealed class FakeTimelineAccess : ITimelineAccessService
{
    public Func<RecordRef, TimelineVisibility, bool> View { get; set; } = (_, _) => true;

    public Func<RecordRef, string, TimelineVisibility, bool> Add { get; set; } = (_, _, _) => true;

    public Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef subject, TimelineVisibility visibility, CancellationToken cancellationToken = default)
        => Task.FromResult(View(subject, visibility));

    public Task<bool> CanAddAsync(ClaimsPrincipal user, RecordRef subject, string kind, TimelineVisibility visibility, CancellationToken cancellationToken = default)
        => Task.FromResult(Add(subject, kind, visibility));
}
