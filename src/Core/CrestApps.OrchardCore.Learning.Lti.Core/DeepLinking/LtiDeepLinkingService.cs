using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Lti.Core.DeepLinking;

/// <summary>
/// Checks a tool's Deep Linking response (Deep Linking 2.0, 4.5): the JWT is signed by the tool's key, its issuer is the
/// tool's client id and its audience the platform, <c>exp</c> and <c>iat</c> are current, the message type and version
/// are a Deep Linking response, the deployment id is the tool's, the nonce was not seen before, and the <c>data</c> is the
/// platform's own, signed, unexpired, for this tool and used once. Of the content items, only <c>ltiResourceLink</c> is
/// accepted; the types of the others are reported.
/// </summary>
public sealed class LtiDeepLinkingService
{
    private readonly ILtiToolDirectory _tools;
    private readonly ILtiPlatform _platform;
    private readonly LtiNonceStore _nonces;
    private readonly LtiDeepLinkingDataTokens _data;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LtiDeepLinkingService"/> class.
    /// </summary>
    /// <param name="tools">The tool directory.</param>
    /// <param name="platform">The platform.</param>
    /// <param name="nonces">The nonce store.</param>
    /// <param name="data">The Deep Linking data tokens.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public LtiDeepLinkingService(
        ILtiToolDirectory tools,
        ILtiPlatform platform,
        LtiNonceStore nonces,
        LtiDeepLinkingDataTokens data,
        IClock clock,
        ILogger<LtiDeepLinkingService> logger)
    {
        _tools = tools;
        _platform = platform;
        _nonces = nonces;
        _data = data;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Checks a Deep Linking response.
    /// </summary>
    /// <param name="jwt">The <c>JWT</c> form field the tool posted.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The response; <see cref="LtiDeepLinkingResponse.Error"/> says why when it was refused.</returns>
    public async Task<LtiDeepLinkingResponse> ReadResponseAsync(string jwt, CancellationToken cancellationToken = default)
    {
        var response = new LtiDeepLinkingResponse();

        if (string.IsNullOrWhiteSpace(jwt) || jwt.Length > 64 * 1024)
        {
            return Refuse(response, "The response has no JWT.");
        }

        JsonWebToken unverified;

        try
        {
            unverified = new JsonWebToken(jwt);
        }
        catch (ArgumentException)
        {
            return Refuse(response, "The JWT cannot be read.");
        }

        var tool = await _tools.FindByClientIdAsync(unverified.Issuer, cancellationToken);

        if (tool is not { Enabled: true })
        {
            return Refuse(response, "The JWT was not issued by a registered external tool.");
        }

        response.Tool = tool;

        var platform = await _platform.GetAsync();
        var keys = await _tools.GetSigningKeysAsync(tool, refresh: false, cancellationToken);

        if (!string.IsNullOrEmpty(unverified.Kid) && !keys.Any(key => string.Equals(key.KeyId, unverified.Kid, StringComparison.Ordinal)))
        {
            keys = await _tools.GetSigningKeysAsync(tool, refresh: true, cancellationToken);
        }

        var validation = await LtiTokens.ValidateAsync(jwt, tool.ClientId, platform.Issuer, keys, _clock.UtcNow);

        if (!validation.IsValid || validation.SecurityToken is not JsonWebToken token)
        {
            return Refuse(response, "The JWT is not signed by the tool's key, or its issuer, audience or times are not valid.");
        }

        if (!string.Equals(Claim(token, LtiConstants.Claims.MessageType), LtiConstants.MessageTypes.DeepLinkingResponse, StringComparison.Ordinal) ||
            !string.Equals(Claim(token, LtiConstants.Claims.Version), LtiConstants.Version, StringComparison.Ordinal))
        {
            return Refuse(response, "The JWT is not an LTI 1.3.0 Deep Linking response.");
        }

        if (!string.Equals(Claim(token, LtiConstants.Claims.DeploymentId), tool.DeploymentId, StringComparison.Ordinal))
        {
            return Refuse(response, "The deployment id is not the tool's.");
        }

        if (!await _nonces.TryUseAsync("deep-linking:" + tool.ClientId, Claim(token, "nonce"), DateTime.SpecifyKind(token.ValidTo, DateTimeKind.Utc), cancellationToken))
        {
            return Refuse(response, "The nonce is missing or was used before.");
        }

        var data = _data.Read(Claim(token, LtiConstants.Claims.Data));

        if (data is null || !string.Equals(data.ToolId, tool.ItemId, StringComparison.Ordinal))
        {
            return Refuse(response, "The data is not the platform's, or it expired.");
        }

        // The data id is remembered while the data is valid, so the same request cannot be answered twice (review
        // round 9, M5).
        if (!await _nonces.TryUseAsync("deep-linking-data", data.Id, data.ExpiresUtc, cancellationToken))
        {
            return Refuse(response, "This Deep Linking request was answered before.");
        }

        response.Data = data;
        response.Message = Claim(token, LtiConstants.Claims.Msg);
        response.ErrorMessage = Claim(token, LtiConstants.Claims.ErrorMsg);

        // A JSON array claim reads as one claim per element, so the payload value is read whole.
        if (token.TryGetPayloadValue<JsonElement>(LtiConstants.Claims.ContentItems, out var itemsElement) &&
            itemsElement.ValueKind == JsonValueKind.Array &&
            JsonNode.Parse(itemsElement.GetRawText()) is JsonArray items)
        {
            foreach (var node in items)
            {
                if (node is not JsonObject item)
                {
                    continue;
                }

                var type = item["type"]?.GetValueKind() == JsonValueKind.String ? item["type"].GetValue<string>() : null;

                if (!string.Equals(type, "ltiResourceLink", StringComparison.Ordinal))
                {
                    response.RefusedTypes.Add(type ?? "(none)");

                    continue;
                }

                response.Items.Add(Map(item));
            }
        }

        return response;
    }

    private static LtiDeepLinkItem Map(JsonObject item)
    {
        var result = new LtiDeepLinkItem
        {
            Title = Text(item, "title"),
            Text = Text(item, "text"),
            Json = item.ToJsonString(),
        };

        if (Text(item, "url") is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            result.Url = url;
        }

        if (item["custom"] is JsonObject custom)
        {
            var lines = new StringBuilder();

            foreach (var (name, value) in custom)
            {
                if (string.IsNullOrWhiteSpace(name) || name.Contains('=') || value is null)
                {
                    continue;
                }

                var text = value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : value.ToJsonString();

                lines.Append(name).Append('=').Append(text.Replace('\r', ' ').Replace('\n', ' ')).Append('\n');
            }

            result.CustomParameters = lines.ToString().TrimEnd('\n');
        }

        if (item["lineItem"] is JsonObject lineItem)
        {
            result.ScoreMaximum = lineItem["scoreMaximum"] is JsonValue maximum && maximum.TryGetValue<decimal>(out var value) && value > 0 ? value : 100;
            result.LineItemLabel = Text(lineItem, "label");
            result.LineItemResourceId = Text(lineItem, "resourceId");
            result.LineItemTag = Text(lineItem, "tag");
        }

        return result;
    }

    private LtiDeepLinkingResponse Refuse(LtiDeepLinkingResponse response, string error)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("A Deep Linking response was refused: {Error}", error);
        }

        response.Error = error;

        return response;
    }

    private static string Claim(JsonWebToken token, string name)
        => token.TryGetClaim(name, out var claim) && !string.IsNullOrEmpty(claim.Value) ? claim.Value : null;

    private static string Text(JsonObject json, string name)
        => json[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String && value.GetValue<string>() is { Length: > 0 } text
            ? text.Trim()
            : json[name] is JsonValue number && number.GetValueKind() == JsonValueKind.Number
                ? number.ToJsonString()
                : null;
}
