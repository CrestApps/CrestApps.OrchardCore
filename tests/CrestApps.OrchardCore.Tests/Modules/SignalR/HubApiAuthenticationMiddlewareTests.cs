using System.Security.Claims;
using CrestApps.OrchardCore.SignalR;
using CrestApps.OrchardCore.SignalR.Middlewares;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.SignalR;

public sealed class HubApiAuthenticationMiddlewareTests
{
    private const string ApiScheme = "Api";

    private const string ValidToken = "valid-token";

    [Fact]
    public async Task InvokeAsync_WhenHubRequestCarriesAccessTokenQueryString_AuthenticatesTheUser()
    {
        // Arrange
        var authenticationService = new TestAuthenticationService();
        var context = CreateHubContext(authenticationService);

        context.Request.QueryString = QueryString.Create("access_token", ValidToken);

        // Act
        await CreateMiddleware().InvokeAsync(context);

        // Assert
        Assert.True(context.User.Identity.IsAuthenticated);
        Assert.Equal("token-user", context.User.Identity.Name);
    }

    [Fact]
    public async Task InvokeAsync_WhenHubRequestCarriesBearerHeader_AuthenticatesTheUser()
    {
        // Arrange
        var authenticationService = new TestAuthenticationService();
        var context = CreateHubContext(authenticationService);

        context.Request.Headers.Authorization = "Bearer " + ValidToken;

        // Act
        await CreateMiddleware().InvokeAsync(context);

        // Assert
        Assert.True(context.User.Identity.IsAuthenticated);
        Assert.Equal("token-user", context.User.Identity.Name);
    }

    [Fact]
    public async Task InvokeAsync_WhenTokenIsInvalid_LeavesTheUserAnonymous()
    {
        // Arrange
        var authenticationService = new TestAuthenticationService();
        var context = CreateHubContext(authenticationService);

        context.Request.QueryString = QueryString.Create("access_token", "unknown-token");

        // Act
        await CreateMiddleware().InvokeAsync(context);

        // Assert
        Assert.False(context.User.Identity.IsAuthenticated);
        Assert.Equal(1, authenticationService.AuthenticateCallCount);
    }

    [Fact]
    public async Task InvokeAsync_WhenEndpointIsNotAHub_DoesNotAuthenticate()
    {
        // Arrange
        var authenticationService = new TestAuthenticationService();
        var context = CreateHubContext(authenticationService, isHubEndpoint: false);

        context.Request.QueryString = QueryString.Create("access_token", ValidToken);

        // Act
        await CreateMiddleware().InvokeAsync(context);

        // Assert
        Assert.False(context.User.Identity.IsAuthenticated);
        Assert.Equal(0, authenticationService.AuthenticateCallCount);
    }

    [Fact]
    public async Task InvokeAsync_WhenUserIsAlreadyAuthenticated_DoesNotAuthenticate()
    {
        // Arrange
        var authenticationService = new TestAuthenticationService();
        var context = CreateHubContext(authenticationService);

        context.Request.QueryString = QueryString.Create("access_token", ValidToken);
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "cookie-user")], "Cookies"));

        // Act
        await CreateMiddleware().InvokeAsync(context);

        // Assert
        Assert.Equal("cookie-user", context.User.Identity.Name);
        Assert.Equal(0, authenticationService.AuthenticateCallCount);
    }

    [Fact]
    public async Task InvokeAsync_WhenHubDidNotOptIn_DoesNotAuthenticate()
    {
        // Arrange
        var authenticationService = new TestAuthenticationService();
        var context = CreateHubContext(authenticationService, allowsApiToken: false);

        context.Request.QueryString = QueryString.Create("access_token", ValidToken);

        // Act
        await CreateMiddleware().InvokeAsync(context);

        // Assert
        Assert.False(context.User.Identity.IsAuthenticated);
        Assert.Equal(0, authenticationService.AuthenticateCallCount);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoTokenIsProvided_DoesNotAuthenticate()
    {
        // Arrange
        var authenticationService = new TestAuthenticationService();
        var context = CreateHubContext(authenticationService);

        // Act
        await CreateMiddleware().InvokeAsync(context);

        // Assert
        Assert.False(context.User.Identity.IsAuthenticated);
        Assert.Equal(0, authenticationService.AuthenticateCallCount);
    }

    [Fact]
    public async Task InvokeAsync_WhenAnotherAuthorizationSchemeIsUsed_DoesNotAuthenticate()
    {
        // Arrange
        var authenticationService = new TestAuthenticationService();
        var context = CreateHubContext(authenticationService);

        context.Request.Headers.Authorization = "Basic dXNlcjpwYXNzd29yZA==";

        // Act
        await CreateMiddleware().InvokeAsync(context);

        // Assert
        Assert.False(context.User.Identity.IsAuthenticated);
        Assert.Equal(0, authenticationService.AuthenticateCallCount);
    }

    private static HubApiAuthenticationMiddleware CreateMiddleware()
        => new(_ => Task.CompletedTask, NullLogger<HubApiAuthenticationMiddleware>.Instance);

    private static DefaultHttpContext CreateHubContext(
        TestAuthenticationService authenticationService,
        bool isHubEndpoint = true,
        bool allowsApiToken = true)
    {
        var schemeProvider = new Mock<IAuthenticationSchemeProvider>();

        schemeProvider.Setup(x => x.GetSchemeAsync(ApiScheme))
            .ReturnsAsync(new AuthenticationScheme(ApiScheme, ApiScheme, typeof(TestAuthenticationHandler)));

        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(authenticationService)
            .AddSingleton(schemeProvider.Object)
            .BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = services,
        };

        var metadata = new List<object>();

        if (isHubEndpoint)
        {
            metadata.Add(new HubMetadata(typeof(TestHub)));
        }

        if (allowsApiToken)
        {
            metadata.Add(new AllowApiTokenAuthenticationAttribute());
        }

        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test"));

        return context;
    }

    [AllowApiTokenAuthentication]
    private sealed class TestHub : Hub
    {
    }

    private sealed class TestAuthenticationHandler : IAuthenticationHandler
    {
        public Task<AuthenticateResult> AuthenticateAsync()
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(AuthenticationProperties properties)
            => Task.CompletedTask;

        public Task ForbidAsync(AuthenticationProperties properties)
            => Task.CompletedTask;

        public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context)
            => Task.CompletedTask;
    }

    private sealed class TestAuthenticationService : IAuthenticationService
    {
        public int AuthenticateCallCount { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string scheme)
        {
            AuthenticateCallCount++;

            var token = GetBearerToken(context.Request.Headers.Authorization);

            if (token != ValidToken)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid token."));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "token-user")], scheme));

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, scheme)));
        }

        public Task ChallengeAsync(HttpContext context, string scheme, AuthenticationProperties properties)
            => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string scheme, AuthenticationProperties properties)
            => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string scheme, ClaimsPrincipal principal, AuthenticationProperties properties)
            => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string scheme, AuthenticationProperties properties)
            => Task.CompletedTask;

        private static string GetBearerToken(StringValues authorization)
        {
            var value = authorization.ToString();

            return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? value.Substring("Bearer ".Length)
                : null;
        }
    }
}
