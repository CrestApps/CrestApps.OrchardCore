using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Api;

public sealed class AIApiKeysTests
{
    private static readonly DateTime _now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(AIApiKeyKind.Server, AIApiKeys.ServerKeyStart)]
    [InlineData(AIApiKeyKind.Browser, AIApiKeys.BrowserKeyStart)]
    public void Generate_MakesAKeyItsPrefixFindsAndItsHashMatches(AIApiKeyKind kind, string start)
    {
        // Act
        var (key, prefix, hash) = AIApiKeys.Generate(kind);

        // Assert
        Assert.StartsWith(start, key, StringComparison.Ordinal);
        Assert.Equal(prefix, AIApiKeys.GetPrefix(key));
        Assert.StartsWith(prefix, key, StringComparison.Ordinal);
        Assert.Equal(AIApiKeys.Hash(key), hash);
        Assert.NotEqual(key, AIApiKeys.Generate(kind).Key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("eyJhbGciOiJSUzI1NiJ9.token")]
    [InlineData("cak_short")]
    public void GetPrefix_IgnoresWhatIsNotAKey(string value)
        => Assert.Null(AIApiKeys.GetPrefix(value));

    [Fact]
    public void Matches_RefusesAWrongDisabledOrExpiredKey()
    {
        // Arrange
        var (key, prefix, hash) = AIApiKeys.Generate(AIApiKeyKind.Server);
        var stored = new AIApiKey { Prefix = prefix, Hash = hash };

        // Act & Assert
        Assert.True(AIApiKeys.Matches(stored, key, _now));
        Assert.False(AIApiKeys.Matches(stored, key + "x", _now));
        Assert.False(AIApiKeys.Matches(new AIApiKey { Hash = hash, Enabled = false }, key, _now));
        Assert.False(AIApiKeys.Matches(new AIApiKey { Hash = hash, ExpiresUtc = _now }, key, _now));
        Assert.True(AIApiKeys.Matches(new AIApiKey { Hash = hash, ExpiresUtc = _now.AddMinutes(1) }, key, _now));
        Assert.False(AIApiKeys.Matches(null, key, _now));
    }

    [Fact]
    public void AllowsOrigin_HoldsABrowserKeyToItsSites()
    {
        // Arrange
        var browser = new AIApiKey { Kind = AIApiKeyKind.Browser, AllowedOrigins = ["https://www.example.com"] };

        // Act & Assert
        Assert.True(AIApiKeys.AllowsOrigin(browser, "https://www.example.com"));
        Assert.True(AIApiKeys.AllowsOrigin(browser, "https://WWW.example.com/"));
        Assert.False(AIApiKeys.AllowsOrigin(browser, "https://evil.example.net"));
        Assert.False(AIApiKeys.AllowsOrigin(browser, null));
        Assert.True(AIApiKeys.AllowsOrigin(new AIApiKey { Kind = AIApiKeyKind.Server }, null));
    }

    [Fact]
    public void AServerKey_ActsWithItsRolesInItsScopesOnly()
    {
        // Arrange
        var user = AIApiKeys.CreatePrincipal(new AIApiKey
        {
            ItemId = "k1",
            Name = "Reports",
            Kind = AIApiKeyKind.Server,
            Scopes = [AIApiKeys.Scopes.Conversations, "nonsense"],
            Roles = ["Analyst"],
        });

        // Assert
        Assert.True(user.Identity.IsAuthenticated);
        Assert.True(user.IsInRole("Analyst"));
        Assert.True(AIApiKeys.AllowsScope(user, AIApiKeys.Scopes.Conversations));
        Assert.False(AIApiKeys.AllowsScope(user, AIApiKeys.Scopes.Calls));
        Assert.DoesNotContain(user.Claims, claim => claim.Value == "ai.nonsense");
    }

    [Fact]
    public void AKeyWithNoScope_MayCallNothing_WhileAnOpenIdClientWithNoAIScopeMayCallEverything()
    {
        // Arrange
        var key = AIApiKeys.CreatePrincipal(new AIApiKey { ItemId = "k1", Kind = AIApiKeyKind.Server });
        var client = new ClaimsPrincipal(new ClaimsIdentity([new Claim("oi_scp", "openid profile")], "Bearer"));
        var limited = new ClaimsPrincipal(new ClaimsIdentity([new Claim("oi_scp", "openid ai.calls")], "Bearer"));

        // Assert
        Assert.False(AIApiKeys.AllowsScope(key, AIApiKeys.Scopes.Profiles));
        Assert.True(AIApiKeys.AllowsScope(client, AIApiKeys.Scopes.Profiles));
        Assert.True(AIApiKeys.AllowsScope(limited, AIApiKeys.Scopes.Calls));
        Assert.False(AIApiKeys.AllowsScope(limited, AIApiKeys.Scopes.Profiles));
    }

    [Fact]
    public async Task ABrowserKey_ChatsOnlyWithItsProfiles()
    {
        // Arrange
        var user = AIApiKeys.CreatePrincipal(new AIApiKey
        {
            ItemId = "k2",
            Kind = AIApiKeyKind.Browser,
            AllowedProfileIds = ["p1"],
            Roles = ["Administrator"],
            Scopes = [AIApiKeys.Scopes.Profiles],
        });
        var handler = new AIApiKeyProfileAuthorizationHandler();

        // Act
        var allowed = await AuthorizeAsync(handler, user, AIPermissions.QueryAnyAIProfile, new AIProfile { ItemId = "p1" });
        var otherProfile = await AuthorizeAsync(handler, user, AIPermissions.QueryAnyAIProfile, new AIProfile { ItemId = "p2" });
        var otherPermission = await AuthorizeAsync(handler, user, AIPermissions.ManageAIProfiles, new AIProfile { ItemId = "p1" });

        // Assert
        Assert.True(allowed);
        Assert.False(otherProfile);
        Assert.False(otherPermission);
        Assert.False(user.IsInRole("Administrator"));
        Assert.False(AIApiKeys.AllowsScope(user, AIApiKeys.Scopes.Profiles));
    }

    [Fact]
    public void Merge_ChangesOnlyWhatIsSent()
    {
        // Arrange
        var target = JsonNode.Parse("""{"Name":"a","Settings":{"X":1,"Y":2}}""").AsObject();

        // Act
        AISourceCatalogApi.Merge(target, JsonNode.Parse("""{"Settings":{"Y":3},"Title":"t"}""").AsObject());

        // Assert
        Assert.Equal("""{"Name":"a","Settings":{"X":1,"Y":3},"Title":"t"}""", target.ToJsonString());
    }

    private static async Task<bool> AuthorizeAsync(AIApiKeyProfileAuthorizationHandler handler, ClaimsPrincipal user, Permission permission, object resource)
    {
        var context = new AuthorizationHandlerContext([new PermissionRequirement(permission)], user, resource);
        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }
}
