using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

public sealed class DataConnectionResolverTests
{
    private readonly DataConnectionSecretManager _secretManager = new(new EphemeralDataProtectionProvider(), NullLogger<DataConnectionSecretManager>.Instance);

    [Fact]
    public async Task ResolveAsync_ReturnsTheConnection_WithItsSecretsInClearText()
    {
        var connection = NewConnection("conn-1", "search", DataConnectorConstants.Connectors.Elasticsearch);
        _secretManager.Set(connection, HttpAuthenticationSecrets.Password, "p@ss");

        var context = await CreateResolver(connection).ResolveAsync("conn-1", TestContext.Current.CancellationToken);

        Assert.NotNull(context);
        Assert.Same(connection, context.Connection);
        Assert.NotEqual("p@ss", connection.Secrets[HttpAuthenticationSecrets.Password]);
        Assert.Equal("p@ss", context.Secrets.Get(HttpAuthenticationSecrets.Password));
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNull_ForAnUnknownOrEmptyIdentifier()
    {
        var resolver = CreateResolver(NewConnection("conn-1", "search", DataConnectorConstants.Connectors.Elasticsearch));

        Assert.Null(await resolver.ResolveAsync("missing", TestContext.Current.CancellationToken));
        Assert.Null(await resolver.ResolveAsync(null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyTheConnectionsOfTheGivenConnectors()
    {
        var resolver = CreateResolver(
            NewConnection("conn-1", "zeta", DataConnectorConstants.Connectors.Elasticsearch),
            NewConnection("conn-2", "api", DataConnectorConstants.Connectors.HttpApi),
            NewConnection("conn-3", "alpha", DataConnectorConstants.Connectors.Elasticsearch));

        var connections = await resolver.ListAsync([DataConnectorConstants.Connectors.Elasticsearch], TestContext.Current.CancellationToken);

        Assert.Equal(["alpha", "zeta"], connections.Select(connection => connection.Name));
    }

    [Fact]
    public async Task ResolveForAsync_RefusesAConnectionOfAnotherConnector()
    {
        var resolver = CreateResolver(NewConnection("conn-2", "api", DataConnectorConstants.Connectors.HttpApi));

        var resolution = await resolver.ResolveForAsync("conn-2", [DataConnectorConstants.Connectors.Elasticsearch], TestContext.Current.CancellationToken);

        Assert.False(resolution.Succeeded);
        Assert.Null(resolution.Context);
    }

    [Fact]
    public async Task ResolveForAsync_ExplainsThatTheFeatureIsOff_WhenThereIsNoResolver()
    {
        IDataConnectionResolver resolver = null;

        var resolution = await resolver.ResolveForAsync("conn-1", [DataConnectorConstants.Connectors.Elasticsearch], TestContext.Current.CancellationToken);

        Assert.False(resolution.Succeeded);
        Assert.Contains("Data Connectors", resolution.Error, StringComparison.Ordinal);
    }

    private DataConnectionResolver CreateResolver(params DataConnection[] connections)
    {
        var store = new Mock<IDataConnectionStore>();
        store
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connections);
        store
            .Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(connections.FirstOrDefault(connection => connection.ItemId == id)));

        return new DataConnectionResolver(store.Object, _secretManager);
    }

    private static DataConnection NewConnection(string id, string name, string connector)
        => new()
        {
            ItemId = id,
            Name = name,
            Connector = connector,
        };
}
