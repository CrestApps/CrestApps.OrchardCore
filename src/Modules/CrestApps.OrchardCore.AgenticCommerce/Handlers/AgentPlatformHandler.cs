using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Acp;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce.Handlers;

/// <summary>
/// Stamps agent platforms, reads them from recipes and checks them. Credentials never come from a recipe: an imported
/// platform gets a new API key in the admin.
/// </summary>
internal sealed class AgentPlatformHandler : CatalogEntryHandlerBase<AgentPlatform>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public AgentPlatformHandler(
        IClock clock,
        IStringLocalizer<AgentPlatformHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AgentPlatform> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;
        Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<AgentPlatform> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;
        Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<AgentPlatform> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(AgentPlatform.Name)]));
        }

        if (context.Model.Protocols is null || context.Model.Protocols.Count == 0)
        {
            context.Result.Fail(new ValidationResult(S["Choose at least one protocol."], [nameof(AgentPlatform.Protocols)]));
        }

        foreach (var url in new[] { context.Model.ProfileUrl, context.Model.WebhookUrl })
        {
            if (!string.IsNullOrWhiteSpace(url) && (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                context.Result.Fail(new ValidationResult(S["Enter a full address that starts with https://."], [url == context.Model.ProfileUrl ? nameof(AgentPlatform.ProfileUrl) : nameof(AgentPlatform.WebhookUrl)]));
            }
        }

        ValidateRequestSigning(context);

        var invalidOrigins = (context.Model.EmbeddedCheckoutOrigins ?? [])
            .Where(origin => EmbeddingPolicy.NormalizeOrigin(origin) is null)
            .ToList();

        if (invalidOrigins.Count > 0)
        {
            context.Result.Fail(new ValidationResult(
                S["Enter each embedding origin as https:// and a host, with no path, such as https://agent.example. Not valid: {0}", string.Join(", ", invalidOrigins)],
                [nameof(AgentPlatform.EmbeddedCheckoutOrigins)]));
        }

        return Task.CompletedTask;
    }

    private void ValidateRequestSigning(ValidatingContext<AgentPlatform> context)
    {
        var platform = context.Model;

        if (!AcpSignatureCheck.IsSupported(platform.AcpSignatureAlgorithm))
        {
            context.Result.Fail(new ValidationResult(S["Choose an algorithm the store verifies: {0}.", string.Join(", ", AcpRequestSignature.Algorithms)], [nameof(AgentPlatform.AcpSignatureAlgorithm)]));
        }

        if (!string.IsNullOrWhiteSpace(platform.AcpSignaturePublicKey))
        {
            if (!AcpRequestSignature.TryReadPublicKey(platform.AcpSignaturePublicKey, out _))
            {
                context.Result.Fail(new ValidationResult(S["Enter a P-256 public key: a JSON Web Key with kty EC and crv P-256, or a PEM block that starts with -----BEGIN PUBLIC KEY-----. Never enter a private key."], [nameof(AgentPlatform.AcpSignaturePublicKey)]));
            }
        }
        else if (platform.AcpRequireSignedRequests)
        {
            context.Result.Fail(new ValidationResult(S["Enter the platform's public key to require signed requests."], [nameof(AgentPlatform.AcpSignaturePublicKey)]));
        }

        if (platform.AcpSignatureClockSkewSeconds is int seconds && (seconds < 1 || seconds > 3600))
        {
            context.Result.Fail(new ValidationResult(S["The allowed clock skew is 1 to 3600 seconds."], [nameof(AgentPlatform.AcpSignatureClockSkewSeconds)]));
        }
    }

    private static void Populate(AgentPlatform model, JsonNode data)
    {
        if (data is not JsonObject json)
        {
            return;
        }

        model.Name = json[nameof(AgentPlatform.Name)]?.GetValue<string>()?.Trim() ?? model.Name;
        model.Description = json[nameof(AgentPlatform.Description)]?.GetValue<string>() ?? model.Description;
        model.Enabled = json[nameof(AgentPlatform.Enabled)]?.GetValue<bool?>() ?? model.Enabled;
        model.TestMode = json[nameof(AgentPlatform.TestMode)]?.GetValue<bool?>() ?? model.TestMode;
        model.OAuthClientId = json[nameof(AgentPlatform.OAuthClientId)]?.GetValue<string>()?.Trim() ?? model.OAuthClientId;
        model.ProfileUrl = json[nameof(AgentPlatform.ProfileUrl)]?.GetValue<string>()?.Trim() ?? model.ProfileUrl;
        model.WebhookUrl = json[nameof(AgentPlatform.WebhookUrl)]?.GetValue<string>()?.Trim() ?? model.WebhookUrl;
        model.AcpOrderCreatedEvent = json[nameof(AgentPlatform.AcpOrderCreatedEvent)]?.GetValue<string>()?.Trim() ?? model.AcpOrderCreatedEvent;
        model.AcpOrderUpdatedEvent = json[nameof(AgentPlatform.AcpOrderUpdatedEvent)]?.GetValue<string>()?.Trim() ?? model.AcpOrderUpdatedEvent;
        model.AcpRequireSignedRequests = json[nameof(AgentPlatform.AcpRequireSignedRequests)]?.GetValue<bool?>() ?? model.AcpRequireSignedRequests;
        model.AcpSignatureAlgorithm = json[nameof(AgentPlatform.AcpSignatureAlgorithm)]?.GetValue<string>()?.Trim() ?? model.AcpSignatureAlgorithm;
        model.AcpSignaturePublicKey = json[nameof(AgentPlatform.AcpSignaturePublicKey)]?.GetValue<string>()?.Trim() ?? model.AcpSignaturePublicKey;
        model.AcpSignatureClockSkewSeconds = json[nameof(AgentPlatform.AcpSignatureClockSkewSeconds)]?.GetValue<int?>() ?? model.AcpSignatureClockSkewSeconds;
        model.Protocols = ReadList(json[nameof(AgentPlatform.Protocols)]) ?? model.Protocols;
        model.AllowedPaymentHandlers = ReadList(json[nameof(AgentPlatform.AllowedPaymentHandlers)]) ?? model.AllowedPaymentHandlers;
        model.AllowedIpRanges = ReadList(json[nameof(AgentPlatform.AllowedIpRanges)]) ?? model.AllowedIpRanges;
        model.EmbeddedCheckoutOrigins = NormalizeOrigins(ReadList(json[nameof(AgentPlatform.EmbeddedCheckoutOrigins)])) ?? model.EmbeddedCheckoutOrigins;
    }

    /// <summary>
    /// Returns origins in the shape browsers report them in, keeping an entry that isn't a valid origin as it was
    /// entered, so validation names it.
    /// </summary>
    /// <param name="values">The origins as entered.</param>
    internal static List<string> NormalizeOrigins(List<string> values)
        => values?
            .Select(value => EmbeddingPolicy.NormalizeOrigin(value) ?? value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static List<string> ReadList(JsonNode node)
        => node is JsonArray array
            ? array.Select(entry => entry?.GetValue<string>()?.Trim()).Where(entry => !string.IsNullOrEmpty(entry)).ToList()
            : null;
}
