using CrestApps.Core.AI;
using CrestApps.OrchardCore.AgenticCommerce.AI;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Records;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce;

/// <summary>
/// Registers the agentic commerce admin AI tools when the AI feature is on: the AI agent platforms as the
/// <c>agentPlatform</c> kind of the generic record tools (<c>listRecords</c>, <c>getRecord</c>, <c>saveRecord</c>,
/// <c>deleteRecord</c>), whose writes replay the platform editor of Commerce › AI agents, and <c>listAgentCheckouts</c>.
/// Unlike the storefront tools of <see cref="AIToolsStartup"/>, these need a signed-in caller with
/// <see cref="AgenticCommercePermissions.ManageAgentPlatforms"/> and do not need the product listing and feeds features.
/// </summary>
[Feature(AgenticCommerceConstants.Features.AgenticCommerce)]
[RequireFeatures(AIConstants.Feature.Area)]
public sealed class AdminAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdminAIToolsStartup(IStringLocalizer<AdminAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListAgentCheckoutsTool>(ListAgentCheckoutsTool.TheName)
            .WithTitle(S["List Agent Checkouts"])
            .WithDescription(S["Lists the checkouts AI agent platforms (ChatGPT, Gemini and others) opened in the store, newest first, filtered by platform, open or ended, and whether they became an order; or explains one checkout by its id or its order: platform, protocol, status, the order it became, times, and why it stopped (the payment message the agent got). Use it for \"which orders came from AI agents\" and \"why did this agent checkout fail\"."])
            .WithCategory(S["Agentic Commerce"])
            .Selectable();

        // The API key and the webhook secret are not editor fields: they are issued by the page's own buttons and shown
        // once, so the record tools never see or set them. A platform is stopped by turning it off (enabled: false).
        services.AddAIRecordKind<AgentPlatform>("agentPlatform", kind => kind
            .WithTitle(S["AI agent platform"])
            .WithDescription(S["an AI agent platform (ChatGPT, Gemini...) allowed to buy from the store through UCP or ACP: whether its agents may buy, test mode, protocols, OAuth client, profile and order-updates webhook addresses, allowed payment handlers, IP ranges, embedded checkout origins and ACP request signing. Its API key and webhook secret are issued on the AI agents admin page only"])
            .WithPermission(AgenticCommercePermissions.ManageAgentPlatforms)
            .UseCatalogManager<IAgentPlatformManager>()
            .WithProjection(platform => new
            {
                id = platform.ItemId,
                name = platform.Name,
                description = platform.Description,
                enabled = platform.Enabled,
                testMode = platform.TestMode,
                protocols = platform.Protocols ?? [],
                oauthClientId = platform.OAuthClientId,
                profileUrl = platform.ProfileUrl,
                webhookUrl = platform.WebhookUrl,
                allowedPaymentHandlers = platform.AllowedPaymentHandlers ?? [],
                allowedIpRanges = platform.AllowedIpRanges ?? [],
                embeddedCheckoutOrigins = platform.EmbeddedCheckoutOrigins ?? [],
                acpOrderCreatedEvent = platform.AcpOrderCreatedEvent,
                acpOrderUpdatedEvent = platform.AcpOrderUpdatedEvent,
                acpRequireSignedRequests = platform.AcpRequireSignedRequests,
                acpSignatureAlgorithm = platform.AcpSignatureAlgorithm,
                hasAcpSignaturePublicKey = !string.IsNullOrEmpty(platform.AcpSignaturePublicKey),
                acpSignatureClockSkewSeconds = platform.AcpSignatureClockSkewSeconds,
                hasApiKey = !string.IsNullOrEmpty(platform.ApiKeyHash),
                apiKeyHint = platform.ApiKeyHint,
                hasWebhookSecret = !string.IsNullOrEmpty(platform.ProtectedWebhookSecret),
            })
            .UseEditorReplay());
    }
}
