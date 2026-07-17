using System;
using System.Linq;
using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Default transfer destination resolver that fails closed for unsafe or unapproved external targets.
/// External transfers are resolved exclusively from the tenant's server-side approved-destination
/// catalog; callers supply only an opaque catalog entry identifier, never a raw phone number.
/// </summary>
public sealed class TransferDestinationResolver : ITransferDestinationResolver
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IAgentProfileManager _agentManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferDestinationResolver"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service used for external transfer RBAC.</param>
    /// <param name="agentManager">The agent profile manager used to resolve agent destinations.</param>
    /// <param name="queueManager">The queue manager used to resolve queue destinations.</param>
    /// <param name="siteService">The site service used to read the tenant-scoped approved-destination catalog.</param>
    public TransferDestinationResolver(
        IAuthorizationService authorizationService,
        IAgentProfileManager agentManager,
        IActivityQueueManager queueManager,
        ISiteService siteService)
    {
        _authorizationService = authorizationService;
        _agentManager = agentManager;
        _queueManager = queueManager;
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public async Task<TransferDestinationResolutionResult> ResolveAsync(
        TransferRequest request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TargetId))
        {
            return TransferDestinationResolutionResult.Denied();
        }

        return request.TargetType switch
        {
            InteractionTransferTargetType.Agent => await ResolveAgentAsync(request.TargetId, cancellationToken),
            InteractionTransferTargetType.Queue => await ResolveQueueAsync(request.TargetId, cancellationToken),
            InteractionTransferTargetType.EntryPoint => ResolveEntryPoint(request.TargetId),
            InteractionTransferTargetType.External => await ResolveExternalAsync(request.TargetId, principal),
            _ => TransferDestinationResolutionResult.Denied(),
        };
    }

    private async Task<TransferDestinationResolutionResult> ResolveAgentAsync(
        string agentId,
        CancellationToken cancellationToken)
    {
        var agent = await _agentManager.FindByIdAsync(agentId, cancellationToken);

        return agent is null
            ? TransferDestinationResolutionResult.Denied()
            : TransferDestinationResolutionResult.Success(InteractionTransferTargetType.Agent, agent.ItemId);
    }

    private async Task<TransferDestinationResolutionResult> ResolveQueueAsync(
        string queueId,
        CancellationToken cancellationToken)
    {
        var queue = await _queueManager.FindByIdAsync(queueId, cancellationToken);

        return queue is null || !queue.Enabled
            ? TransferDestinationResolutionResult.Denied()
            : TransferDestinationResolutionResult.Success(InteractionTransferTargetType.Queue, queue.ItemId);
    }

    private static TransferDestinationResolutionResult ResolveEntryPoint(string entryPointId)
    {
        return string.IsNullOrWhiteSpace(entryPointId)
            ? TransferDestinationResolutionResult.Denied()
            : TransferDestinationResolutionResult.Success(InteractionTransferTargetType.EntryPoint, entryPointId.Trim());
    }

    private async Task<TransferDestinationResolutionResult> ResolveExternalAsync(
        string targetId,
        ClaimsPrincipal principal)
    {
        if (principal is null ||
            !await _authorizationService.AuthorizeAsync(principal, ContactCenterPermissions.TransferExternally))
        {
            return TransferDestinationResolutionResult.Denied();
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<ContactCenterExternalTransferSettings>();
        var entry = settings.Destinations
            .FirstOrDefault(d => string.Equals(d.Id, targetId, StringComparison.OrdinalIgnoreCase));

        if (entry is null || !entry.Enabled)
        {
            return TransferDestinationResolutionResult.Denied();
        }

        if (!IsAllowedExternalAddress(entry.E164Address))
        {
            return TransferDestinationResolutionResult.Denied();
        }

        return TransferDestinationResolutionResult.Success(InteractionTransferTargetType.External, entry.E164Address);
    }

    private static bool IsAllowedExternalAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address) ||
            !address.StartsWith('+') ||
            address.Length < 8 ||
            address.Length > 16)
        {
            return false;
        }

        var digits = address.Substring(1);

        if (!digits.All(char.IsDigit))
        {
            return false;
        }

        return !IsEmergencyNumber(digits) && !IsPremiumNumber(digits);
    }

    private static bool IsEmergencyNumber(string digits)
    {
        return digits is "911" or "112" or "999" ||
            digits.EndsWith("911", StringComparison.Ordinal) ||
            digits.EndsWith("112", StringComparison.Ordinal) ||
            digits.EndsWith("999", StringComparison.Ordinal);
    }

    private static bool IsPremiumNumber(string digits)
    {
        return digits.StartsWith("1900", StringComparison.Ordinal) ||
            digits.StartsWith("1976", StringComparison.Ordinal) ||
            digits.StartsWith("4470", StringComparison.Ordinal);
    }
}
