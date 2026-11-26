using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// Builds the new-ticket form's own rows as cards. The ticket type's content editor sits between the two groups and is
/// not grouped in cards, so the cards come in two groups, <see cref="RequestGroup"/> above it and
/// <see cref="MessageGroup"/> below it.
/// </summary>
internal sealed class TicketCreateEditDisplayDriver : DisplayDriver<TicketCreateViewModel>
{
    /// <summary>
    /// The group of the cards above the ticket type's fields: who the ticket is for and where it goes.
    /// </summary>
    public const string RequestGroup = "TicketRequest";

    /// <summary>
    /// The group of the cards below the ticket type's fields: the first message and protected information.
    /// </summary>
    public const string MessageGroup = "TicketMessage";

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketCreateEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketCreateEditDisplayDriver(IStringLocalizer<TicketCreateEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(TicketCreateViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<TicketCreateViewModel>("TicketCreateRequester_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Requester"], "10"))
                .OnGroup(RequestGroup),
            Initialize<TicketCreateViewModel>("TicketCreateRouting_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Routing"], "20"))
                .OnGroup(RequestGroup),
            Initialize<TicketCreateViewModel>("TicketCreateMessage_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Message"], "10"))
                .OnGroup(MessageGroup),
            Initialize<TicketCreateViewModel>("TicketCreateProtected_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Protected Information"], "20"))
                .OnGroup(MessageGroup));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(TicketCreateViewModel model, UpdateEditorContext context)
    {
        if (context.GroupId == RequestGroup)
        {
            await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Requester, m => m.RequesterName, m => m.RequesterEmail, m => m.CategoryId, m => m.DeskId, m => m.PriorityId);
        }
        else if (context.GroupId == MessageGroup)
        {
            await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Body, m => m.ProtectedLabel, m => m.ProtectedValue);
        }

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates.
    protected override void BuildPrefix(TicketCreateViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(TicketCreateViewModel source, TicketCreateViewModel model)
    {
        model.ContentType = source.ContentType;
        model.Requester = source.Requester;
        model.RequesterName = source.RequesterName;
        model.RequesterEmail = source.RequesterEmail;
        model.CategoryId = source.CategoryId;
        model.Categories = source.Categories;
        model.CategoryRequired = source.CategoryRequired;
        model.DeskId = source.DeskId;
        model.Desks = source.Desks;
        model.PriorityId = source.PriorityId;
        model.Priorities = source.Priorities;
        model.Body = source.Body;
        model.CanAttach = source.CanAttach;
        model.ProtectedLabel = source.ProtectedLabel;
    }
}
