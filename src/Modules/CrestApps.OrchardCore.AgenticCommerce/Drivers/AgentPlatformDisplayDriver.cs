using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AgenticCommerce.Drivers;

internal sealed class AgentPlatformDisplayDriver : DisplayDriver<AgentPlatform>
{
    internal readonly IStringLocalizer S;

    public AgentPlatformDisplayDriver(IStringLocalizer<AgentPlatformDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AgentPlatform platform, BuildDisplayContext context)
    {
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = platform.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("AgentPlatform_Fields_SummaryAdmin", platform)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AgentPlatform_Description_SummaryAdmin", platform)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrWhiteSpace(platform.Description))),
            View("AgentPlatform_DefaultTags_SummaryAdmin", platform)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Protocols:5"),
            View("AgentPlatform_Buttons_SummaryAdmin", platform).Location("Actions:5"),
            View("AgentPlatform_DefaultMeta_SummaryAdmin", platform)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5")
        );
    }

    public override IDisplayResult Edit(AgentPlatform platform, BuildEditorContext context)
        => Initialize<AgentPlatformViewModel>("AgentPlatformFields_Edit", model =>
        {
            model.ItemId = platform.ItemId;
            model.IsNew = context.IsNew;
            model.Name = platform.Name;
            model.Description = platform.Description;
            model.Enabled = platform.Enabled;
            model.TestMode = platform.TestMode;
            model.Protocols = [.. platform.Protocols ?? []];
            model.OAuthClientId = platform.OAuthClientId;
            model.ProfileUrl = platform.ProfileUrl;
            model.WebhookUrl = platform.WebhookUrl;
            model.AllowedPaymentHandlers = string.Join(Environment.NewLine, platform.AllowedPaymentHandlers ?? []);
            model.AllowedIpRanges = string.Join(Environment.NewLine, platform.AllowedIpRanges ?? []);
            model.AcpOrderCreatedEvent = platform.AcpOrderCreatedEvent;
            model.AcpOrderUpdatedEvent = platform.AcpOrderUpdatedEvent;
            model.AcpRequireSignedRequests = platform.AcpRequireSignedRequests;
            model.AcpSignatureAlgorithm = platform.AcpSignatureAlgorithm ?? AcpRequestSignature.ES256;
            model.AcpSignaturePublicKey = platform.AcpSignaturePublicKey;
            model.AcpSignatureClockSkewSeconds = platform.AcpSignatureClockSkewSeconds;
            model.SignatureAlgorithmOptions = [.. AcpRequestSignature.Algorithms.Select(algorithm => new SelectListItem(algorithm, algorithm))];
            model.ApiKeyHint = platform.ApiKeyHint;
            model.HasWebhookSecret = !string.IsNullOrEmpty(platform.ProtectedWebhookSecret);
            model.ProtocolOptions =
            [
                new SelectListItem(S["Universal Commerce Protocol (UCP)"], AgenticCommerceConstants.Protocols.Ucp, platform.Uses(AgenticCommerceConstants.Protocols.Ucp)),
                new SelectListItem(S["Agentic Commerce Protocol (ACP)"], AgenticCommerceConstants.Protocols.Acp, platform.Uses(AgenticCommerceConstants.Protocols.Acp)),
            ];
        }).Location("Content:1");

    public override async Task<IDisplayResult> UpdateAsync(AgentPlatform platform, UpdateEditorContext context)
    {
        var model = new AgentPlatformViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        platform.Name = model.Name?.Trim();
        platform.Description = model.Description?.Trim();
        platform.Enabled = model.Enabled;
        platform.TestMode = model.TestMode;
        platform.Protocols = (model.Protocols ?? [])
            .Where(protocol => protocol is AgenticCommerceConstants.Protocols.Ucp or AgenticCommerceConstants.Protocols.Acp)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        platform.OAuthClientId = Clean(model.OAuthClientId);
        platform.ProfileUrl = Clean(model.ProfileUrl);
        platform.WebhookUrl = Clean(model.WebhookUrl);
        platform.AllowedPaymentHandlers = Lines(model.AllowedPaymentHandlers);
        platform.AllowedIpRanges = Lines(model.AllowedIpRanges);
        platform.AcpOrderCreatedEvent = Clean(model.AcpOrderCreatedEvent);
        platform.AcpOrderUpdatedEvent = Clean(model.AcpOrderUpdatedEvent);
        platform.AcpRequireSignedRequests = model.AcpRequireSignedRequests;
        platform.AcpSignatureAlgorithm = Clean(model.AcpSignatureAlgorithm);
        platform.AcpSignaturePublicKey = Clean(model.AcpSignaturePublicKey);
        platform.AcpSignatureClockSkewSeconds = model.AcpSignatureClockSkewSeconds;

        return Edit(platform, context);
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> Lines(string value)
        => (value ?? string.Empty)
            .Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
