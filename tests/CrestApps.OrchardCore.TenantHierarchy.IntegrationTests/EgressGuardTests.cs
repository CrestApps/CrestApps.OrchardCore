using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Proves the egress guard reaches the HTTP clients of parent and child tenants, and leaves ordinary tenants alone.
/// </summary>
[Collection(TenantHierarchyCollection.Name)]
public sealed class EgressGuardTests
{
    private readonly TenantHierarchyFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="EgressGuardTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared hierarchy.</param>
    public EgressGuardTests(TenantHierarchyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RequestToALoopbackAddress_FromAChild_IsRefused()
    {
        // Act
        var exception = await Record.ExceptionAsync(() => _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
        {
            var client = services.GetRequiredService<IHttpClientFactory>().CreateClient();
            await client.GetAsync($"http://127.0.0.1:{_fixture.Host.Port}/");
        }));

        // Assert
        Assert.IsType<HttpRequestException>(exception);
        Assert.Contains("refused", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestToASiblingsHost_FromAChild_IsRefused()
    {
        // Act
        var exception = await Record.ExceptionAsync(() => _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
        {
            var client = services.GetRequiredService<IHttpClientFactory>().CreateClient("named-client");
            await client.GetAsync("http://business2.firma.localhost/");
        }));

        // Assert
        Assert.IsType<HttpRequestException>(exception);
        Assert.Contains("tenant of this application", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestToALoopbackAddress_FromAParent_IsRefused()
    {
        // Act
        var exception = await Record.ExceptionAsync(() => _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
        {
            var client = services.GetRequiredService<IHttpClientFactory>().CreateClient();
            await client.GetAsync($"http://127.0.0.1:{_fixture.Host.Port}/");
        }));

        // Assert
        Assert.IsType<HttpRequestException>(exception);
    }

    [Fact]
    public async Task RequestToALoopbackAddress_FromAnOrdinaryTenant_IsSent()
    {
        // Act
        var status = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.Plain, async services =>
        {
            var client = services.GetRequiredService<IHttpClientFactory>().CreateClient();
            using var response = await client.GetAsync($"http://127.0.0.1:{_fixture.Host.Port}/");

            return (int)response.StatusCode;
        });

        // Assert
        Assert.True(status > 0);
    }
}
