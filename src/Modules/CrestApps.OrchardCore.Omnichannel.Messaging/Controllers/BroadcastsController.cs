using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Controllers;

/// <summary>
/// Composes and lists broadcasts (one composer → many recipients as individual 1:1 threads) on any messaging
/// channel that supports them.
/// </summary>
[Admin]
public sealed class BroadcastsController : Controller
{
    private static readonly char[] _recipientSeparators = ['\n', '\r', ',', ';'];

    private readonly IMessagingBroadcastManager _manager;
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly MessagingWorkspaceBuilder _workspaceBuilder;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    private readonly IHtmlLocalizer H;
    private readonly IStringLocalizer S;

    public BroadcastsController(
        IMessagingBroadcastManager manager,
        IOmnichannelChannelEndpointManager endpointManager,
        IMessagingChannelResolver channelResolver,
        MessagingWorkspaceBuilder workspaceBuilder,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<BroadcastsController> htmlLocalizer,
        IStringLocalizer<BroadcastsController> stringLocalizer)
    {
        _manager = manager;
        _endpointManager = endpointManager;
        _channelResolver = channelResolver;
        _workspaceBuilder = workspaceBuilder;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("messaging/broadcasts", "MessagingBroadcastsIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, MessagingPermissions.SendGroupMessages))
        {
            return Forbid();
        }

        var broadcasts = (await _manager.GetAllAsync())
            .OrderByDescending(b => b.CreatedUtc)
            .ToArray();

        return View(new BroadcastListViewModel
        {
            Broadcasts = broadcasts,
            Channels = _workspaceBuilder.GetChannels().ToDictionary(channel => channel.Name, StringComparer.OrdinalIgnoreCase),
        });
    }

    [Admin("messaging/broadcasts/create", "MessagingBroadcastsCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, MessagingPermissions.SendGroupMessages))
        {
            return Forbid();
        }

        var model = new BroadcastCreateViewModel();

        await PopulateEndpointsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("messaging/broadcasts/create", "MessagingBroadcastsCreate")]
    public async Task<IActionResult> CreatePost(BroadcastCreateViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MessagingPermissions.SendGroupMessages))
        {
            return Forbid();
        }

        var endpoint = string.IsNullOrEmpty(model.EndpointId) ? null : await _endpointManager.FindByIdAsync(model.EndpointId);
        var channel = _channelResolver.Get(endpoint?.Channel);

        var recipients = ParseRecipients(model.RecipientsText)
            .Concat(model.ContactAddresses ?? [])
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => channel?.NormalizeAddress(address) ?? address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), S["A name is required."]);
        }

        if (endpoint is null || channel is null)
        {
            ModelState.AddModelError(nameof(model.EndpointId), S["Select the address to send from."]);
        }
        else
        {
            if (!channel.Capabilities.SupportsBroadcast)
            {
                ModelState.AddModelError(nameof(model.EndpointId), S["{0} does not support broadcasts.", channel.DisplayName]);
            }

            var invalid = recipients.Where(address => !channel.IsValidAddress(address)).ToArray();

            if (invalid.Length > 0)
            {
                ModelState.AddModelError(nameof(model.RecipientsText), S["These are not valid {0} addresses: {1}", channel.DisplayName, string.Join(", ", invalid)]);
            }
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
            await PopulateEndpointsAsync(model);

            return View(model);
        }

        var agent = await _workspaceBuilder.GetCurrentAgentAsync(User);

        var broadcast = await _manager.NewAsync();
        broadcast.ItemId = UniqueId.GenerateId();
        broadcast.Name = model.Name.Trim();
        broadcast.Channel = channel.Name;
        broadcast.ServiceAddress = endpoint.Value;
        broadcast.Body = model.Body.Trim();
        broadcast.Recipients = recipients;
        broadcast.OwnerAgentId = agent?.ItemId;
        broadcast.Status = MessagingBroadcastStatus.Queued;

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

    private async Task PopulateEndpointsAsync(BroadcastCreateViewModel model)
    {
        var options = await _workspaceBuilder.BuildEndpointOptionsAsync(null, model.EndpointId);

        model.Endpoints = options.Items;
        model.EndpointChannels = options.EndpointChannels;
    }
}
