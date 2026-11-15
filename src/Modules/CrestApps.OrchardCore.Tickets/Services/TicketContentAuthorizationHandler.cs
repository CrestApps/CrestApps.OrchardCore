using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// Puts tickets under the ticket rules wherever Orchard Core's own content permissions are asked about one: the
/// content item pages, the front-end item route, the content record picker and anything else that checks
/// <c>ViewContent</c> or <c>EditContent</c> on a content item. Viewing follows the ticket scope (desk and operation);
/// editing, publishing, cloning and deleting through the generic content pages are refused, because a ticket changes
/// only through the ticket service (its lock, its events, its read-only rule). What the content type's permissions say
/// doesn't matter: this handler fails the requirement, which no other handler can override.
/// </summary>
/// <remarks>
/// A content item is a ticket when it has <see cref="TicketPart"/> with a desk, as every ticket the ticket service
/// creates does. An item of a type an administrator gave the part to, made outside Tickets, has no desk and is left to
/// Orchard Core's rules.
/// </remarks>
internal sealed class TicketContentAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private static readonly HashSet<string> _view = new(StringComparer.Ordinal)
    {
        "ViewContent",
        "ViewOwnContent",
        "PreviewContent",
        "PreviewOwnContent",
        "ListContent",
    };

    private static readonly HashSet<string> _change = new(StringComparer.Ordinal)
    {
        "EditContent",
        "EditOwnContent",
        "PublishContent",
        "PublishOwnContent",
        "DeleteContent",
        "DeleteOwnContent",
        "CloneContent",
        "CloneOwnContent",
    };

    private readonly IServiceProvider _services;

    // The access service is resolved when needed: it reaches services that ask for authorization themselves.
    public TicketContentAuthorizationHandler(IServiceProvider services)
    {
        _services = services;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.Resource is not ContentItem item
            || !item.TryGet<TicketPart>(out var part)
            || string.IsNullOrEmpty(part.DeskId)
            || requirement.Permission?.Name is not { } name)
        {
            return;
        }

        // A content type's own permission, such as EditContent_Ticket, counts as the general one.
        var general = name.Split('_', 2)[0];

        if (_change.Contains(general))
        {
            context.Fail();

            return;
        }

        if (!_view.Contains(general))
        {
            return;
        }

        if (context.User is not null
            && await _services.GetRequiredService<ITicketAccessService>().CanAsync(context.User, item, TicketOperation.ViewAssigned))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
    }
}
