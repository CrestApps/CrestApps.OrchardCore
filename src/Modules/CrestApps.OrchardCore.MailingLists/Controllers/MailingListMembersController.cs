using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.MailingLists.ViewModels;
using CrestApps.OrchardCore.Timeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.MailingLists.Controllers;

/// <summary>
/// The members of a mailing list: the list with its search, status and source filters and bulk actions, a member's page
/// with the evidence of consent and the timeline, and the form that adds a member by hand. Every change goes through
/// <see cref="IMailingListMemberService"/>.
/// </summary>
[Admin]
public sealed class MailingListMembersController : Controller
{
    private const string NotSyncedFilter = "NotSynced";

    private readonly IMailingListManager _lists;
    private readonly IMailingListMemberStore _members;
    private readonly IMailingListMemberService _memberService;
    private readonly IMailingListProviderResolver _providers;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShapeFactory _shapeFactory;
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListMembersController"/> class.
    /// </summary>
    public MailingListMembersController(
        IMailingListManager lists,
        IMailingListMemberStore members,
        IMailingListMemberService memberService,
        IMailingListProviderResolver providers,
        IAuthorizationService authorizationService,
        IShapeFactory shapeFactory,
        IShellFeaturesManager shellFeaturesManager,
        INotifier notifier,
        IHtmlLocalizer<MailingListMembersController> htmlLocalizer,
        IStringLocalizer<MailingListMembersController> stringLocalizer)
    {
        _lists = lists;
        _members = members;
        _memberService = memberService;
        _providers = providers;
        _authorizationService = authorizationService;
        _shapeFactory = shapeFactory;
        _shellFeaturesManager = shellFeaturesManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the members of a mailing list.
    /// </summary>
    [Admin("mailing-lists/{listId}/members", "MailingListMembersIndex")]
    public async Task<IActionResult> Index(string listId, MailingListMembersOptions options, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MailingListsPermissions.ViewMailingListMembers))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(listId) || await _lists.FindByIdAsync(listId) is not { } list)
        {
            return NotFound();
        }

        options ??= new MailingListMembersOptions();

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, members) = await _members.PageAsync(new MailingListMemberQuery
        {
            ListId = list.ItemId,
            Status = Enum.TryParse<MailingListMemberStatus>(options.Status, out var status) ? status : null,
            NotSyncedOnly = string.Equals(options.Status, NotSyncedFilter, StringComparison.Ordinal),
            Source = options.Source,
            Search = options.Search,
        }, pager.GetStartIndex(), pager.PageSize);

        var routeData = new RouteData();

        routeData.Values.TryAdd("listId", list.ItemId);

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd("Options.Search", options.Search);
        }

        if (!string.IsNullOrEmpty(options.Status))
        {
            routeData.Values.TryAdd("Options.Status", options.Status);
        }

        if (!string.IsNullOrEmpty(options.Source))
        {
            routeData.Values.TryAdd("Options.Source", options.Source);
        }

        var pagerShape = await _shapeFactory.PagerAsync(pager, count, routeData);
        var canManage = await _authorizationService.AuthorizeAsync(User, MailingListsPermissions.ManageMailingListMembers);

        options.BulkActions = [];

        if (canManage)
        {
            options.BulkActions.Add(new SelectListItem(S["Unsubscribe"], nameof(MailingListMemberBulkAction.Unsubscribe)));
            options.BulkActions.Add(new SelectListItem(S["Send a new confirmation link"], nameof(MailingListMemberBulkAction.ResendConfirmation)));

            // The members of a list kept on the site are always synced.
            if (!list.IsLocal())
            {
                options.BulkActions.Add(new SelectListItem(S["Retry sync"], nameof(MailingListMemberBulkAction.RetrySync)));
            }

            options.BulkActions.Add(new SelectListItem(S["Erase (privacy request)"], nameof(MailingListMemberBulkAction.Erase)));
        }

        return View(new MailingListMembersIndexViewModel
        {
            MailingList = list,
            Options = options,
            CanManage = canManage,
            EraseConfirmation = canManage ? await EraseConfirmationAsync(list) : null,
            List = new AdminListContext<MailingListMember>
            {
                Source = members,
                Pager = pagerShape,
                ToolbarActions = await _shapeFactory.CreateAsync<MailingListMembersFilterViewModel>("MailingListMembersFilter", model =>
                {
                    model.Statuses = Statuses(options.Status);
                    model.Sources = Sources(options.Source);
                }),
            },
        });
    }

    /// <summary>
    /// Applies the search and the status and source filters.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("mailing-lists/{listId}/members", "MailingListMembersIndex")]
    public async Task<IActionResult> IndexFilterPost(string listId, MailingListMembersIndexViewModel model, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MailingListsPermissions.ViewMailingListMembers))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { "listId", listId },
            { "Options.Search", model.Options?.Search },
            { "Options.Status", model.Options?.Status },
            { "Options.Source", model.Options?.Source },
            { "pageSize", pagerParameters.PageSize },
        });
    }

    /// <summary>
    /// Runs a bulk action on the checked members.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("mailing-lists/{listId}/members", "MailingListMembersIndex")]
    public async Task<IActionResult> IndexBulkPost(string listId, MailingListMembersIndexViewModel model, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MailingListsPermissions.ManageMailingListMembers))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(listId) || await _lists.FindByIdAsync(listId) is not { } list)
        {
            return NotFound();
        }

        var changed = 0;

        foreach (var id in itemIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal) ?? [])
        {
            // A member is read by id and must belong to this list; ids from another list are ignored.
            if (await _members.FindByIdAsync(id) is not { } member || member.ListId != list.ItemId)
            {
                continue;
            }

            changed += model.Options?.BulkAction switch
            {
                MailingListMemberBulkAction.Unsubscribe => await _memberService.UnsubscribeAsync(list.ItemId, member.NormalizedEmail, MailingListMemberSources.Admin, S["Unsubscribed by an administrator."]),
                MailingListMemberBulkAction.ResendConfirmation => await _memberService.ResendConfirmationAsync(member.ItemId) ? 1 : 0,
                MailingListMemberBulkAction.RetrySync => await _memberService.RetrySyncAsync(member.ItemId) ? 1 : 0,
                MailingListMemberBulkAction.Erase => await _memberService.EraseAsync(member.NormalizedEmail),
                _ => 0,
            };
        }

        if (changed > 0)
        {
            await _notifier.SuccessAsync(H["{0} memberships were changed.", changed]);
        }
        else
        {
            await _notifier.WarningAsync(H["No membership was changed."]);
        }

        return RedirectToAction(nameof(Index), new { listId });
    }

    /// <summary>
    /// Shows a member's state, the evidence of consent and the timeline.
    /// </summary>
    [Admin("mailing-lists/{listId}/members/{id}", "MailingListMembersDetails")]
    public async Task<IActionResult> Details(string listId, string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MailingListsPermissions.ViewMailingListMembers))
        {
            return Forbid();
        }

        var (list, member) = await FindAsync(listId, id);

        if (member is null)
        {
            return NotFound();
        }

        var canManage = await _authorizationService.AuthorizeAsync(User, MailingListsPermissions.ManageMailingListMembers);

        return View(new MailingListMemberDetailsViewModel
        {
            MailingList = list,
            Member = member,
            ProviderName = ProviderDisplayName(list.GetProviderName()),
            CanManage = canManage,
            EraseConfirmation = canManage ? await EraseConfirmationAsync(member) : null,
            ShowTimeline = (await _shellFeaturesManager.GetEnabledFeaturesAsync()).Any(feature => feature.Id == TimelineConstants.Features.Timeline),
        });
    }

    /// <summary>
    /// Unsubscribes one member.
    /// </summary>
    [HttpPost]
    [Admin("mailing-lists/{listId}/members/{id}/unsubscribe", "MailingListMembersUnsubscribe")]
    public async Task<IActionResult> Unsubscribe(string listId, string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MailingListsPermissions.ManageMailingListMembers))
        {
            return Forbid();
        }

        var (list, member) = await FindAsync(listId, id);

        if (member is null)
        {
            return NotFound();
        }

        if (await _memberService.UnsubscribeAsync(list.ItemId, member.NormalizedEmail, MailingListMemberSources.Admin, S["Unsubscribed by an administrator."]) > 0)
        {
            await _notifier.SuccessAsync(H["The member has been unsubscribed."]);
        }

        return RedirectToAction(nameof(Details), new { listId, id });
    }

    /// <summary>
    /// Sends a pending member a new confirmation link.
    /// </summary>
    [HttpPost]
    [Admin("mailing-lists/{listId}/members/{id}/resend", "MailingListMembersResend")]
    public async Task<IActionResult> Resend(string listId, string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MailingListsPermissions.ManageMailingListMembers))
        {
            return Forbid();
        }

        var (_, member) = await FindAsync(listId, id);

        if (member is null)
        {
            return NotFound();
        }

        if (await _memberService.ResendConfirmationAsync(member.ItemId))
        {
            await _notifier.SuccessAsync(H["A new confirmation link has been sent."]);
        }
        else
        {
            await _notifier.WarningAsync(H["No link was sent: the member is not waiting for one, or several links were sent in the last minutes."]);
        }

        return RedirectToAction(nameof(Details), new { listId, id });
    }

    /// <summary>
    /// Erases every membership of a member's address, for a privacy request.
    /// </summary>
    [HttpPost]
    [Admin("mailing-lists/{listId}/members/{id}/erase", "MailingListMembersErase")]
    public async Task<IActionResult> Erase(string listId, string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MailingListsPermissions.ManageMailingListMembers))
        {
            return Forbid();
        }

        var (_, member) = await FindAsync(listId, id);

        if (member is null)
        {
            return NotFound();
        }

        var erased = await _memberService.EraseAsync(member.NormalizedEmail);

        await _notifier.SuccessAsync(H["{0} memberships of the address have been erased.", erased]);

        return RedirectToAction(nameof(Index), new { listId });
    }

    /// <summary>
    /// Shows the form that adds a member by hand.
    /// </summary>
    [Admin("mailing-lists/{listId}/add-member", "MailingListMembersCreate")]
    public async Task<IActionResult> Create(string listId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MailingListsPermissions.ManageMailingListMembers))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(listId) || await _lists.FindByIdAsync(listId) is not { } list)
        {
            return NotFound();
        }

        return View(new AddMailingListMemberViewModel { MailingList = list });
    }

    /// <summary>
    /// Adds a member by hand: subscribed at once, without any e-mail or welcome coupon, with the admin's word for the
    /// person's consent kept as evidence.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("mailing-lists/{listId}/add-member", "MailingListMembersCreate")]
    public async Task<IActionResult> CreatePost(string listId, AddMailingListMemberViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MailingListsPermissions.ManageMailingListMembers))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(listId) || await _lists.FindByIdAsync(listId) is not { } list)
        {
            return NotFound();
        }

        model.MailingList = list;

        if (!EmailAddresses.IsValid(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), S["Enter one valid e-mail address."]);
        }

        if (!model.ConsentConfirmed)
        {
            ModelState.AddModelError(nameof(model.ConsentConfirmed), S["Confirm that you hold the person's consent to receive this list."]);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _memberService.SubscribeAsync(new MailingListSubscribeRequest
        {
            ListId = list.ItemId,
            Email = model.Email,
            Name = model.Name,
            Source = MailingListMemberSources.Admin,
            SourceId = User.Identity?.Name,
            ConsentForm = string.IsNullOrWhiteSpace(model.ConsentNote) ? S["Added by an administrator who confirmed the person's consent."] : model.ConsentNote.Trim(),
            Silent = true,
            Status = MailingListMemberStatus.Subscribed,
        });

        switch (result.Outcome)
        {
            case MailingListSubscribeOutcome.AlreadySubscribed:
                await _notifier.InformationAsync(H["The address is already subscribed to the list."]);
                break;

            case MailingListSubscribeOutcome.Refused:
                ModelState.AddModelError(nameof(model.Email), S["The address could not be added: {0}", result.Error]);

                return View(model);

            default:
                await _notifier.SuccessAsync(H["The member has been added."]);
                break;
        }

        return RedirectToAction(nameof(Index), new { listId });
    }

    private async Task<(MailingList List, MailingListMember Member)> FindAsync(string listId, string id)
    {
        if (string.IsNullOrEmpty(listId) || string.IsNullOrEmpty(id)
            || await _members.FindByIdAsync(id) is not { } member
            || member.ListId != listId
            || await _lists.FindByIdAsync(listId) is not { } list)
        {
            return (null, null);
        }

        return (list, member);
    }

    // An erasure deletes the address on every list. An outside provider (Mailchimp) deletes it for good, and the address
    // can never be imported there again, so the admin is told before erasing.
    private async Task<string> EraseConfirmationAsync(MailingList list)
    {
        if (!list.IsLocal())
        {
            return S["Erase every membership of each chosen address, on every list, for a privacy request? {0} deletes each address for good, and it can never be imported into {0} again. This cannot be undone.", ProviderDisplayName(list.GetProviderName())];
        }

        var outside = OutsideProviders(await _lists.GetAllAsync());

        return outside is null
            ? S["Erase every membership of each chosen address, on every list, for a privacy request? This cannot be undone."]
            : S["Erase every membership of each chosen address, on every list, for a privacy request? Where an address is also on a list kept at {0}, {0} deletes it for good, and it can never be imported into {0} again. This cannot be undone.", outside];
    }

    private async Task<string> EraseConfirmationAsync(MailingListMember member)
    {
        var lists = new List<MailingList>();

        foreach (var listId in (await _members.ListByEmailAsync(member.NormalizedEmail)).Select(membership => membership.ListId).Distinct(StringComparer.Ordinal))
        {
            if (await _lists.FindByIdAsync(listId) is { } list)
            {
                lists.Add(list);
            }
        }

        var outside = OutsideProviders(lists);

        return outside is null
            ? S["Erase every membership of this address, on every list, for a privacy request? This cannot be undone."]
            : S["Erase every membership of this address, on every list, for a privacy request? {0} deletes the address for good, and it can never be imported into {0} again. This cannot be undone.", outside];
    }

    // The names of the outside providers that keep some of the lists, or null when every list is kept on the site.
    private string OutsideProviders(IEnumerable<MailingList> lists)
    {
        var names = lists
            .Where(list => !list.IsLocal())
            .Select(list => ProviderDisplayName(list.GetProviderName()))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return names.Count == 0 ? null : string.Join(", ", names);
    }

    private string ProviderDisplayName(string providerName)
        => _providers.Find(providerName)?.DisplayName?.Value ?? providerName;

    private List<SelectListItem> Sources(string selected)
    {
        var items = new List<SelectListItem>
        {
            new(S["All sources"], string.Empty),
            new(S["Sign-up widget"], MailingListMemberSources.Widget),
            new(S["Workflow"], MailingListMemberSources.Workflow),
            new(S["Added by an admin"], MailingListMemberSources.Admin),
            new(S["Import"], MailingListMemberSources.Import),
            new(S["The provider"], MailingListMemberSources.Provider),
        };

        foreach (var item in items)
        {
            item.Selected = string.Equals(item.Value, selected ?? string.Empty, StringComparison.Ordinal);
        }

        return items;
    }

    private List<SelectListItem> Statuses(string selected)
    {
        var items = new List<SelectListItem>
        {
            new(S["All members"], string.Empty),
            new(S["Subscribed"], nameof(MailingListMemberStatus.Subscribed)),
            new(S["Pending"], nameof(MailingListMemberStatus.Pending)),
            new(S["Unsubscribed"], nameof(MailingListMemberStatus.Unsubscribed)),
            new(S["Cleaned"], nameof(MailingListMemberStatus.Cleaned)),
            new(S["Not synced"], NotSyncedFilter),
        };

        foreach (var item in items)
        {
            item.Selected = string.Equals(item.Value, selected ?? string.Empty, StringComparison.Ordinal);
        }

        return items;
    }
}
