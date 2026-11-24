using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Lti.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// Builds the claims of the id token of a launch (LTI 1.3 Core section 5): the OpenID Connect claims, the LTI message
/// claims, and the Deep Linking settings (Deep Linking 2.0) or the resource link with the Assignment and Grade Services
/// and Names and Role Provisioning Services endpoints the tool may use.
/// </summary>
public static class LtiClaims
{
    /// <summary>
    /// How long a launch's id token is valid.
    /// </summary>
    public static readonly TimeSpan IdTokenLifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The product family code the platform names itself with.
    /// </summary>
    public const string ProductFamilyCode = "crestapps-orchardcore-learning";

    private const string CourseOfferingType = "http://purl.imsglobal.org/vocab/lis/v2/course#CourseOffering";

    /// <summary>
    /// Builds the claims.
    /// </summary>
    /// <param name="context">The launch.</param>
    /// <param name="tool">The tool.</param>
    /// <param name="platform">The platform.</param>
    /// <param name="nonce">The nonce of the authorization request.</param>
    /// <param name="utcNow">The current time.</param>
    /// <returns>The claims, JSON values as <see cref="JsonNode"/>.</returns>
    public static IDictionary<string, object> Build(LtiLaunchContext context, ExternalTool tool, LtiPlatformInfo platform, string nonce, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(platform);

        var issuedAt = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["iss"] = platform.Issuer,
            ["aud"] = tool.ClientId,
            ["azp"] = tool.ClientId,
            ["sub"] = context.Subject,
            ["nonce"] = nonce,
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            ["exp"] = issuedAt.Add(IdTokenLifetime).ToUnixTimeSeconds(),
            [LtiConstants.Claims.MessageType] = context.MessageType,
            [LtiConstants.Claims.Version] = LtiConstants.Version,
            [LtiConstants.Claims.DeploymentId] = tool.DeploymentId,
            [LtiConstants.Claims.TargetLinkUri] = context.TargetLinkUri,
            [LtiConstants.Claims.Roles] = new JsonArray((context.Roles ?? []).Select(role => (JsonNode)role).ToArray()),
            [LtiConstants.Claims.Context] = new JsonObject
            {
                ["id"] = context.CourseId,
                ["title"] = context.CourseTitle,
                ["type"] = new JsonArray(CourseOfferingType),
            },
            [LtiConstants.Claims.ToolPlatform] = new JsonObject
            {
                ["guid"] = platform.Guid,
                ["name"] = platform.Name,
                ["product_family_code"] = ProductFamilyCode,
            },
            [LtiConstants.Claims.Custom] = LtiCustomParameters.Resolve(tool.CustomParameters, context.LinkCustomParameters, LtiCustomParameters.VariablesOf(context)),
        };

        var presentation = new JsonObject
        {
            ["document_target"] = "window",
            ["locale"] = context.Locale,
        };

        if (!string.IsNullOrEmpty(context.ReturnUrl))
        {
            presentation["return_url"] = context.ReturnUrl;
        }

        claims[LtiConstants.Claims.LaunchPresentation] = presentation;

        AddIfPresent(claims, "name", context.Name);
        AddIfPresent(claims, "given_name", context.GivenName);
        AddIfPresent(claims, "family_name", context.FamilyName);
        AddIfPresent(claims, "email", context.Email);

        if (context.MessageType == LtiConstants.MessageTypes.DeepLinkingRequest)
        {
            claims[LtiConstants.Claims.DeepLinkingSettings] = new JsonObject
            {
                ["deep_link_return_url"] = platform.DeepLinkingReturnUrl,
                ["accept_types"] = new JsonArray("ltiResourceLink"),
                ["accept_presentation_document_targets"] = new JsonArray("window"),
                ["accept_multiple"] = true,
                ["accept_lineitem"] = true,
                ["auto_create"] = false,
                ["data"] = context.DeepLinkingData,
            };
        }
        else
        {
            claims[LtiConstants.Claims.ResourceLink] = new JsonObject
            {
                ["id"] = context.ItemId,
                ["title"] = context.ItemTitle,
            };
        }

        if (tool.AllowGrades)
        {
            var ags = new JsonObject
            {
                ["scope"] = new JsonArray(LtiConstants.Scopes.LineItem, LtiConstants.Scopes.LineItemReadOnly, LtiConstants.Scopes.ResultReadOnly, LtiConstants.Scopes.Score),
                ["lineitems"] = platform.LineItemsUrl(context.CourseId),
            };

            if (!string.IsNullOrEmpty(context.LineItemUrl))
            {
                ags["lineitem"] = context.LineItemUrl;
            }

            claims[LtiConstants.Claims.AgsEndpoint] = ags;
        }

        if (tool.AllowNamesAndRoles)
        {
            claims[LtiConstants.Claims.NrpsEndpoint] = new JsonObject
            {
                ["context_memberships_url"] = platform.MembershipsUrl(context.CourseId),
                ["service_versions"] = new JsonArray("2.0"),
            };
        }

        return claims;
    }

    private static void AddIfPresent(Dictionary<string, object> claims, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            claims[name] = value;
        }
    }
}
