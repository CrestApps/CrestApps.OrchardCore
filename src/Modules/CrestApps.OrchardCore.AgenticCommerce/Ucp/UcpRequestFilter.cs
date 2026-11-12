using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Signs a UCP request in and agrees its version and capabilities, before the endpoint runs: it reads the body once,
/// finds the platform (API key, OAuth bearer token or an RFC 9421 signature with a key from the platform's profile),
/// fetches the platform's profile named in <c>UCP-Agent</c>, and checks the version. Nothing is fetched for a caller
/// that isn't signed in, or whose signed request names a profile of a platform the store doesn't sell to.
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
        string requestedVersion = null;
        var agentHeader = request.Headers[UcpConstants.AgentHeader].ToString();

        if (!string.IsNullOrWhiteSpace(agentHeader))
        {
            if (!StructuredFieldParser.TryParseDictionary(agentHeader, out var agent) || agent.GetString("profile") is not { } profile)
            {
                return Fail(httpContext, body, StatusCodes.Status400BadRequest, "invalid_profile_url", "The UCP-Agent header doesn't name a profile.");
            }

            // The version is a member of its own, or a parameter of the profile (profile="..."; version="...").
            requestedVersion = agent.GetString("version")
                ?? (agent.TryGetValue("profile", out var profileMember) && profileMember.Item?.Parameters is { } parameters && parameters.TryGetValue("version", out var version) ? version as string : null);

            // A profile that isn't a web address (a placeholder such as "...") names nothing to read: the platform signs
            // in with its key, and the version it asks for is agreed with everything the store offers.
            profileUrl = Uri.TryCreate(profile, UriKind.Absolute, out var profileUri) && (profileUri.Scheme == Uri.UriSchemeHttps || profileUri.Scheme == Uri.UriSchemeHttp)
                ? profile
                : null;
        }

        // The caller is authenticated before anything is fetched: an anonymous request must not make the store read
        // (or cache) a profile address it chose. Only a signed-in platform's profile, or the profile of a signed request
        // from a platform the store sells to, is read.
        var authentication = await services.GetRequiredService<AgentPlatformAuthenticator>().AuthenticateAsync(httpContext, AgenticCommerceConstants.Protocols.Ucp);

        if (!authentication.Succeeded && authentication.Error is not null)
        {
            return Fail(httpContext, body, authentication.StatusCode, authentication.StatusCode == StatusCodes.Status401Unauthorized ? "unauthorized" : "forbidden", authentication.Error);
        }

        var signed = !string.IsNullOrEmpty(request.Headers["Signature-Input"]);
        AgentAuthentication signatureAuthentication = null;

        if (authentication.Succeeded)
        {
            // A platform acts only for its own profile.
            if (profileUrl is not null && !string.IsNullOrEmpty(authentication.Platform.ProfileUrl) && !string.Equals(profileUrl, authentication.Platform.ProfileUrl, StringComparison.Ordinal))
            {
                return Fail(httpContext, body, StatusCodes.Status403Forbidden, "forbidden", "The UCP-Agent profile isn't the platform's.");
            }
        }
        else if (signed && profileUrl is not null)
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

            signatureAuthentication = AgentPlatformAuthenticator.Check(platform, AgenticCommerceConstants.Protocols.Ucp, httpContext);

            if (!signatureAuthentication.Succeeded)
            {
                return Fail(httpContext, body, signatureAuthentication.StatusCode, "forbidden", signatureAuthentication.Error);
            }
        }
        else
        {
            httpContext.Response.Headers.WWWAuthenticate = "Bearer";

            return Fail(httpContext, body, StatusCodes.Status401Unauthorized, "unauthorized", "Sign in with the platform's API key or OAuth token, or sign the request.");
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

        if (signed)
        {
            var verified = await VerifyAsync(httpContext, body, platformProfile);

            if (verified is not null)
            {
                return verified;
            }

            authentication = authentication.Succeeded ? authentication : signatureAuthentication;
        }

        var signedIn = authentication.Platform;

        if (platformProfile is null && !string.IsNullOrEmpty(signedIn.ProfileUrl))
        {
            profileUrl = signedIn.ProfileUrl;
            platformProfile = (await profiles.GetAsync(profileUrl, httpContext.RequestAborted)).Profile;
        }

        var negotiation = await services.GetRequiredService<UcpNegotiator>().NegotiateAsync(platformProfile, requestedVersion);

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

        agentRequest.Context.PlatformProfile = platformProfile;

        // A checkout made while AP2 is agreed keeps needing a mandate, whatever later requests negotiate.
        if (agentRequest.Has(Ap2.Ap2ProfileContributor.GetName(negotiation.Version)))
        {
            agentRequest.Context.Properties[Ap2.Ap2CompletionGate.RequiredProperty] = "true";
        }

        agentRequest.Context.Properties[UcpProfileClient.ProfileUrlProperty] = profileUrl;
        agentRequest.Context.Properties[UcpProfileClient.WebhookUrlProperty] = UcpProfileClient.GetOrderWebhookUrl(platformProfile) ?? signedIn.WebhookUrl;
        agentRequest.Set(httpContext);
        httpContext.User = AgentPrincipal.Create(signedIn.ItemId);

        return await next(context);
    }

    // A signed request is checked against the key it names in the platform's profile, and its body against its digest;
    // the signature must cover the request's target and body, be fresh, and not have been used before.
    private static async Task<IResult> VerifyAsync(HttpContext httpContext, byte[] body, JsonObject platformProfile)
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
            var now = new DateTimeOffset(DateTime.SpecifyKind(httpContext.RequestServices.GetRequiredService<IClock>().UtcNow, DateTimeKind.Utc));
            var idempotencyKey = request.Headers["Idempotency-Key"].ToString();

            if (UcpSignaturePolicy.Check(verification, body.Length > 0, idempotencyKey, now) is { } policyError)
            {
                return Fail(httpContext, body, StatusCodes.Status401Unauthorized, "signature_invalid", policyError);
            }

            if (!await httpContext.RequestServices.GetRequiredService<AgentSignatureReplayGuard>().TryUseAsync(request.Headers["Signature"].ToString(), idempotencyKey, UcpSignaturePolicy.ReplayWindow, httpContext.RequestAborted))
            {
                return Fail(httpContext, body, StatusCodes.Status401Unauthorized, "signature_invalid", "The signature was used before. Sign each request anew.");
            }

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
