using System.Security.Claims;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Resources.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Localization;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Parties;

/// <summary>
/// The party picker's search: a user searches only the kinds whose search permission they hold, a kind without a
/// search permission or without a source is never searched, and a user who may search none is refused.
/// </summary>
public sealed class PartySearchEndpointTests
{
    private static readonly Permission _searchUsers = new("SearchUsers", LocalizationSource.Create<PartySearchEndpointTests>("Search users"));
    private static readonly Permission _searchCustomers = new("SearchCustomers", LocalizationSource.Create<PartySearchEndpointTests>("Search customers"));

    [Fact]
    public async Task AUser_SearchesOnlyTheKindsTheyMaySearch()
    {
        // Arrange
        var user = User(_searchUsers.Name);

        // Act
        var result = await SearchAsync(user, kinds: null);

        // Assert
        var items = Assert.IsType<Ok<IEnumerable<PartySearchEndpoints.PartySearchItem>>>(result).Value.ToList();
        Assert.Equal(["User:u1"], items.Select(item => item.Value));
    }

    [Fact]
    public async Task AUserWhoMaySearchNone_IsRefused()
    {
        // Arrange
        var user = User();

        // Act
        var result = await SearchAsync(user, kinds: "User,Customer");

        // Assert
        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task AKindWithoutASearchPermission_OrWithoutASource_IsNeverSearched()
    {
        // Arrange
        var user = User(_searchUsers.Name, _searchCustomers.Name);

        // Act
        var open = await SearchAsync(user, kinds: "Guest");
        var sourceless = await SearchAsync(user, kinds: "Account");

        // Assert
        Assert.IsType<ForbidHttpResult>(open);
        Assert.IsType<ForbidHttpResult>(sourceless);
    }

    [Fact]
    public async Task SeveralKinds_AreLabelledWithTheirKind()
    {
        // Arrange
        var user = User(_searchUsers.Name, _searchCustomers.Name);

        // Act
        var result = await SearchAsync(user, kinds: "User,Customer");

        // Assert
        var items = Assert.IsType<Ok<IEnumerable<PartySearchEndpoints.PartySearchItem>>>(result).Value.ToList();
        Assert.Equal(["User:u1", "Customer:c1"], items.Select(item => item.Value));
        Assert.Equal(["User", "Customer"], items.Select(item => item.SecondaryText));
    }

    private static async Task<IResult> SearchAsync(ClaimsPrincipal user, string kinds)
    {
        var directory = new Mock<IPartyDirectory>();

        directory.SetupGet(value => value.Kinds).Returns(["User", "Customer", "Guest"]);
        directory.Setup(value => value.GetSource(It.Is<string>(kind => kind == "User" || kind == "Customer" || kind == "Guest"))).Returns(Mock.Of<IPartySource>());
        directory.Setup(value => value.SearchAsync("User", It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([new PartyOption("u1", "Maria")]);
        directory.Setup(value => value.SearchAsync("Customer", It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([new PartyOption("c1", "Sam")]);

        var options = new PartySourceOptions();

        options.Kinds["User"] = new PartyKindDescriptor { Kind = "User", DisplayName = LocalizationSource.Create<PartySearchEndpointTests>("User"), SearchPermission = _searchUsers };
        options.Kinds["Customer"] = new PartyKindDescriptor { Kind = "Customer", DisplayName = LocalizationSource.Create<PartySearchEndpointTests>("Customer"), SearchPermission = _searchCustomers };
        options.Kinds["Guest"] = new PartyKindDescriptor { Kind = "Guest", DisplayName = LocalizationSource.Create<PartySearchEndpointTests>("Guest") };
        options.Kinds["Account"] = new PartyKindDescriptor { Kind = "Account", DisplayName = LocalizationSource.Create<PartySearchEndpointTests>("Account"), SearchPermission = _searchCustomers };

        return await PartySearchEndpoints.HandleAsync(
            kinds,
            "a",
            new DefaultHttpContext { User = user },
            new PermissionClaimsAuthorizationService(),
            directory.Object,
            Options.Create(options),
            new PassThroughLocalizerFactory(),
            TestContext.Current.CancellationToken);
    }

    private static ClaimsPrincipal User(params string[] permissions)
        => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1"), .. permissions.Select(permission => new Claim(Permission.ClaimType, permission))],
            "test"));

    private sealed class PermissionClaimsAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(requirements.OfType<PermissionRequirement>().All(requirement => user.HasClaim(Permission.ClaimType, requirement.Permission.Name))
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }

    private sealed class PassThroughLocalizerFactory : IStringLocalizerFactory
    {
        public IStringLocalizer Create(Type resourceSource) => new PassThrough();

        public IStringLocalizer Create(string baseName, string location) => new PassThrough();

        private sealed class PassThrough : IStringLocalizer
        {
            public LocalizedString this[string name] => new(name, name);

            public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

            public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
        }
    }
}
