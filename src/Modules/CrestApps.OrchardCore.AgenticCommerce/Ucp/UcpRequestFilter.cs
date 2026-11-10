using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Signs a UCP request in and agrees its version and capabilities, before the endpoint runs: it reads the body once,
/// finds the platform (API key, OAuth bearer token or an RFC 9421 signature with a key from the platform's profile),
/// fetches the platform's profile named in <c>UCP-Agent</c>, and checks the version.
/// </summary>
public sealed class UcpRequestFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var services = httpContext.RequestServices;
        var request = httpContext.Request;
        var body = await AgentRequestBody.ReadAsync(request);

        if (body is null)
        {
            return Fail(httpContext, body, StatusCodes.Status413PayloadTooLarge, "request_too_large", "The request is too large.");
        }

        string profileUrl = null;
        var agentHeader = request.Headers[UcpConstants.AgentHeader].ToString();

        if (!string.IsNullOrWhiteSpace(agentHeader))
        {
            if (!StructuredFieldParser.TryParseDictionary(agentHeader, out var agent) || agent.GetString("profile") is not { } profile)
            {
                return Fail(httpContext, body, StatusCodes.Status400BadRequest, "invalid_profile_url", "The UCP-Agent header doesn't name a profile.");
            }

            profileUrl = profile;
        }

        var profiles = services.GetRequiredService<UcpProfileClient>();
        JsonObject platformProfile = null;

        if (profileUrl is not null)
        {
            var (fetched, error) = await profiles.GetAsync(profileUrl, httpContext.RequestAborted);

            if (fetched is null)
            {
                return Fail(
                    httpContext,
                    body,
                    error == "profile_unreachable" ? StatusCodes.Status424FailedDependency : error == "invalid_profile_url" ? StatusCodes.Status400BadRequest : StatusCodes.Status422UnprocessableEntity,
                    error,
                    "The platform's UCP profile can't be read.");
            }

            platformProfile = fetched;
        }

        var authentication = await services.GetRequiredService<AgentPlatformAuthenticator>().AuthenticateAsync(httpContext, AgenticCommerceConstants.Protocols.Ucp);

        if (!authentication.Succeeded && authentication.Error is not null)
        {
            return Fail(httpContext, body, authentication.StatusCode, authentication.StatusCode == StatusCodes.Status401Unauthorized ? "unauthorized" : "forbidden", authentication.Error);
        }

        if (!string.IsNullOrEmpty(request.Headers["Signature-Input"]))
        {
            var verified = Verify(httpContext, body, platformProfile);

            if (verified is not null)
            {
                return verified;
            }

            if (!authentication.Succeeded)
            {
                var platform = await services.GetRequiredService<IAgentPlatformStore>().FindByProfileUrlAsync(profileUrl, httpContext.RequestAborted);

                if (platform is null && (await services.GetRequiredService<ISiteService>().GetSettingsAsync<AgenticCommerceSettings>()).AllowUnregisteredUcpPlatforms)
                {
                    platform = new AgentPlatform
                    {
                        ItemId = UcpProfileClient.GetUnregisteredPlatformId(profileUrl),
                        Name = profileUrl,
                        Enabled = true,
                        Protocols = [AgenticCommerceConstants.Protocols.Ucp],
                        ProfileUrl = profileUrl,
                    };
                }

                if (platform is null)
                {
                    return Fail(httpContext, body, StatusCodes.Status403Forbidden, "forbidden", "The platform isn't one this store sells to.");
                }

                authentication = AgentPlatformAuthenticator.Check(platform, AgenticCommerceConstants.Protocols.Ucp, httpContext);

                if (!authentication.Succeeded)
                {
                    return Fail(httpContext, body, authentication.StatusCode, "forbidden", authentication.Error);
                }
            }
        }

        if (!authentication.Succeeded)
        {
            httpContext.Response.Headers.WWWAuthenticate = "Bearer";

            return Fail(httpContext, body, StatusCodes.Status401Unauthorized, "unauthorized", "Sign in with the platform's API key or OAuth token, or sign the request.");
        }

        var signedIn = authentication.Platform;

        // A platform acts only for its own profile.
        if (profileUrl is not null && !string.IsNullOrEmpty(signedIn.ProfileUrl) && !string.Equals(profileUrl, signedIn.ProfileUrl, StringComparison.Ordinal))
        {
            return Fail(httpContext, body, StatusCodes.Status403Forbidden, "forbidden", "The UCP-Agent profile isn't the platform's.");
        }

        if (platformProfile is null && !string.IsNullOrEmpty(signedIn.ProfileUrl))
        {
            profileUrl = signedIn.ProfileUrl;
            platformProfile = (await profiles.GetAsync(profileUrl, httpContext.RequestAborted)).Profile;
        }

        var negotiation = await services.GetRequiredService<UcpNegotiator>().NegotiateAsync(platformProfile);

        if (negotiation.Version is null)
        {
            return Fail(httpContext, body, StatusCodes.Status422UnprocessableEntity, "version_unsupported", $"The store serves UCP {string.Join(" and ", UcpConstants.Versions)}.");
        }

        var linkedUserId = authentication.UserId;
        var scopes = authentication.Scopes ?? [];

        var agentRequest = new AgentRequest
        {
            Platform = signedIn,
            Scopes = scopes,
            LinkedUserId = linkedUserId,
            Body = body,
            Json = AgentRequestBody.Parse(body),
            Version = negotiation.Version,
            Capabilities = negotiation.Capabilities,
            PlatformProfile = platformProfile,
            ProfileUrl = profileUrl,
            Context = new AgentCheckoutContext
            {
                PlatformId = signedIn.ItemId,
                Protocol = AgenticCommerceConstants.Protocols.Ucp,
                ProtocolVersion = negotiation.Version,
                TestMode = signedIn.TestMode,
                AllowedPaymentHandlers = signedIn.AllowedPaymentHandlers ?? [],
                UserId = linkedUserId is not null && scopes.Contains(UcpIdentityScopes.CheckoutManage) ? linkedUserId : null,
                ClientAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            },
        };

        agentRequest.Context.Properties[UcpProfileClient.ProfileUrlProperty] = profileUrl;
        agentRequest.Context.Properties[UcpProfileClient.WebhookUrlProperty] = UcpProfileClient.GetOrderWebhookUrl(platformProfile) ?? signedIn.WebhookUrl;
        agentRequest.Set(httpContext);
        httpContext.User = AgentPrincipal.Create(signedIn.ItemId);

        return await next(context);
    }

    // A signed request is checked against the key it names in the platform's profile, and its body against its digest.
    private static IResult Verify(HttpContext httpContext, byte[] body, JsonObject platformProfile)
    {
        var request = httpContext.Request;

        if (body.Length > 0 && !ContentDigest.Verify(request.Headers["Content-Digest"].ToString(), body))
        {
            return Fail(httpContext, body, StatusCodes.Status400BadRequest, "digest_mismatch", "The body doesn't match its Content-Digest.");
        }

        var keys = JsonWebKeySet.Parse(platformProfile?["keys"]);
        var message = ToSignatureMessage(request);
        var verification = HttpMessageSignatures.Verify(
            message,
            request.Headers["Signature-Input"].ToString(),
            request.Headers["Signature"].ToString(),
            keyId => keys.FirstOrDefault(key => string.Equals(key.Kid, keyId, StringComparison.Ordinal)));

        if (verification.Succeeded)
        {
            return null;
        }

        return Fail(
            httpContext,
            body,
            verification.ErrorCode == "algorithm_unsupported" ? StatusCodes.Status400BadRequest : StatusCodes.Status401Unauthorized,
            verification.ErrorCode ?? "signature_invalid",
            "The request's signature can't be verified with the platform's keys.");
    }

    // A JSON-RPC binding (UCP over A2A) answers the same errors as JSON-RPC errors, with the same HTTP status.
    private static IResult Fail(HttpContext httpContext, byte[] body, int statusCode, string code, string content)
        => httpContext.GetEndpoint()?.Metadata.GetMetadata<UcpJsonRpcErrors>() is not null
            ? UcpJsonRpc.WriteTransportError(body, statusCode, code, content)
            : UcpResults.Error(statusCode, code, content);

    internal static HttpSignatureMessage ToSignatureMessage(HttpRequest request)
    {
        var message = new HttpSignatureMessage
        {
            Method = request.Method,
            Scheme = request.Scheme,
            Authority = request.Host.Value?.ToLowerInvariant(),
            Path = $"{request.PathBase}{request.Path}",
            Query = request.QueryString.HasValue ? request.QueryString.Value : null,
        };

        foreach (var (name, values) in request.Headers)
        {
            message.Headers[name] = values.ToString();
        }

        return message;
    }
}
