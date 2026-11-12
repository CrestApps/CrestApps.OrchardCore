using System.Net;
using System.Threading.RateLimiting;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Addresses.Core.Autocomplete;
using CrestApps.OrchardCore.Addresses.Endpoints;
using CrestApps.OrchardCore.Addresses.GooglePlaces;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Tests.Addresses;

/// <summary>
/// A picked address fills the editor in the way its country writes it, and a visitor can't run up the store's bill by
/// asking for suggestions without end.
/// </summary>
public sealed class AddressSuggestionTests
{
    [Fact]
    public void AGermanAddress_PutsTheNumberAfterTheStreet()
    {
        var address = GooglePlacesAddressAutocompleteProvider.ToAddress(
        [
            Component("5", "5", "street_number"),
            Component("Hauptstraße", "Hauptstraße", "route"),
            Component("Berlin", "Berlin", "locality", "political"),
            Component("Berlin", "BE", "administrative_area_level_1", "political"),
            Component("10115", "10115", "postal_code"),
            Component("Deutschland", "DE", "country", "political"),
        ]);

        Assert.Equal("Hauptstraße 5", address.AddressLine1);
        Assert.Equal("Berlin", address.City);
        Assert.Equal("BE", address.Region);
        Assert.Equal("10115", address.PostalCode);
        Assert.Equal("DE", address.Country);
    }

    [Fact]
    public void AnAmericanAddress_PutsTheNumberFirst_AndKeepsTheUnitOnTheSecondLine()
    {
        var address = GooglePlacesAddressAutocompleteProvider.ToAddress(
        [
            Component("100", "100", "street_number"),
            Component("Main Street", "Main St", "route"),
            Component("Suite 4", "Suite 4", "subpremise"),
            Component("Las Vegas", "Las Vegas", "locality"),
            Component("Nevada", "NV", "administrative_area_level_1"),
            Component("89101", "89101", "postal_code"),
            Component("United States", "us", "country"),
        ]);

        Assert.Equal("100 Main Street", address.AddressLine1);
        Assert.Equal("Suite 4", address.AddressLine2);
        Assert.Equal("NV", address.Region);
        Assert.Equal("US", address.Country);
    }

    [Fact]
    public void ABritishTown_IsTakenFromThePostalTown()
    {
        var address = GooglePlacesAddressAutocompleteProvider.ToAddress(
        [
            Component("10", "10", "street_number"),
            Component("Downing Street", "Downing St", "route"),
            Component("London", "London", "postal_town"),
            Component("SW1A 2AA", "SW1A 2AA", "postal_code"),
            Component("United Kingdom", "GB", "country"),
        ]);

        Assert.Equal("10 Downing Street", address.AddressLine1);
        Assert.Equal("London", address.City);
    }

    [Fact]
    public void AVisitor_IsHeldToSoManyRequestsAMinute_ByTheRateLimitsFeature()
    {
        // Arrange
        var services = new ServiceCollection();
        new RateLimitsStartup().ConfigureServices(services);

        var limit = Assert.Single(
            services.BuildServiceProvider().GetRequiredService<IOptions<RateLimitsOptions>>().Value.GroupRateLimits,
            limit => limit.GroupName == AddressConstants.AutocompleteRateLimitGroup);

        using var visitor = Limiter(limit, "203.0.113.7");
        using var other = Limiter(limit, "203.0.113.8");

        // Act
        var allowed = Enumerable.Range(0, AddressAutocompleteEndpoints.RequestsPerMinute)
            .Select(_ =>
            {
                using var lease = visitor.AttemptAcquire();

                return lease.IsAcquired;
            })
            .ToList();

        using var refused = visitor.AttemptAcquire();
        using var otherVisitor = other.AttemptAcquire();

        // Assert
        Assert.All(allowed, Assert.True);
        Assert.False(refused.IsAcquired);
        Assert.True(otherVisitor.IsAcquired);
    }

    [Fact]
    public async Task ASuggestion_WithoutTheEditorToken_IsNotFound_AndThePaidProviderIsNotCalled()
    {
        // Arrange
        var harness = new SuggestionHarness();

        // Act
        var missing = await harness.SuggestAsync(token: null);
        var forged = await harness.SuggestAsync(token: "not-a-token");
        var resolved = await harness.ResolveAsync(token: null);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, missing);
        Assert.Equal(StatusCodes.Status404NotFound, forged);
        Assert.Equal(StatusCodes.Status404NotFound, resolved);
        harness.Provider.Verify(provider => provider.SuggestAsync(It.IsAny<AddressSuggestionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Provider.Verify(provider => provider.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnExpiredEditorToken_IsNotFound()
    {
        // Arrange
        var harness = new SuggestionHarness();
        var token = harness.CreateToken();
        harness.Clock.UtcNow = harness.Clock.UtcNow.AddDays(1);

        // Act
        var status = await harness.SuggestAsync(token);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, status);
        harness.Provider.Verify(provider => provider.SuggestAsync(It.IsAny<AddressSuggestionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheRequestAfterTheMinuteLimit_GetsTooManyRequests_EvenWithRateLimitsOff()
    {
        // Arrange
        var harness = new SuggestionHarness();
        var token = harness.CreateToken();

        // Act
        var allowed = new List<int>();

        for (var i = 0; i < AddressAutocompleteEndpoints.RequestsPerMinute; i++)
        {
            allowed.Add(await harness.SuggestAsync(token));
        }

        var refused = await harness.SuggestAsync(token);
        var otherNetwork = await harness.SuggestAsync(token, address: "198.51.100.20");

        // Assert
        Assert.All(allowed, status => Assert.Equal(StatusCodes.Status200OK, status));
        Assert.Equal(StatusCodes.Status429TooManyRequests, refused);
        Assert.Equal(StatusCodes.Status200OK, otherNetwork);
    }

    [Fact]
    public async Task AfterTheDailyLimit_ThePaidProviderIsNotCalled()
    {
        // Arrange
        var harness = new SuggestionHarness(dailyRequestLimit: 3);
        var token = harness.CreateToken();

        // Act
        var statuses = new List<int>();

        for (var i = 0; i < 5; i++)
        {
            statuses.Add(await harness.SuggestAsync(token, address: "198.51.100." + i));
        }

        // Assert
        Assert.Equal([200, 200, 200], statuses.Take(3));
        Assert.All(statuses.Skip(3), status => Assert.Equal(StatusCodes.Status429TooManyRequests, status));
        harness.Provider.Verify(provider => provider.SuggestAsync(It.IsAny<AddressSuggestionRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ADailyLimitOfZero_FailsClosed()
    {
        // Arrange
        var harness = new SuggestionHarness(dailyRequestLimit: 0);

        // Act
        var status = await harness.SuggestAsync(harness.CreateToken());

        // Assert
        Assert.NotEqual(StatusCodes.Status200OK, status);
        harness.Provider.Verify(provider => provider.SuggestAsync(It.IsAny<AddressSuggestionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class SuggestionHarness
    {
        private readonly ServiceProvider _services;

        public SuggestionHarness(int? dailyRequestLimit = null)
        {
            Provider.Setup(provider => provider.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Provider.Setup(provider => provider.SuggestAsync(It.IsAny<AddressSuggestionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([new AddressSuggestion("a", "1 Main Street")]);

            var services = new ServiceCollection();
            new Startup().ConfigureServices(services);
            services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
            services.AddSingleton<IDistributedLock>(new LocalLock(NullLogger<LocalLock>.Instance));
            services.AddSingleton(new ShellSettings { Name = "Default" });
            services.AddSingleton<IClock>(Clock);
            services.AddLogging();

            if (dailyRequestLimit is { } limit)
            {
                services.Configure<AddressAutocompleteOptions>(options => options.DailyRequestLimit = limit);
            }

            _services = services.BuildServiceProvider();
            Tokens = SignedLinkDoubles.Tokens(new EphemeralDataProtectionProvider(), Clock);
        }

        public TestClock Clock { get; } = new(DateTime.UtcNow);

        public Mock<IAddressAutocompleteProvider> Provider { get; } = new();

        public SignedTokenService Tokens { get; }

        public string CreateToken() => AddressAutocompleteEndpoints.CreateEditorToken(Tokens);

        public async Task<int> SuggestAsync(string token, string address = "203.0.113.7")
        {
            using var scope = _services.CreateScope();
            var httpContext = Request(address, scope.ServiceProvider);
            var result = await AddressAutocompleteEndpoints.SuggestAsync(
                token,
                "1 Main",
                "US",
                "session-1",
                httpContext,
                [Provider.Object],
                Tokens,
                scope.ServiceProvider.GetRequiredService<IAttemptLimiter>(),
                scope.ServiceProvider.GetRequiredService<IOptions<AddressAutocompleteOptions>>());

            return await StatusOfAsync(result, httpContext);
        }

        public async Task<int> ResolveAsync(string token)
        {
            using var scope = _services.CreateScope();
            var httpContext = Request("203.0.113.7", scope.ServiceProvider);
            var result = await AddressAutocompleteEndpoints.ResolveAsync(
                token,
                "place-1",
                "session-1",
                httpContext,
                [Provider.Object],
                Tokens,
                scope.ServiceProvider.GetRequiredService<IAttemptLimiter>(),
                scope.ServiceProvider.GetRequiredService<IOptions<AddressAutocompleteOptions>>());

            return await StatusOfAsync(result, httpContext);
        }

        private static DefaultHttpContext Request(string address, IServiceProvider services)
        {
            var httpContext = new DefaultHttpContext { RequestServices = services };
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(address);
            httpContext.Response.Body = new MemoryStream();

            return httpContext;
        }

        private static async Task<int> StatusOfAsync(IResult result, HttpContext httpContext)
        {
            await result.ExecuteAsync(httpContext);

            return httpContext.Response.StatusCode;
        }
    }

    private static RateLimiter Limiter(GroupRateLimit limit, string address)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(address);

        var partition = limit.Partitioner(httpContext);

        return partition.Factory(partition.PartitionKey);
    }

    private static GooglePlacesAddressAutocompleteProvider.AddressComponent Component(string longText, string shortText, params string[] types)
        => new() { LongText = longText, ShortText = shortText, Types = [.. types] };
}
