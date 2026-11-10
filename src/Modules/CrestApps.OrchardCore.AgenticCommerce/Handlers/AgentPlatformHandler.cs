using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
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

        return Task.CompletedTask;
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
        model.Protocols = ReadList(json[nameof(AgentPlatform.Protocols)]) ?? model.Protocols;
        model.AllowedPaymentHandlers = ReadList(json[nameof(AgentPlatform.AllowedPaymentHandlers)]) ?? model.AllowedPaymentHandlers;
        model.AllowedIpRanges = ReadList(json[nameof(AgentPlatform.AllowedIpRanges)]) ?? model.AllowedIpRanges;
    }

    private static List<string> ReadList(JsonNode node)
        => node is JsonArray array
            ? array.Select(entry => entry?.GetValue<string>()?.Trim()).Where(entry => !string.IsNullOrEmpty(entry)).ToList()
            : null;
}
