using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Core.Handlers;
using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Approvals;

/// <summary>
/// The approval rules over in-memory requests and policies, with a directory that names people by rule value.
/// </summary>
internal sealed class ApprovalsTestContext
{
    public ApprovalsTestContext()
    {
        var clock = new Mock<IClock>();

        clock.SetupGet(entry => entry.UtcNow).Returns(() => Now);

        Store = new InMemoryApprovalRequestStore();
        Manager = new ApprovalRequestManager(Store, [new ApprovalRequestHandler(clock.Object)], NullLogger<CatalogManager<ApprovalRequest>>.Instance);
        Directory = new NamedApproverDirectory();
        Observer = new RecordingApprovalObserver();
        Observers = [Observer];
        Handlers = [];

        var policies = new Mock<INamedCatalogManager<ApprovalPolicy>>();

        policies.Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => Policies.FirstOrDefault(policy => policy.ItemId == id));
        policies.Setup(manager => manager.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) => Policies.FirstOrDefault(policy => policy.Name == name));

        PolicyManager = policies.Object;
        Service = new ApprovalService(Manager, PolicyManager, Directory, Handlers, Observers, Locks, new NoopApprovalCommitter(), clock.Object, RecipeRoundTrip.Localizer<ApprovalService>());
    }

    public DateTime Now { get; set; } = new(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc);

    public KeyedDistributedLock Locks { get; } = new();

    public InMemoryApprovalRequestStore Store { get; }

    public ApprovalRequestManager Manager { get; }

    public List<ApprovalPolicy> Policies { get; } = [];

    public INamedCatalogManager<ApprovalPolicy> PolicyManager { get; }

    public NamedApproverDirectory Directory { get; }

    public RecordingApprovalObserver Observer { get; }

    public List<IApprovalObserver> Observers { get; }

    public List<IApprovalSubjectHandler> Handlers { get; }

    public ApprovalService Service { get; }

    /// <summary>
    /// Adds a policy whose approvers are the people named by the "Finance" permission rule.
    /// </summary>
    public ApprovalPolicy Policy(int requiredApprovals = 1, bool allowRequester = false, bool allowSubject = false, int? dueAfterHours = null, string name = "PayRunApproval")
    {
        var policy = new ApprovalPolicy
        {
            ItemId = "policy-" + name,
            Name = name,
            Title = "Pay run approval",
            RequiredApprovals = requiredApprovals,
            AllowRequesterToApprove = allowRequester,
            AllowSubjectToDecide = allowSubject,
            DueAfterHours = dueAfterHours,
            Approvers = [new ApproverRule { Kind = ApproverKind.Permission, Value = "Finance" }],
        };

        Policies.Add(policy);

        return policy;
    }

    public async Task<ApprovalRequest> RequestAsync(string requester = "riley", string subjectId = "run-1", string scope = null, string[] subjectUserIds = null, string policy = "PayRunApproval")
    {
        var result = await Service.RequestAsync(new ApprovalSubmission
        {
            Policy = policy,
            SubjectKind = "PayRun",
            SubjectId = subjectId,
            AccessScope = scope,
            Title = "Approve the November pay run",
            SubjectUserIds = subjectUserIds?.ToList() ?? [],
        }, TestSecurity.User(requester), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Request;
    }
}

/// <summary>
/// Requests held in memory; the access rule is the one the database runs (ApprovalRequestStoreTests proves both agree).
/// </summary>
internal sealed class InMemoryApprovalRequestStore : InMemoryCatalog<ApprovalRequest>, IApprovalRequestStore
{
    public Task<ApprovalRequest> FindPendingAsync(string subjectKind, string subjectId, string policyId, CancellationToken cancellationToken = default)
        => Task.FromResult(Entries.FirstOrDefault(entry => entry.SubjectKind == subjectKind && entry.SubjectId == subjectId && entry.Status == ApprovalStatus.Pending && (string.IsNullOrEmpty(policyId) || entry.PolicyId == policyId)));

    public Task<IReadOnlyList<ApprovalRequest>> ListBySubjectAsync(string subjectKind, string subjectId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ApprovalRequest>>(Entries.Where(entry => entry.SubjectKind == subjectKind && entry.SubjectId == subjectId).OrderByDescending(entry => entry.CreatedUtc).ToList());

    public Task<(int Count, IReadOnlyList<ApprovalRequest> Requests)> ListPageAsync(ApprovalRequestQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var access = query.Access ?? SourceAccessFilter.Unrestricted;
        var matching = Entries
            .Where(entry => access.Allows(entry.SubjectKind, entry.AccessScope))
            .Where(entry => query.Status is null || entry.Status == query.Status)
            .Where(entry => string.IsNullOrEmpty(query.PolicyId) || entry.PolicyId == query.PolicyId)
            .ToList();
        var ordered = query.OldestFirst ? matching.OrderBy(entry => entry.CreatedUtc) : matching.OrderByDescending(entry => entry.CreatedUtc);

        return Task.FromResult<(int, IReadOnlyList<ApprovalRequest>)>((matching.Count, ordered.Skip(skip).Take(take).ToList()));
    }
}

/// <summary>
/// Names, for each rule value, the user names it lists; user ids are the name plus "-id", as TestSecurity makes them.
/// </summary>
internal sealed class NamedApproverDirectory : IApproverDirectory
{
    public Dictionary<string, string[]> People { get; } = new(StringComparer.Ordinal)
    {
        ["Finance"] = ["pat", "lee", "riley", "kim"],
    };

    public Task<bool> IsNamedAsync(ApproverRule rule, ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => Task.FromResult(user?.Identity?.IsAuthenticated == true && People.GetValueOrDefault(rule.Value ?? string.Empty, []).Contains(user.Identity.Name, StringComparer.Ordinal));

    public Task<IReadOnlyList<string>> GetUserIdsAsync(ApproverRule rule, ApprovalRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(People.GetValueOrDefault(rule.Value ?? string.Empty, []).Select(name => name + "-id").ToList());
}

internal sealed class RecordingApprovalObserver : IApprovalObserver
{
    public List<ApprovalChange> Changes { get; } = [];

    public Task OnChangedAsync(ApprovalChange change, CancellationToken cancellationToken = default)
    {
        Changes.Add(change);

        return Task.CompletedTask;
    }
}

internal sealed class RecordingSubjectHandler : IApprovalSubjectHandler
{
    public string SubjectKind => "PayRun";

    public List<ApprovalStatus> Closed { get; } = [];

    public Task ClosedAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        Closed.Add(request.Status);

        return Task.CompletedTask;
    }
}

internal sealed class NoopApprovalCommitter : IApprovalCommitter
{
    public Task CommitAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
