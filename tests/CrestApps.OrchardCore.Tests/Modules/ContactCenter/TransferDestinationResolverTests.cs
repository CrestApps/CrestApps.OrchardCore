using System.Collections.Generic;
using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class TransferDestinationResolverTests
{
    // Old approved-external prefix strings and bare E.164 numbers must all be denied

    [Theory]
    [InlineData("approved-external:+1911")]
    [InlineData("approved-external:+112")]
    [InlineData("approved-external:+19005551234")]
    [InlineData("+15551234567")]
    public async Task ResolveAsync_WhenExternalDestinationIsUnsafeOrNotApproved_Denies(
        string targetId)
    {
        // Arrange — empty catalog; none of the old prefix-style strings will match an entry
        var settings = new ContactCenterExternalTransferSettings();
        var resolver = BuildResolver(new AllowAuthorizationService(), settings);

        // Act
        var result = await resolver.ResolveAsync(new TransferRequest
        {
            TargetType = InteractionTransferTargetType.External,
            TargetId = targetId,
        }, new ClaimsPrincipal(new ClaimsIdentity("test")), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("The requested transfer destination is not available.", result.FailureReason);
    }

    // Catalog entry present and enabled resolves to stored E.164

    [Fact]
    public async Task ResolveAsync_WhenDestinationIdIsInCatalogAndEnabled_Succeeds()
    {
        // Arrange
        var entry = new ContactCenterExternalDestination
        {
            Id = "dest-001",
            DisplayName = "Support Line",
            E164Address = "+15551234567",
            Enabled = true,
        };

        var settings = new ContactCenterExternalTransferSettings
        {
            Destinations = [entry],
        };

        var resolver = BuildResolver(new AllowAuthorizationService(), settings);

        // Act
        var result = await resolver.ResolveAsync(new TransferRequest
        {
            TargetType = InteractionTransferTargetType.External,
            TargetId = "dest-001",
        }, new ClaimsPrincipal(new ClaimsIdentity("test")), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(InteractionTransferTargetType.External, result.TargetType);
        Assert.Equal("+15551234567", result.ResolvedTarget);
    }

    [Fact]
    public async Task ResolveAsync_WhenDestinationIdIsInCatalogAndEnabled_ReturnsStoredAddressNotCallerSupplied()
    {
        // Arrange — the caller supplies only the catalog ID; the stored E.164 must be used
        var entry = new ContactCenterExternalDestination
        {
            Id = "dest-safe",
            DisplayName = "Partner Line",
            E164Address = "+442071234567",
            Enabled = true,
        };

        var settings = new ContactCenterExternalTransferSettings
        {
            Destinations = [entry],
        };

        var resolver = BuildResolver(new AllowAuthorizationService(), settings);

        // Act
        var result = await resolver.ResolveAsync(new TransferRequest
        {
            TargetType = InteractionTransferTargetType.External,
            TargetId = "dest-safe",
        }, new ClaimsPrincipal(new ClaimsIdentity("test")), TestContext.Current.CancellationToken);

        // Assert — resolved to the stored address, not to "dest-safe"
        Assert.True(result.Succeeded);
        Assert.Equal("+442071234567", result.ResolvedTarget);
        Assert.NotEqual("dest-safe", result.ResolvedTarget);
    }

    // Unknown destination identifier is denied

    [Fact]
    public async Task ResolveAsync_WhenDestinationIdIsNotInCatalog_Denies()
    {
        // Arrange — catalog has one entry; caller provides a different ID
        var settings = new ContactCenterExternalTransferSettings
        {
            Destinations =
            [
                new ContactCenterExternalDestination
                {
                    Id = "known-entry",
                    DisplayName = "Known",
                    E164Address = "+15559876543",
                    Enabled = true,
                },
            ],
        };

        var resolver = BuildResolver(new AllowAuthorizationService(), settings);

        // Act
        var result = await resolver.ResolveAsync(new TransferRequest
        {
            TargetType = InteractionTransferTargetType.External,
            TargetId = "unknown-entry",
        }, new ClaimsPrincipal(new ClaimsIdentity("test")), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("The requested transfer destination is not available.", result.FailureReason);
    }

    // Disabled catalog entry is denied

    [Fact]
    public async Task ResolveAsync_WhenDestinationIsDisabled_Denies()
    {
        // Arrange — entry exists in catalog but is disabled
        var settings = new ContactCenterExternalTransferSettings
        {
            Destinations =
            [
                new ContactCenterExternalDestination
                {
                    Id = "dest-disabled",
                    DisplayName = "Disabled Line",
                    E164Address = "+15551112222",
                    Enabled = false,
                },
            ],
        };

        var resolver = BuildResolver(new AllowAuthorizationService(), settings);

        // Act
        var result = await resolver.ResolveAsync(new TransferRequest
        {
            TargetType = InteractionTransferTargetType.External,
            TargetId = "dest-disabled",
        }, new ClaimsPrincipal(new ClaimsIdentity("test")), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("The requested transfer destination is not available.", result.FailureReason);
    }

    // Emergency number stored in catalog is denied

    [Theory]
    [InlineData("+1911")]
    [InlineData("+112")]
    [InlineData("+999")]
    [InlineData("+15551234911")]
    public async Task ResolveAsync_WhenStoredAddressIsEmergency_Denies(string emergencyAddress)
    {
        // Arrange — entry is enabled and in catalog but its stored address is an emergency number
        var settings = new ContactCenterExternalTransferSettings
        {
            Destinations =
            [
                new ContactCenterExternalDestination
                {
                    Id = "emergency-dest",
                    DisplayName = "Should Never Resolve",
                    E164Address = emergencyAddress,
                    Enabled = true,
                },
            ],
        };

        var resolver = BuildResolver(new AllowAuthorizationService(), settings);

        // Act
        var result = await resolver.ResolveAsync(new TransferRequest
        {
            TargetType = InteractionTransferTargetType.External,
            TargetId = "emergency-dest",
        }, new ClaimsPrincipal(new ClaimsIdentity("test")), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("The requested transfer destination is not available.", result.FailureReason);
    }

    // Premium number stored in catalog is denied

    [Theory]
    [InlineData("+19005551234")]
    [InlineData("+19765551234")]
    [InlineData("+44705551234")]
    public async Task ResolveAsync_WhenStoredAddressIsPremium_Denies(string premiumAddress)
    {
        // Arrange
        var settings = new ContactCenterExternalTransferSettings
        {
            Destinations =
            [
                new ContactCenterExternalDestination
                {
                    Id = "premium-dest",
                    DisplayName = "Premium Line",
                    E164Address = premiumAddress,
                    Enabled = true,
                },
            ],
        };

        var resolver = BuildResolver(new AllowAuthorizationService(), settings);

        // Act
        var result = await resolver.ResolveAsync(new TransferRequest
        {
            TargetType = InteractionTransferTargetType.External,
            TargetId = "premium-dest",
        }, new ClaimsPrincipal(new ClaimsIdentity("test")), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("The requested transfer destination is not available.", result.FailureReason);
    }

    // RBAC denied always overrides catalog state

    [Fact]
    public async Task ResolveAsync_WhenRbacDenied_DeniesEvenForValidCatalogEntry()
    {
        // Arrange — entry is in catalog and enabled, but RBAC says no
        var settings = new ContactCenterExternalTransferSettings
        {
            Destinations =
            [
                new ContactCenterExternalDestination
                {
                    Id = "dest-rbac",
                    DisplayName = "Restricted Line",
                    E164Address = "+15559990000",
                    Enabled = true,
                },
            ],
        };

        var resolver = BuildResolver(new DenyAuthorizationService(), settings);

        // Act
        var result = await resolver.ResolveAsync(new TransferRequest
        {
            TargetType = InteractionTransferTargetType.External,
            TargetId = "dest-rbac",
        }, new ClaimsPrincipal(new ClaimsIdentity("test")), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("The requested transfer destination is not available.", result.FailureReason);
    }

    [Fact]
    public async Task ResolveAsync_WhenPrincipalIsNull_DeniesForExternalTransfer()
    {
        // Arrange
        var settings = new ContactCenterExternalTransferSettings
        {
            Destinations =
            [
                new ContactCenterExternalDestination
                {
                    Id = "dest-null-principal",
                    DisplayName = "Any Line",
                    E164Address = "+15558880000",
                    Enabled = true,
                },
            ],
        };

        var resolver = BuildResolver(new AllowAuthorizationService(), settings);

        // Act
        var result = await resolver.ResolveAsync(new TransferRequest
        {
            TargetType = InteractionTransferTargetType.External,
            TargetId = "dest-null-principal",
        }, principal: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    // Raw E.164 supplied directly by caller is always denied

    [Theory]
    [InlineData("+15551234567")]
    [InlineData("+442071234567")]
    public async Task ResolveAsync_WhenCallerSuppliesRawE164NotMatchingAnyId_Denies(string rawE164)
    {
        // Arrange — catalog has an entry whose E164Address matches but whose ID does not
        var settings = new ContactCenterExternalTransferSettings
        {
            Destinations =
            [
                new ContactCenterExternalDestination
                {
                    Id = "dest-xxx",
                    DisplayName = "Some Line",
                    E164Address = rawE164,
                    Enabled = true,
                },
            ],
        };

        var resolver = BuildResolver(new AllowAuthorizationService(), settings);

        // Act — caller passes the raw E.164 as targetId instead of the catalog ID
        var result = await resolver.ResolveAsync(new TransferRequest
        {
            TargetType = InteractionTransferTargetType.External,
            TargetId = rawE164,
        }, new ClaimsPrincipal(new ClaimsIdentity("test")), TestContext.Current.CancellationToken);

        // Assert — must be denied; the resolver only accepts catalog IDs, never raw E.164
        Assert.False(result.Succeeded);
        Assert.Equal("The requested transfer destination is not available.", result.FailureReason);
    }

    // Helpers

    private static TransferDestinationResolver BuildResolver(
        IAuthorizationService authorizationService,
        ContactCenterExternalTransferSettings settings)
    {
        return new TransferDestinationResolver(
            authorizationService,
            Mock.Of<IAgentProfileManager>(),
            Mock.Of<IActivityQueueManager>(),
            SiteServiceFactory.Create(settings));
    }

    private sealed class AllowAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            return Task.FromResult(AuthorizationResult.Success());
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            string policyName)
        {
            return Task.FromResult(AuthorizationResult.Success());
        }
    }

    private sealed class DenyAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            return Task.FromResult(AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            string policyName)
        {
            return Task.FromResult(AuthorizationResult.Failed());
        }
    }
}

