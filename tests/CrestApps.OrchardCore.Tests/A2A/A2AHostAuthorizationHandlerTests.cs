using System.Security.Claims;
using CrestApps.AI.A2A.Models;
using CrestApps.AI.A2A.Services;
using CrestApps.OrchardCore.AI.A2A.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.A2A;

public sealed class A2AHostAuthorizationHandlerTests
{
    // ───────────────────────────────────────────────────────────────
    // Default configuration safety
    // ───────────────────────────────────────────────────────────────
    [Fact]
    public void DefaultOptions_AuthenticationType_IsOpenId()
    {
        var options = new A2AHostOptions();
        Assert.Equal(A2AHostAuthenticationType.OpenId, options.AuthenticationType);
    }

    [Fact]
    public void DefaultOptions_RequireAccessPermission_IsTrue()
    {
        var options = new A2AHostOptions();
        Assert.True(options.RequireAccessPermission);
    }

    [Fact]
    public void DefaultOptions_ApiKey_IsNull()
    {
        var options = new A2AHostOptions();
        Assert.Null(options.ApiKey);
    }

    [Fact]
    public void AccessA2AHostPermission_IsSecurityCritical()
    {
        Assert.True(A2AHostPermissionsProvider.AccessA2AHost.IsSecurityCritical);
    }

    // ───────────────────────────────────────────────────────────────
    // AuthenticationType = None
    // ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task None_AnonymousUser_Succeeds()
    {
        var handler = CreateHandler(new A2AHostOptions
        {
            AuthenticationType = A2AHostAuthenticationType.None,
        });

        var context = CreateContext(CreateAnonymousPrincipal());
        await handler.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task None_AuthenticatedUser_Succeeds()
    {
        var handler = CreateHandler(new A2AHostOptions
        {
            AuthenticationType = A2AHostAuthenticationType.None,
        });

        var context = CreateContext(CreateAuthenticatedPrincipal("Bearer"));
        await handler.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    // ───────────────────────────────────────────────────────────────
    // AuthenticationType = ApiKey
    // ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task ApiKey_AuthenticatedWithCorrectScheme_Succeeds()
    {
        var handler = CreateHandler(new A2AHostOptions
        {
            AuthenticationType = A2AHostAuthenticationType.ApiKey,
        });

        var context = CreateContext(CreateAuthenticatedPrincipal(A2AApiKeyAuthenticationDefaults.AuthenticationScheme));
        await handler.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task ApiKey_AuthenticatedWithDifferentScheme_DoesNotSucceed()
    {
        var handler = CreateHandler(new A2AHostOptions
        {
            AuthenticationType = A2AHostAuthenticationType.ApiKey,
        });

        var context = CreateContext(CreateAuthenticatedPrincipal("Bearer"));
        await handler.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task ApiKey_AnonymousUser_DoesNotSucceed()
    {
        var handler = CreateHandler(new A2AHostOptions
        {
            AuthenticationType = A2AHostAuthenticationType.ApiKey,
        });

        var context = CreateContext(CreateAnonymousPrincipal());
        await handler.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    // ───────────────────────────────────────────────────────────────
    // AuthenticationType = OpenId, RequireAccessPermission = true
    // ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task OpenId_RequirePermission_AuthenticatedWithPermission_Succeeds()
    {
        var handler = CreateHandler(
            new A2AHostOptions
            {
                AuthenticationType = A2AHostAuthenticationType.OpenId,
                RequireAccessPermission = true,
            },
            authorizeResult: true);
        var context = CreateContext(CreateAuthenticatedPrincipal("Bearer"));
        await handler.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task OpenId_RequirePermission_AuthenticatedWithoutPermission_DoesNotSucceed()
    {
        var handler = CreateHandler(
            new A2AHostOptions
            {
                AuthenticationType = A2AHostAuthenticationType.OpenId,
                RequireAccessPermission = true,
            },
            authorizeResult: false);
        var context = CreateContext(CreateAuthenticatedPrincipal("Bearer"));
        await handler.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task OpenId_RequirePermission_AnonymousUser_DoesNotSucceed()
    {
        var handler = CreateHandler(
            new A2AHostOptions
            {
                AuthenticationType = A2AHostAuthenticationType.OpenId,
                RequireAccessPermission = true,
            },
            authorizeResult: true);
        var context = CreateContext(CreateAnonymousPrincipal());
        await handler.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    // ───────────────────────────────────────────────────────────────
    // AuthenticationType = OpenId, RequireAccessPermission = false
    // ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task OpenId_NoPermissionRequired_AuthenticatedUser_Succeeds()
    {
        var handler = CreateHandler(new A2AHostOptions
        {
            AuthenticationType = A2AHostAuthenticationType.OpenId,
            RequireAccessPermission = false,
        });

        var context = CreateContext(CreateAuthenticatedPrincipal("Bearer"));
        await handler.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task OpenId_NoPermissionRequired_AnonymousUser_DoesNotSucceed()
    {
        var handler = CreateHandler(new A2AHostOptions
        {
            AuthenticationType = A2AHostAuthenticationType.OpenId,
            RequireAccessPermission = false,
        });

        var context = CreateContext(CreateAnonymousPrincipal());
        await handler.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    // ───────────────────────────────────────────────────────────────
    // Default / fallback behavior (unrecognized enum value falls through to OpenId)
    // ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task DefaultFallback_AuthenticatedWithPermission_Succeeds()
    {
        var handler = CreateHandler(
            new A2AHostOptions
            {
                AuthenticationType = (A2AHostAuthenticationType)999,
                RequireAccessPermission = true,
            },
            authorizeResult: true);
        var context = CreateContext(CreateAuthenticatedPrincipal("Bearer"));
        await handler.HandleAsync(context);
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task DefaultFallback_AnonymousUser_DoesNotSucceed()
    {
        var handler = CreateHandler(
            new A2AHostOptions
            {
                AuthenticationType = (A2AHostAuthenticationType)999,
                RequireAccessPermission = false,
            });

        var context = CreateContext(CreateAnonymousPrincipal());
        await handler.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    // ───────────────────────────────────────────────────────────────
    // Cross-authentication scheme security tests
    // ───────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("Bearer")]
    [InlineData("Cookies")]
    [InlineData("OpenIdConnect")]
    public async Task ApiKey_AuthenticatedWithNonApiKeyScheme_DoesNotSucceed(string scheme)
    {
        var handler = CreateHandler(new A2AHostOptions
        {
            AuthenticationType = A2AHostAuthenticationType.ApiKey,
        });

        var context = CreateContext(CreateAuthenticatedPrincipal(scheme));
        await handler.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task OpenId_RequirePermission_ApiKeySchemeUser_WithoutPermission_DoesNotSucceed()
    {
        var handler = CreateHandler(
            new A2AHostOptions
            {
                AuthenticationType = A2AHostAuthenticationType.OpenId,
                RequireAccessPermission = true,
            },
            authorizeResult: false);
        var context = CreateContext(CreateAuthenticatedPrincipal(A2AApiKeyAuthenticationDefaults.AuthenticationScheme));
        await handler.HandleAsync(context);
        Assert.False(context.HasSucceeded);
    }

    // ───────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────
    private static A2AHostAuthorizationHandler CreateHandler(
        A2AHostOptions options,
        bool authorizeResult = false)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(options));
        var authServiceMock = new Mock<IAuthorizationService>();
        authServiceMock
            .Setup(s => s.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
        It.IsAny<object>(),
        It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(authorizeResult
        ? AuthorizationResult.Success()
        : AuthorizationResult.Failed());
        services.AddSingleton(authServiceMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        return new A2AHostAuthorizationHandler(serviceProvider);
    }

    private static AuthorizationHandlerContext CreateContext(ClaimsPrincipal user)
    {
        var requirement = new A2AHostAuthorizationRequirement();

        return new AuthorizationHandlerContext(
            [requirement],
            user,
            resource: null);
    }

    private static ClaimsPrincipal CreateAnonymousPrincipal()
    {
        return new ClaimsPrincipal(new ClaimsIdentity());
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(string authenticationScheme)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user"),
            new Claim(ClaimTypes.Name, "Test User"),
        };

        var identity = new ClaimsIdentity(claims, authenticationScheme);

        return new ClaimsPrincipal(identity);
    }
}
