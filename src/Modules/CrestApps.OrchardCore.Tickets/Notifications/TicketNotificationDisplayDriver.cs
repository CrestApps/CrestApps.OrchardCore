using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Notifications;

namespace CrestApps.OrchardCore.Tickets.Notifications;

/// <summary>
/// Shows which ticket a ticket notification is about (its number and desk) in the navbar, the notification center and
/// the notification's page, and nothing more once the user may no longer see the ticket.
/// </summary>
internal sealed class TicketNotificationDisplayDriver : DisplayDriver<Notification>
{
    private readonly INotificationVisibilityEvaluator _visibilityEvaluator;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICatalog<TicketDesk> _desks;

    public TicketNotificationDisplayDriver(
        INotificationVisibilityEvaluator visibilityEvaluator,
        IHttpContextAccessor httpContextAccessor,
        ICatalog<TicketDesk> desks)
    {
        _visibilityEvaluator = visibilityEvaluator;
        _httpContextAccessor = httpContextAccessor;
        _desks = desks;
    }

    /// <inheritdoc/>
    public override bool CanHandleModel(Notification model)
        => TicketNotificationVisibilityHandler.IsTicketType(model?.Type);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> DisplayAsync(Notification model, BuildDisplayContext context)
    {
        if (!model.TryGet<TicketNotificationInfo>(out var info)
            || string.IsNullOrEmpty(info.Number)
            || await _visibilityEvaluator.EvaluateAsync(model, _httpContextAccessor.HttpContext?.User) != NotificationVisibility.Visible)
        {
            return null;
        }

        var desk = string.IsNullOrEmpty(info.DeskId) ? null : await _desks.FindByIdAsync(info.DeskId);
        var meta = new TicketNotificationMetaViewModel { Number = info.Number, DeskName = desk?.Name };

        return View("TicketNotificationMeta", meta)
            .Location(NotificationConstants.DisplayTypes.Header, "Meta:5")
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:10")
            .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Subject:10")
            .Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Meta:5");
    }
}

/// <summary>
/// The ticket a notification is about, as its display shows it.
/// </summary>
public sealed class TicketNotificationMetaViewModel
{
    /// <summary>
    /// Gets or sets the ticket's number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the name of the ticket's desk, if it still exists.
    /// </summary>
    public string DeskName { get; set; }
}
