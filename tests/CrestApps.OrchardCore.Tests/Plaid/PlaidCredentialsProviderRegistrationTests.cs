using CrestApps.OrchardCore.Plaid;
using CrestApps.OrchardCore.Plaid.Models;
using CrestApps.OrchardCore.Plaid.Services;
using Microsoft.Extensions.DependencyInjection;
using PlaidStartup = CrestApps.OrchardCore.Plaid.Startup;

namespace CrestApps.OrchardCore.Tests.Plaid;

public sealed class PlaidCredentialsProviderRegistrationTests
{
    [Fact]
    public void WithoutAHostProvider_TheSiteSettingsProviderIsUsed()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new PlaidStartup().ConfigureServices(services);

        // Assert
        Assert.Equal(typeof(PlaidCredentialsProvider), ProviderType(services));
    }

    [Fact]
    public void AHostProviderRegisteredBeforeThePlaidFeature_WinsOverTheSiteSettings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPlaidCredentialsProvider<HostPlaidCredentials>();

        // Act
        new PlaidStartup().ConfigureServices(services);

        // Assert
        Assert.Equal(typeof(HostPlaidCredentials), ProviderType(services));
    }

    [Fact]
    public void AHostProviderRegisteredAfterThePlaidFeature_ReplacesTheSiteSettings()
    {
        // Arrange
        var services = new ServiceCollection();
        new PlaidStartup().ConfigureServices(services);

        // Act
        services.AddPlaidCredentialsProvider<HostPlaidCredentials>();

        // Assert
        Assert.Equal(typeof(HostPlaidCredentials), ProviderType(services));
    }

    [Fact]
    public void ThePlaidFeature_RegistersTheTenantClient_AndTheBankAccountWebhookHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new PlaidStartup().ConfigureServices(services);
        services.AddPlaidWebhookHandler<RecordingPlaidWebhookHandler>();
        services.AddPlaidWebhookHandler<RecordingPlaidWebhookHandler>();

        // Assert
        Assert.Equal(typeof(PlaidTenantClient), Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IPlaidClient)).ImplementationType);
        Assert.Equal(
            [typeof(PlaidBankAccountWebhookHandler), typeof(RecordingPlaidWebhookHandler)],
            services.Where(descriptor => descriptor.ServiceType == typeof(IPlaidWebhookHandler)).Select(descriptor => descriptor.ImplementationType));
    }

    [Fact]
    public async Task TheTenantClient_CallsPlaidWithTheProvidersCredentials_AndRefusesWithoutThem()
    {
        // Arrange
        var context = new PlaidTestContext();
        context.Handler.Answer("item/remove", "{}");
        var client = new PlaidTenantClient(context.Client, context.Credentials);

        // Act
        await client.RemoveItemAsync("access-1", TestContext.Current.CancellationToken);
        context.Credentials.Credentials = null;
        var configured = await client.IsConfiguredAsync(TestContext.Current.CancellationToken);

        // Assert
        var (_, request, _) = Assert.Single(context.Handler.Calls);
        Assert.Equal("client-1", request.Headers.GetValues("PLAID-CLIENT-ID").Single());
        Assert.False(configured);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.RemoveItemAsync("access-1", TestContext.Current.CancellationToken));
        Assert.Single(context.Handler.Calls);
    }

    private static Type ProviderType(IServiceCollection services)
        => Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IPlaidCredentialsProvider)).ImplementationType;

    private sealed class HostPlaidCredentials : IPlaidCredentialsProvider
    {
        public Task<PlaidCredentials> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new PlaidCredentials(PlaidEnvironment.Production, "host-client", "host-secret"));
    }
}
