namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterHubSecurityTests
{
    [Fact]
    public void OnConnectedAsync_RequiresPermissionBeforeJoiningEachTenantQualifiedDestination()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var hubPath = Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.ContactCenter",
            "Hubs",
            "ContactCenterHub.cs");

        // Act
        var source = File.ReadAllText(hubPath);
        var onConnectedStart = source.IndexOf("public override async Task OnConnectedAsync()", StringComparison.Ordinal);
        var onDisconnectedStart = source.IndexOf("public override async Task OnDisconnectedAsync", StringComparison.Ordinal);

        Assert.True(onConnectedStart >= 0);
        Assert.True(onDisconnectedStart > onConnectedStart);

        var onConnectedSource = source.Substring(onConnectedStart, onDisconnectedStart - onConnectedStart);
        var queueAuthorization = onConnectedSource.IndexOf(
            "AuthorizeAsync(services, ContactCenterPermissions.SignIntoQueues)",
            StringComparison.Ordinal);
        var queueJoin = onConnectedSource.IndexOf(
            "Groups.AddToGroupAsync(Context.ConnectionId, GetQueueGroup(queueId)",
            StringComparison.Ordinal);
        var supervisorAuthorization = onConnectedSource.IndexOf(
            "AuthorizeAsync(services, ContactCenterPermissions.MonitorContactCenter)",
            StringComparison.Ordinal);
        var supervisorJoin = onConnectedSource.IndexOf(
            "Groups.AddToGroupAsync(Context.ConnectionId, GetSupervisorsGroup()",
            StringComparison.Ordinal);
        var unauthorizedAbort = onConnectedSource.IndexOf("if (!authorized)", StringComparison.Ordinal);
        var tenantUserJoin = onConnectedSource.IndexOf(
            "TenantSignalRGroupName.ForUser(_tenantName, userId)",
            StringComparison.Ordinal);

        // Assert
        Assert.True(queueAuthorization >= 0);
        Assert.True(queueJoin > queueAuthorization);
        Assert.True(supervisorAuthorization >= 0);
        Assert.True(supervisorJoin > supervisorAuthorization);
        Assert.True(unauthorizedAbort > supervisorJoin);
        Assert.True(tenantUserJoin > unauthorizedAbort);
    }

    [Fact]
    public void OnConnectedAsync_ReconcilesQueueGroupsAfterInitialAssignment()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var hubPath = Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.ContactCenter",
            "Hubs",
            "ContactCenterHub.cs");

        // Act
        var source = File.ReadAllText(hubPath);
        var onConnectedStart = source.IndexOf("public override async Task OnConnectedAsync()", StringComparison.Ordinal);
        var onDisconnectedStart = source.IndexOf("public override async Task OnDisconnectedAsync", StringComparison.Ordinal);

        Assert.True(onConnectedStart >= 0);
        Assert.True(onDisconnectedStart > onConnectedStart);

        var onConnectedSource = source.Substring(onConnectedStart, onDisconnectedStart - onConnectedStart);
        var initialQueueJoin = onConnectedSource.IndexOf(
            "Groups.AddToGroupAsync(Context.ConnectionId, GetQueueGroup(queueId)",
            StringComparison.Ordinal);
        var freshSnapshot = onConnectedSource.IndexOf(
            "services.SessionService.BuildSnapshotAsync(userId",
            StringComparison.Ordinal);
        var reconciliation = onConnectedSource.IndexOf(
            "UpdateQueueGroupsAsync(session.QueueIds, snapshot.QueueIds)",
            StringComparison.Ordinal);

        // Assert
        Assert.True(initialQueueJoin >= 0);
        Assert.True(freshSnapshot > initialQueueJoin);
        Assert.True(reconciliation > freshSnapshot);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("The repository root could not be located.");
    }
}
