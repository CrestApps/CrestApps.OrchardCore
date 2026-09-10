using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Default <see cref="IContactCenterAgentBarBuilder"/> that resolves the hub URL, the shared workspace
/// endpoints, the complete-activity screen-pop template, and the inline disposition and reason-code options for
/// the docked agent bar.
/// </summary>
public sealed class ContactCenterAgentBarBuilder : IContactCenterAgentBarBuilder
{
    private const string ActivityIdToken = "__activityId__";

    private readonly LinkGenerator _linkGenerator;
    private readonly IAntiforgery _antiforgery;
    private readonly INamedCatalog<OmnichannelDisposition> _dispositionCatalog;
    private readonly IAgentStateReasonCodeManager _reasonCodeManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAgentBarBuilder"/> class.
    /// </summary>
    /// <param name="linkGenerator">The link generator used to resolve endpoint and screen-pop URLs.</param>
    /// <param name="antiforgery">The antiforgery service used to issue the bar's request token.</param>
    /// <param name="dispositionCatalog">The disposition catalog used to list quick-disposition options.</param>
    /// <param name="reasonCodeManagers">The optional agent state reason code managers, available when the Agents feature is enabled.</param>
    public ContactCenterAgentBarBuilder(
        LinkGenerator linkGenerator,
        IAntiforgery antiforgery,
        INamedCatalog<OmnichannelDisposition> dispositionCatalog,
        IEnumerable<IAgentStateReasonCodeManager> reasonCodeManagers)
    {
        _linkGenerator = linkGenerator;
        _antiforgery = antiforgery;
        _dispositionCatalog = dispositionCatalog;
        _reasonCodeManager = reasonCodeManagers.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<AgentBarViewModel> BuildAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var dispositions = await _dispositionCatalog.GetAllAsync(httpContext.RequestAborted);
        var reasonCodes = _reasonCodeManager is null
            ? []
            : await _reasonCodeManager.GetEnabledAsync(httpContext.RequestAborted);

        var tokens = _antiforgery.GetAndStoreTokens(httpContext);

        return new AgentBarViewModel
        {
            HubUrl = SignalRHubRoutes.GetTenantAwareHubUrl<ContactCenterHub>(httpContext),
            StateUrl = _linkGenerator.GetPathByName(httpContext, AgentWorkspaceEndpoints.StateRouteName),
            SetPresenceUrl = _linkGenerator.GetPathByName(httpContext, AgentWorkspaceEndpoints.SetPresenceRouteName),
            CompleteUrl = _linkGenerator.GetPathByName(httpContext, AgentWorkspaceEndpoints.CompleteRouteName),
            AcceptOfferUrl = _linkGenerator.GetPathByName(httpContext, VoiceOfferEndpoints.AcceptOfferRouteName),
            DeclineOfferUrl = _linkGenerator.GetPathByName(httpContext, VoiceOfferEndpoints.DeclineOfferRouteName),
            CompleteActivityUrlTemplate = BuildCompleteActivityTemplate(httpContext),
            WorkspaceUrl = _linkGenerator.GetPathByAction(
                httpContext,
                "Index",
                "AgentWorkspace",
                new { area = ContactCenterConstants.Feature.Area }),
            AntiForgeryToken = tokens.RequestToken,
            Dispositions = [.. dispositions.Select(disposition => new WorkspaceLookupViewModel
            {
                Id = disposition.ItemId,
                Name = disposition.Name,
            })],
            ReasonCodes = [.. reasonCodes.Select(code => new WorkspaceLookupViewModel
            {
                Id = code.AppliesTo.ToString(),
                Name = code.Name,
            })],
        };
    }

    private string BuildCompleteActivityTemplate(HttpContext httpContext)
    {
        // Return the agent to wherever the bar popped them from, rather than to the workspace, so completing an
        // activity does not yank them out of the page they were on when the work arrived.
        var returnUrl = httpContext.Request.PathBase.Add(httpContext.Request.Path).Value + httpContext.Request.QueryString.Value;

        return _linkGenerator.GetPathByAction(
            httpContext,
            "Complete",
            "Activities",
            new
            {
                area = OmnichannelConstants.Features.Managements,
                id = ActivityIdToken,
                returnUrl,
            });
    }
}
