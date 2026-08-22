using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using CrestApps.OrchardCore.Sms.Workspace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Sms.Workspace.Controllers;

/// <summary>
/// Composes and lists SMS broadcasts (one composer → many recipients as individual 1:1 threads).
/// </summary>
[Admin]
public sealed class SmsBroadcastsController : Controller
{
    private static readonly char[] _recipientSeparators = ['\n', '\r', ',', ';'];

    private readonly ISmsBroadcastManager _manager;
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    private readonly IHtmlLocalizer H;
    private readonly IStringLocalizer S;

    public SmsBroadcastsController(
        ISmsBroadcastManager manager,
        IOmnichannelChannelEndpointManager endpointManager,
        IAgentProfileManager agentProfileManager,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<SmsBroadcastsController> htmlLocalizer,
        IStringLocalizer<SmsBroadcastsController> stringLocalizer)
    {
        _manager = manager;
        _endpointManager = endpointManager;
        _agentProfileManager = agentProfileManager;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("sms/broadcasts", "SmsBroadcastsIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.SendGroupSms))
        {
            return Forbid();
        }

        var broadcasts = (await _manager.GetAllAsync())
            .OrderByDescending(b => b.CreatedUtc)
            .ToArray();

        return View(new SmsBroadcastListViewModel { Broadcasts = broadcasts });
    }

    [Admin("sms/broadcasts/create", "SmsBroadcastsCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.SendGroupSms))
        {
            return Forbid();
        }

        return View(new SmsBroadcastCreateViewModel { Endpoints = await BuildEndpointOptionsAsync(null) });
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("sms/broadcasts/create", "SmsBroadcastsCreate")]
    public async Task<IActionResult> CreatePost(SmsBroadcastCreateViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SmsWorkspacePermissions.SendGroupSms))
        {
            return Forbid();
        }

        var endpoint = string.IsNullOrEmpty(model.EndpointId) ? null : await _endpointManager.FindByIdAsync(model.EndpointId);
        var recipients = ParseRecipients(model.RecipientsText)
            .Concat(model.ContactPhones ?? [])
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .Select(number => number.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), S["A name is required."]);
        }

        if (endpoint is null)
        {
            ModelState.AddModelError(nameof(model.EndpointId), S["A sending number is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.Body))
        {
            ModelState.AddModelError(nameof(model.Body), S["A message body is required."]);
        }

        if (recipients.Count == 0)
        {
            ModelState.AddModelError(nameof(model.RecipientsText), S["At least one recipient is required."]);
        }

        if (!ModelState.IsValid)
        {
            model.Endpoints = await BuildEndpointOptionsAsync(model.EndpointId);

            return View(model);
        }

        var agent = await GetCurrentAgentAsync();

        var broadcast = await _manager.NewAsync();
        broadcast.ItemId = UniqueId.GenerateId();
        broadcast.Name = model.Name.Trim();
        broadcast.FromNumber = endpoint.Value;
        broadcast.Body = model.Body.Trim();
        broadcast.Recipients = recipients;
        broadcast.OwnerAgentId = agent?.ItemId;
        broadcast.Status = SmsBroadcastStatus.Queued;

        await _manager.CreateAsync(broadcast);
        await _notifier.SuccessAsync(H["The broadcast was queued and will be sent to {0} recipient(s).", recipients.Count]);

        return RedirectToAction(nameof(Index));
    }

    private static List<string> ParseRecipients(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split(_recipientSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IEnumerable<SelectListItem>> BuildEndpointOptionsAsync(string selectedId)
    {
        var endpoints = await _endpointManager.GetAllAsync();

        return endpoints
            .Where(endpoint => string.Equals(endpoint.Channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase))
            .Select(endpoint => new SelectListItem
            {
                Text = string.IsNullOrEmpty(endpoint.DisplayText) ? endpoint.Value : $"{endpoint.DisplayText} ({endpoint.Value})",
                Value = endpoint.ItemId,
                Selected = endpoint.ItemId == selectedId,
            })
            .ToList();
    }

    private Task<CrestApps.OrchardCore.ContactCenter.Core.Models.AgentProfile> GetCurrentAgentAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrEmpty(userId)
            ? Task.FromResult<CrestApps.OrchardCore.ContactCenter.Core.Models.AgentProfile>(null)
            : _agentProfileManager.FindByUserIdAsync(userId);
    }
}
