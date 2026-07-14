namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterHubSecurityTests
{
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
            "sessionService.BuildSnapshotAsync(userId",
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
