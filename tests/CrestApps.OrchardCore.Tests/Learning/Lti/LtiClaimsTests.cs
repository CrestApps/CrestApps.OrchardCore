using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Lti;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Lti;

/// <summary>
/// The claims of a launch's id token, following the example resource link request of LTI 1.3 Core (section 5.3 and the
/// sample in Appendix D), the Deep Linking request of Deep Linking 2.0 (section 4.4) and the service claims of
/// Assignment and Grade Services 2.0 (3.1) and Names and Role Provisioning Services 2.0 (3.6.1).
/// </summary>
public sealed class LtiClaimsTests
{
    private static readonly DateTime _now = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    private static readonly LtiPlatformInfo _platform = new()
    {
        Issuer = "https://lms.example.com",
        Guid = "5d2b4c4e-0d3c-4b0f-9a7e-2f5d3a8c1b6e",
        Name = "Example LMS",
        AuthorizationEndpoint = "https://lms.example.com/learning/lti/authorize",
        TokenEndpoint = "https://lms.example.com/connect/token",
        JwksUrl = "https://lms.example.com/learning/lti/jwks",
        DeepLinkingReturnUrl = "https://lms.example.com/learning/lti/deep-linking/return",
    };

    [Fact]
    public void Build_AResourceLinkRequest_HasTheRequiredClaims()
    {
        // Act
        var claims = LtiClaims.Build(ResourceLink(), Tool(), _platform, "nonce-1", _now);

        // Assert: the OpenID Connect claims (Core 5.1.2).
        Assert.Equal("https://lms.example.com", claims["iss"]);
        Assert.Equal("client-1", claims["aud"]);
        Assert.Equal("client-1", claims["azp"]);
        Assert.Equal("subject-1", claims["sub"]);
        Assert.Equal("nonce-1", claims["nonce"]);
        Assert.Equal(new DateTimeOffset(_now).ToUnixTimeSeconds(), Convert.ToInt64(claims["iat"]));
        Assert.Equal(new DateTimeOffset(_now + LtiClaims.IdTokenLifetime).ToUnixTimeSeconds(), Convert.ToInt64(claims["exp"]));

        // The LTI message claims (Core 5.3 and 5.4).
        Assert.Equal("LtiResourceLinkRequest", claims[LtiConstants.Claims.MessageType]);
        Assert.Equal("1.3.0", claims[LtiConstants.Claims.Version]);
        Assert.Equal("deployment-1", claims[LtiConstants.Claims.DeploymentId]);
        Assert.Equal("https://tool.example.com/launch/simulator", claims[LtiConstants.Claims.TargetLinkUri]);

        var resourceLink = Assert.IsType<JsonObject>(claims[LtiConstants.Claims.ResourceLink]);
        Assert.Equal("item-1", resourceLink["id"]?.GetValue<string>());
        Assert.Equal("Simulator", resourceLink["title"]?.GetValue<string>());

        var roles = Assert.IsType<JsonArray>(claims[LtiConstants.Claims.Roles]);
        Assert.Equal([LtiConstants.Roles.Learner], roles.Select(role => role.GetValue<string>()));

        var context = Assert.IsType<JsonObject>(claims[LtiConstants.Claims.Context]);
        Assert.Equal("course-1", context["id"]?.GetValue<string>());
        Assert.Equal("Forklift Safety", context["title"]?.GetValue<string>());
        Assert.Equal("http://purl.imsglobal.org/vocab/lis/v2/course#CourseOffering", context["type"]?.AsArray()[0]?.GetValue<string>());

        var platform = Assert.IsType<JsonObject>(claims[LtiConstants.Claims.ToolPlatform]);
        Assert.Equal(_platform.Guid, platform["guid"]?.GetValue<string>());
        Assert.Equal("Example LMS", platform["name"]?.GetValue<string>());
        Assert.Equal("crestapps-orchardcore-learning", platform["product_family_code"]?.GetValue<string>());

        var presentation = Assert.IsType<JsonObject>(claims[LtiConstants.Claims.LaunchPresentation]);
        Assert.Equal("window", presentation["document_target"]?.GetValue<string>());
        Assert.Equal("https://lms.example.com/learn/course-1/item-1", presentation["return_url"]?.GetValue<string>());
        Assert.Equal("en-US", presentation["locale"]?.GetValue<string>());

        var custom = Assert.IsType<JsonObject>(claims[LtiConstants.Claims.Custom]);
        Assert.Equal("hard", custom["level"]?.GetValue<string>());
        Assert.Equal("subject-1", custom["learner"]?.GetValue<string>());

        Assert.False(claims.ContainsKey(LtiConstants.Claims.DeepLinkingSettings));
    }

    [Fact]
    public void Build_AnAnonymousTool_GetsNoNameAndNoEmail_AndNeverTheUserId()
    {
        // Act
        var claims = LtiClaims.Build(ResourceLink(), Tool(), _platform, "nonce-1", _now);

        // Assert
        Assert.DoesNotContain("name", claims.Keys);
        Assert.DoesNotContain("given_name", claims.Keys);
        Assert.DoesNotContain("family_name", claims.Keys);
        Assert.DoesNotContain("email", claims.Keys);
        Assert.DoesNotContain(claims.Values, value => Equals(value, "real-user-id"));
    }

    [Fact]
    public void Build_AToolThatMayHaveNameAndEmail_GetsThem()
    {
        // Arrange
        var context = ResourceLink();
        context.Name = "Avery Lane";
        context.GivenName = "Avery";
        context.FamilyName = "Lane";
        context.Email = "avery@example.com";

        // Act
        var claims = LtiClaims.Build(context, Tool(), _platform, "nonce-1", _now);

        // Assert
        Assert.Equal("Avery Lane", claims["name"]);
        Assert.Equal("Avery", claims["given_name"]);
        Assert.Equal("Lane", claims["family_name"]);
        Assert.Equal("avery@example.com", claims["email"]);
    }

    [Fact]
    public void Build_AToolThatMayUseTheServices_GetsTheirEndpoints()
    {
        // Arrange
        var tool = Tool();
        tool.AllowGrades = true;
        tool.AllowNamesAndRoles = true;

        var context = ResourceLink();
        context.LineItemUrl = _platform.LineItemUrl("course-1", "line-1");

        // Act
        var claims = LtiClaims.Build(context, tool, _platform, "nonce-1", _now);

        // Assert
        var ags = Assert.IsType<JsonObject>(claims[LtiConstants.Claims.AgsEndpoint]);
        Assert.Equal(
            [LtiConstants.Scopes.LineItem, LtiConstants.Scopes.LineItemReadOnly, LtiConstants.Scopes.ResultReadOnly, LtiConstants.Scopes.Score],
            ags["scope"]!.AsArray().Select(scope => scope.GetValue<string>()));
        Assert.Equal("https://lms.example.com/learning/lti/ags/course-1/lineitems", ags["lineitems"]?.GetValue<string>());
        Assert.Equal("https://lms.example.com/learning/lti/ags/course-1/lineitems/line-1", ags["lineitem"]?.GetValue<string>());

        var nrps = Assert.IsType<JsonObject>(claims[LtiConstants.Claims.NrpsEndpoint]);
        Assert.Equal("https://lms.example.com/learning/lti/nrps/course-1/memberships", nrps["context_memberships_url"]?.GetValue<string>());
        Assert.Equal(["2.0"], nrps["service_versions"]!.AsArray().Select(version => version.GetValue<string>()));
    }

    [Fact]
    public void Build_AToolThatMayNotUseTheServices_GetsNoEndpoints()
    {
        // Act
        var claims = LtiClaims.Build(ResourceLink(), Tool(), _platform, "nonce-1", _now);

        // Assert
        Assert.False(claims.ContainsKey(LtiConstants.Claims.AgsEndpoint));
        Assert.False(claims.ContainsKey(LtiConstants.Claims.NrpsEndpoint));
    }

    [Fact]
    public void Build_ADeepLinkingRequest_HasTheSettings_AndNoResourceLink()
    {
        // Arrange
        var context = new LtiLaunchContext
        {
            MessageType = LtiConstants.MessageTypes.DeepLinkingRequest,
            UserId = "real-user-id",
            Subject = "subject-1",
            Roles = [LtiConstants.Roles.Instructor],
            CourseId = "course-1",
            CourseTitle = "Forklift Safety",
            TargetLinkUri = "https://tool.example.com/deep-link",
            DeepLinkingData = "signed-data",
            Locale = "en-US",
        };

        // Act
        var claims = LtiClaims.Build(context, Tool(), _platform, "nonce-2", _now);

        // Assert
        Assert.Equal("LtiDeepLinkingRequest", claims[LtiConstants.Claims.MessageType]);
        Assert.Equal("https://tool.example.com/deep-link", claims[LtiConstants.Claims.TargetLinkUri]);
        Assert.False(claims.ContainsKey(LtiConstants.Claims.ResourceLink));

        var settings = Assert.IsType<JsonObject>(claims[LtiConstants.Claims.DeepLinkingSettings]);
        Assert.Equal("https://lms.example.com/learning/lti/deep-linking/return", settings["deep_link_return_url"]?.GetValue<string>());
        Assert.Equal(["ltiResourceLink"], settings["accept_types"]!.AsArray().Select(type => type.GetValue<string>()));
        Assert.Equal(["window"], settings["accept_presentation_document_targets"]!.AsArray().Select(target => target.GetValue<string>()));
        Assert.True(settings["accept_multiple"]?.GetValue<bool>());
        Assert.True(settings["accept_lineitem"]?.GetValue<bool>());
        Assert.Equal("signed-data", settings["data"]?.GetValue<string>());
    }

    private static ExternalTool Tool()
        => new()
        {
            ItemId = "tool-1",
            ClientId = "client-1",
            DeploymentId = "deployment-1",
            TargetLinkUri = "https://tool.example.com/launch",
            CustomParameters = "level=easy\nlearner=$User.id",
            PrivacyLevel = LtiPrivacyLevel.Anonymous,
        };

    private static LtiLaunchContext ResourceLink()
        => new()
        {
            MessageType = LtiConstants.MessageTypes.ResourceLinkRequest,
            UserId = "real-user-id",
            Subject = "subject-1",
            Roles = [LtiConstants.Roles.Learner],
            CourseId = "course-1",
            CourseTitle = "Forklift Safety",
            ItemId = "item-1",
            ItemTitle = "Simulator",
            TargetLinkUri = "https://tool.example.com/launch/simulator",
            LinkCustomParameters = "level=hard",
            ReturnUrl = "https://lms.example.com/learn/course-1/item-1",
            Locale = "en-US",
        };
}
