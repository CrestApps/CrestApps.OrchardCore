using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.WebForm;

/// <summary>
/// Shows the Ticket Form widget's form, or what happened to the last post from it, and edits which type and desk it
/// opens tickets in.
/// </summary>
internal sealed class TicketFormPartDisplayDriver : ContentPartDisplayDriver<TicketFormPart>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IEnumerable<ITicketFormChallenge> _challenges;

    internal readonly IStringLocalizer S;

    public TicketFormPartDisplayDriver(
        IContentDefinitionManager contentDefinitionManager,
        ICatalog<TicketDesk> desks,
        IHttpContextAccessor httpContextAccessor,
        IEnumerable<ITicketFormChallenge> challenges,
        IStringLocalizer<TicketFormPartDisplayDriver> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _desks = desks;
        _httpContextAccessor = httpContextAccessor;
        _challenges = challenges;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(TicketFormPart part, BuildPartDisplayContext context)
        => Initialize<TicketFormViewModel>("TicketFormPart", model =>
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            var forThisForm = string.Equals(request?.Query[TicketFormController.FormQueryName], part.ContentItem.ContentItemId, StringComparison.Ordinal);

            model.WidgetId = part.ContentItem.ContentItemId;
            model.Ready = !string.IsNullOrEmpty(part.TicketType) && !string.IsNullOrEmpty(part.DeskId);
            model.SignedIn = _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
            model.ShowChallenge = _challenges.FirstOrDefault()?.IsConfigured == true;
            model.SentNumber = forThisForm ? request.Query[TicketFormController.SentQueryName].ToString() : null;
            model.SuccessMessage = !string.IsNullOrWhiteSpace(part.SuccessMessage)
                ? part.SuccessMessage
                : model.SentNumber is { Length: > 1 }
                    ? S["Thank you. We received your request {0} and will get back to you.", model.SentNumber].Value
                    : S["Thank you. We received your request and will get back to you."].Value;
            model.Error = forThisForm ? TicketFormController.Describe(request.Query[TicketFormController.ErrorQueryName].ToString(), S) : null;
        }).Location("Detail", "Content:5");

    public override IDisplayResult Edit(TicketFormPart part, BuildPartEditorContext context)
        => Initialize<TicketFormPartViewModel>("TicketFormPart_Edit", async model =>
        {
            model.TicketType = part.TicketType;
            model.DeskId = part.DeskId;
            model.SuccessMessage = part.SuccessMessage;
            model.Types = [.. (await _contentDefinitionManager.ListTypeDefinitionsAsync())
                .Where(type => type.Parts.Any(p => p.PartDefinition.Name == TicketsConstants.PartName))
                .Select(type => new SelectListItem(type.DisplayName, type.Name, type.Name == part.TicketType))];
            model.Desks = [.. (await _desks.GetAllAsync()).OrderBy(desk => desk.Name, StringComparer.CurrentCultureIgnoreCase).Select(desk => new SelectListItem(desk.Name, desk.ItemId, desk.ItemId == part.DeskId))];
        });

    public override async Task<IDisplayResult> UpdateAsync(TicketFormPart part, UpdatePartEditorContext context)
    {
        var model = new TicketFormPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.TicketType))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.TicketType), S["Choose the ticket type the form opens."]);
        }

        if (string.IsNullOrWhiteSpace(model.DeskId))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.DeskId), S["Choose the desk the tickets go to."]);
        }

        part.TicketType = model.TicketType;
        part.DeskId = model.DeskId;
        part.SuccessMessage = string.IsNullOrWhiteSpace(model.SuccessMessage) ? null : model.SuccessMessage.Trim();

        return Edit(part, context);
    }
}
